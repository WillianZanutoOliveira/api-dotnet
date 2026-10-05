[🇺🇸 English](secrets-management.en.md)

# Gestão de segredos

## Objetivo

O perfil seguro do projeto usa HashiCorp Vault para duas categorias diferentes de segredo:

1. **segredos estáticos de infraestrutura que ainda precisam existir**, como as credenciais locais do RabbitMQ;
2. **credenciais PostgreSQL dinâmicas**, emitidas sob demanda pelo Vault Database Secrets Engine.

A distinção é importante: guardar uma senha fixa no Vault melhora armazenamento e governança; emitir uma credencial temporária reduz também o tempo de exposição e o blast radius.

## Executando o perfil seguro

Se você já executou versões anteriores do projeto com volumes PostgreSQL persistidos, recrie os volumes uma vez para que as roles de runtime sejam inicializadas:

```bash
docker compose -f docker-compose.yml -f docker-compose.vault.yml down -v
```

Depois:

```bash
cp .env.example .env
docker compose -f docker-compose.yml -f docker-compose.vault.yml up --build
```

O arquivo `.env` contém apenas valores de **bootstrap local da infraestrutura**.

Os containers de Orders, Inventory e Payments recebem `ConnectionStrings__*-db=""`. O connection string efetivo é construído em memória após o serviço obter um usuário PostgreSQL temporário do Vault.

## Arquitetura do fluxo

```text
                     bootstrap local
.env ------------------------------------------------+
                                                     |
                                                     v
                                            +-----------------+
                                            | Vault server-dev |
                                            +-----------------+
                                                |          |
                         KV v2 (RabbitMQ) -------+          +---- Database Secrets Engine
                                                |                        |
                                                v                        v
                                     token por workload       usuário PostgreSQL temporário
                                                |              + password + lease_id + TTL
                                                +------------------------+
                                                                         |
                                                                         v
                                                       configuração em memória do serviço
                                                                         |
                                                                         v
                                                                    PostgreSQL
```

## Paths do Vault

### KV v2

- `secret/data/platform/orders`
- `secret/data/platform/inventory`
- `secret/data/platform/payments`
- `secret/data/platform/notifications`

No perfil atual, esses paths carregam as credenciais do RabbitMQ.

### Database Secrets Engine

- `database/creds/orders-app`
- `database/creds/inventory-app`
- `database/creds/payments-app`

Esses endpoints não armazenam uma senha fixa de aplicação. Cada leitura gera um novo login PostgreSQL com validade limitada.

## Modelo de autorização PostgreSQL

As permissões estáveis ficam em roles `NOLOGIN`:

| Serviço | Role dinâmica Vault | Role PostgreSQL estável |
| --- | --- | --- |
| Orders | `orders-app` | `orders_runtime` |
| Inventory | `inventory-app` | `inventory_runtime` |
| Payments | `payments-app` | `payments_runtime` |

O usuário temporário recebe apenas membership na role de runtime correspondente.

Exemplo:

```text
v-token-orders-...  (LOGIN, temporário)
        |
        +--> member of orders_runtime (NOLOGIN)
                         |
                         +--> CONNECT orders
                         +--> USAGE public
                         +--> CREATE public apenas no demo local
```

O connection string usa:

```text
Options=-c role=orders_runtime
```

Assim, a sessão efetiva trabalha como a identidade estável de autorização.

## Lifecycle da credencial dinâmica

### 1. Emissão

No startup, `DistributedCommerce.Secrets` chama:

```text
GET /v1/database/creds/orders-app
```

A resposta inclui:

```text
username
password
lease_id
lease_duration
renewable
```

A senha não é escrita em arquivo e não é adicionada à variável de ambiente do container.

### 2. Construção do connection string

O serviço monta o connection string apenas em memória usando:

- host;
- port;
- database;
- username temporário;
- password temporário;
- role estável de runtime.

### 3. Renovação

O projeto registra `VaultLeaseRenewalService`.

Por padrão, ele tenta renovar o lease aproximadamente na metade do TTL, respeitando um mínimo de 30 segundos. O CI usa uma janela menor para tornar a renovação observável durante o smoke test.

O endpoint usado é:

```text
POST /v1/sys/leases/renew/<lease_id>
```

A renovação estende a validade do mesmo usuário temporário; ela não transforma a credencial em permanente.

### 4. Max TTL

No demo:

- TTL inicial: **5 minutos**;
- TTL máximo: **24 horas**.

