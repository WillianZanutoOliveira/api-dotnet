[🇺🇸 English](0007-dynamic-postgresql-credentials.en.md)

# ADR-0007 — Credenciais PostgreSQL dinâmicas com Vault Database Secrets Engine

## Status

Aceito.

## Contexto

O ADR-0006 centralizou segredos de aplicação no HashiCorp Vault e removeu as credenciais efetivas de PostgreSQL/RabbitMQ do ambiente dos containers de aplicação.

Ainda existia, porém, um problema conceitual: mesmo guardada em um cofre, uma senha de banco estática continua sendo uma credencial de longa duração. Em caso de vazamento, o impacto permanece até que alguém a rotacione.

O objetivo desta evolução é eliminar credenciais PostgreSQL permanentes dos workloads de Orders, Inventory e Payments.

## Decisão

Usar o **Vault Database Secrets Engine** com o plugin PostgreSQL para emitir um usuário e uma senha exclusivos por execução de workload.

O fluxo é:

```text
Orders / Inventory / Payments
          |
          | token Vault do próprio workload
          v
Vault database/creds/<service>-app
          |
          | cria usuário PostgreSQL temporário
          | retorna username + password + lease_id + TTL
          v
Connection string construída apenas em memória
          |
          v
PostgreSQL
```

Roles Vault atuais:

- `database/creds/orders-app`
- `database/creds/inventory-app`
- `database/creds/payments-app`

Cada resposta contém um `lease_id`, um TTL e o indicador `renewable`.

## Separação entre identidade e autorização no PostgreSQL

O usuário dinâmico não recebe permissões de negócio diretamente.

Cada banco possui uma role PostgreSQL sem login:

- `orders_runtime`
- `inventory_runtime`
- `payments_runtime`

O Vault cria o usuário temporário e concede membership apenas na role correspondente.

A conexão inclui `Options=-c role=<runtime_role>`. Com isso, a sessão muda para a role estável de runtime enquanto o login efêmero continua sendo apenas a identidade temporária emitida pelo Vault.

A role de runtime não possui DDL. O ownership do schema e a criação de objetos ficam sob uma role estável de migration separada, conforme o [ADR-0011](0011-ef-migrations-vault-deployment-identity.md).

## TTL e renovação

No perfil local:

- `default_ttl = 5m`
- `max_ttl = 24h`

O building block `DistributedCommerce.Secrets` lê `lease_id`, `lease_duration` e `renewable` junto com as credenciais.

Um `BackgroundService` renova o lease antes da expiração usando o endpoint `sys/leases/renew`.

O token de cada workload possui permissão somente para:

- ler seu path KV v2;
- gerar credenciais da sua própria role de banco;
- renovar leases que pertencem ao prefixo da sua própria role.

Exemplo de boundary de Orders:

```text
secret/data/platform/orders                      read
database/creds/orders-app                        read
sys/leases/renew/database/creds/orders-app/*    update
```

## Fail closed

Se o Vault estiver configurado e:

- o token estiver ausente;
- a emissão da credencial dinâmica falhar;
- o lease retornado não tiver o prefixo esperado;
- ou uma renovação de lease falhar;

o serviço não continua silenciosamente com uma credencial fallback.

Na falha de renovação, o hosted service lança a exceção e o host é encerrado. Em ambiente orquestrado, a expectativa é que o workload reinicie, se autentique novamente no Vault e obtenha um novo usuário de banco.

Essa escolha evita manter um processo vivo com credencial potencialmente expirada.

## Statements PostgreSQL

Cada role dinâmica define explicitamente:

- `creation_statements`;
- `renew_statements`;
- `revocation_statements`;
- `rollback_statements`.

O Vault cria uma role PostgreSQL `LOGIN` temporária, define `VALID UNTIL`, concede apenas a role de runtime correspondente e atualiza o `VALID UNTIL` durante a renovação.

Na revogação, a membership é removida e o usuário efêmero é apagado.

## Credencial administrativa do plugin

No ambiente local, o Vault usa o usuário administrativo de bootstrap do PostgreSQL para configurar o plugin. Isso é um **fixture de desenvolvimento**, porque o Vault está executando em `server -dev` e seu estado é efêmero.

Em produção:

1. criar uma identidade administrativa exclusiva para o Vault, em vez de usar `postgres`;
2. conceder somente os privilégios necessários para criar, renovar e revogar as identities dinâmicas;
3. configurar a conexão no Database Secrets Engine;
4. rotacionar a credencial administrativa do plugin pelo próprio Vault quando aplicável;
5. habilitar TLS entre Vault e PostgreSQL;
6. habilitar audit devices e monitorar emissão/renovação/revogação.

## Migrações de schema

A separação de migration e runtime deixou de ser apenas uma recomendação e passou a fazer parte da implementação.

Cada banco possui duas roles estáveis:

- `<service>_runtime` — DML necessário ao workload, sem `CREATE` no schema;
- `<service>_migrator` — DDL necessário para aplicar migrations EF Core.

O Vault emite credenciais dinâmicas distintas para `<service>-app` e `<service>-migration`. Um processo one-shot `DatabaseMigrator` executa `MigrateAsync()` antes do workload iniciar. Os serviços não executam `EnsureCreatedAsync` nem migrations durante o próprio startup.

A decisão completa, incluindo ownership, default privileges e GitOps/PreSync, está no [ADR-0011](0011-ef-migrations-vault-deployment-identity.md).

## Validação no CI

O pipeline seguro valida que:

1. o stack sobe usando `docker-compose.vault.yml`;
2. Orders cria e consulta pedidos através de Keycloak + YARP;
3. existe no PostgreSQL uma role dinâmica com prefixo gerado pelo Vault;
4. `ConnectionStrings__orders-db` permanece vazio no ambiente do container de aplicação;
5. o connection string efetivo existe somente na configuração em memória carregada do Vault;
6. o log confirma pelo menos uma renovação do lease de runtime durante o smoke test;
7. `__EFMigrationsHistory` comprova que o schema foi criado por EF Migrations;
8. `orders_runtime` não possui `CREATE` no schema;
9. `orders_migrator` possui DDL;
10. existe uma identidade dinâmica Vault associada à role de migration.

## Consequências

### Benefícios

- nenhuma senha PostgreSQL de longa duração é entregue ao workload;
- credenciais são exclusivas por emissão;
- cada identidade possui validade limitada;
- revogação é centralizada no Vault;
- o blast radius de vazamento é reduzido;
- a auditoria consegue correlacionar emissão e lease;
- a aplicação demonstra lifecycle de secret, não apenas armazenamento de secret.

### Trade-offs

- Vault passa a participar do caminho de bootstrap dos serviços;
- renovação de lease precisa ser monitorada;
- após `max_ttl`, é necessário obter uma nova credencial;
- migrations e runtime exigem identities, policies e lifecycle distintos;
- disponibilidade, HA, unseal, backup e disaster recovery do Vault são responsabilidades de plataforma.

## Alternativas consideradas

### Senha estática no GitHub Secret / Kubernetes Secret

Mais simples, mas continua sendo uma credencial de longa duração e exige rotação externa.

### Senha estática no Vault KV

Melhora armazenamento e governança, mas não elimina a natureza permanente da credencial.

### Credencial dinâmica via Vault

Escolhida porque combina TTL, emissão sob demanda, revogação, lease e política de menor privilégio.