Depois de atingir o `max_ttl`, a credencial não deve ser mantida indefinidamente. Um workload orquestrado deve reiniciar/reautenticar para obter uma nova identidade.

### 5. Revogação

A role Vault define statements explícitos para remover a membership e apagar o login efêmero do PostgreSQL.

## Least privilege no próprio Vault

Orders não recebe uma policy genérica de banco.

Seu token pode apenas:

```text
read   secret/data/platform/orders
read   database/creds/orders-app
update sys/leases/renew/database/creds/orders-app/*
```

Inventory e Payments possuem boundaries equivalentes para suas próprias roles.

Notifications não possui database role porque não usa PostgreSQL.

## Fail closed

Quando o perfil Vault está configurado, falhas de secrets não são tratadas com fallback silencioso.

O serviço falha quando:

- o token file não existe ou está vazio;
- o Vault não responde;
- o Database Secrets Engine não consegue emitir uma credencial;
- o lease retornado não pertence à role esperada;
- a renovação falha.

Em caso de falha de renewal, o host é encerrado. Essa escolha permite que Kubernetes/nomad/orquestrador reinicie o workload e force nova autenticação.

## O que o CI comprova

O pipeline seguro não valida apenas sintaxe.

Ele comprova que:

- build + Sonar + testes passam;
- Testcontainers consegue usar PostgreSQL real;
- Vault configura o Database Secrets Engine;
- Orders inicia sem connection string efetivo no ambiente;
- o PostgreSQL contém um login dinâmico gerado pelo Vault;
- a API cria e consulta pedidos através de Keycloak + YARP;
- o log registra renovação de lease;
- CodeQL passa;
- Trivy não encontra vulnerabilidades HIGH/CRITICAL não corrigidas nem secrets detectáveis;
- SBOM SPDX é gerado.

## Produção: diferenças obrigatórias

O perfil local é propositalmente autocontido. Ele **não é um template de produção pronto**.

Em produção:

### Vault

- executar Vault em HA;
- usar TLS;
- configurar auto-unseal apropriado;
- habilitar audit devices;
- monitorar health/latência/erros;
- ter backup e disaster recovery;
- autenticar workloads com identidade de plataforma.

### Autenticação do workload

Preferência:

```text
Kubernetes ServiceAccount
        |
        v
Vault Kubernetes Auth / Vault Secrets Operator
        |
        v
token curto e policy por workload
```

AppRole é alternativa quando uma identidade nativa de plataforma não está disponível.

### Administração PostgreSQL do Vault

Não usar o superuser `postgres` permanentemente.

Criar uma identidade administrativa exclusiva para o Vault, com somente os privilégios necessários para gerenciar usuários dinâmicos. Depois da configuração, usar os mecanismos de rotação do Database Secrets Engine quando aplicáveis.

### Migrations

Separar DDL de runtime:

```text
CI/CD migration identity ------> migrations / DDL
runtime dynamic identity ------> DML
```

No portfólio, `EnsureCreatedAsync` continua existindo para manter a experiência local simples. Por isso as roles de runtime recebem `CREATE` no schema local.

## Arquivos principais

- [Vault loader](../src/BuildingBlocks/Secrets/VaultConfigurationExtensions.cs)
- [Lease renewer](../src/BuildingBlocks/Secrets/VaultLeaseRenewalService.cs)
- [Vault bootstrap](../deploy/vault/vault-init.sh)
- [Orders DB role](../deploy/postgres/orders-init.sql)
- [Inventory DB role](../deploy/postgres/inventory-init.sql)
- [Payments DB role](../deploy/postgres/payments-init.sql)
- [Secure Compose overlay](../docker-compose.vault.yml)
- [ADR-0006](adr/0006-secrets-hashicorp-vault.md)
- [ADR-0007](adr/0007-dynamic-postgresql-credentials.md)

## O que explicar em entrevista

Uma forma curta de explicar:

> "Eu não quis apenas tirar a senha do appsettings e colocar no Vault. Para PostgreSQL eu usei o Database Secrets Engine: cada workload recebe uma identidade temporária, com TTL, lease renovável e policy própria. As permissões ficam numa role NOLOGIN estável, a aplicação monta o connection string só em memória e o processo falha fechado se não conseguir renovar o lease. O CI confirma que existe um usuário dinâmico real no PostgreSQL e que a senha efetiva não está no environment do container."

Isso mostra a diferença entre **secret storage** e **secret lifecycle management**.
