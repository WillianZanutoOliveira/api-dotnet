[🇺🇸 English](0008-dotnet-aspire-local-orchestration.en.md)

# ADR-0008 — .NET Aspire como orquestrador de desenvolvimento local

## Status

Aceito.

## Contexto

O projeto possui um ambiente Docker Compose completo e seguro, porém o fluxo de desenvolvimento local exige que o desenvolvedor:

- conheça os containers que precisam subir;
- acompanhe vários logs separadamente;
- entenda a ordem de inicialização;
- descubra endpoints manualmente;
- alterne entre terminal, Docker e ferramentas de observabilidade;
- reconstrua imagens para mudanças simples de código.

Isso cria atrito para quem entra no projeto e reduz a velocidade do inner loop.

Ao mesmo tempo, a simplificação local não pode remover características importantes da arquitetura, especialmente:

- Keycloak;
- HashiCorp Vault;
- credenciais PostgreSQL dinâmicas;
- RabbitMQ;
- policies por workload;
- lease renewal;
- fail closed;
- OpenTelemetry.

## Decisão

Adotar **Aspire 13.6 / .NET 10 AppHost** como entrada preferencial para desenvolvimento local.

O AppHost fica em:

```text
src/Platform/DistributedCommerce.AppHost/
```

O comando principal passa a ser:

```bash
dotnet run --project src/Platform/DistributedCommerce.AppHost
```

O AppHost:

1. cria PostgreSQL para Orders, Inventory e Payments;
2. cria RabbitMQ;
3. cria Keycloak e importa o realm local;
4. cria Vault em modo dev;
5. executa o bootstrap do Vault;
6. grava tokens scoped em `.aspire/vault-tokens`;
7. inicia os quatro serviços .NET como processos locais;
8. inicia o YARP API Gateway;
9. conecta telemetria dos recursos ao Aspire Dashboard.

## Princípio de paridade

Aspire **não substitui a arquitetura de segurança**.

O perfil local continua exercitando:

```text
Keycloak
   |
   v
YARP Gateway
   |
   v
Orders API
   |
   +--> Vault KV v2 -> RabbitMQ credentials
   |
   +--> Vault Database Secrets Engine
                 |
                 +--> temporary PostgreSQL login
                 +--> TTL + lease
                 +--> lease renewal
```

O Docker Compose seguro continua existindo e permanece usado pelo CI. Aspire é uma segunda forma de modelar a mesma topologia para melhorar o inner loop.

## Por que os serviços rodam como projetos e a infraestrutura como containers

Os serviços .NET são adicionados por `AddProject`, permitindo:

- build incremental;
- breakpoints;
- hot reload quando suportado pela ferramenta;
- logs separados no Dashboard;
- traces e métricas no mesmo painel.

Infraestrutura continua containerizada porque representa dependências externas reais.

## Credenciais locais

Nenhuma senha efetiva é escrita no AppHost.

O Aspire gera parâmetros secretos para:

- Vault root token de desenvolvimento;
- senha administrativa de cada PostgreSQL;
- senha do RabbitMQ;
- senha administrativa do Keycloak.

Os valores são tratados pelo secret store do AppHost.

O bootstrap do Vault aceita credenciais administrativas separadas para cada PostgreSQL, mantendo compatibilidade com o Docker Compose, que ainda pode usar o par compartilhado `POSTGRES_USER/POSTGRES_PASSWORD`.

## Tokens Vault

O bootstrap escreve tokens scoped em:

```text
.aspire/vault-tokens/
├── orders/token
├── inventory/token
├── payments/token
└── notifications/token
```

A pasta inteira é ignorada pelo Git.

Os projetos recebem apenas o path do próprio token.

## Portas locais

Para manter URLs previsíveis durante desenvolvimento:

| Recurso | Porta |
| --- | ---: |
| API Gateway | 8080 |
| Orders API | 8081 |
| Inventory | 8082 |
| Payments | 8083 |
| Notifications | 8084 |
| Keycloak | 8180 |
| Vault | 8200 |
| RabbitMQ AMQP | 5672 |
| RabbitMQ Management | 15672 |
| Orders PostgreSQL | 5432 |
| Inventory PostgreSQL | 5433 |
| Payments PostgreSQL | 5434 |

O Aspire Dashboard usa seu próprio endpoint HTTPS configurado pelo AppHost.

## Observabilidade

Os projetos recebem configuração OTLP do AppHost através de `WithOtlpExporter()`.

Isso permite inspecionar no Aspire Dashboard:

- logs;
- traces;
- métricas;
- estado dos recursos;
- endpoints;
- dependências.

O stack Grafana/Tempo/Prometheus permanece disponível no Docker Compose para demonstrar uma topologia de observabilidade independente do Aspire.

## Falhas e ordem de startup

O `vault-init` é tratado como recurso one-shot.

Os serviços que usam Vault executam somente depois que esse recurso termina.

O script de bootstrap também possui retry para configuração do Database Secrets Engine, evitando race condition enquanto PostgreSQL termina de inicializar.

## Docker Compose continua sendo importante

Aspire é a experiência recomendada para desenvolvimento.

Docker Compose continua sendo usado para:

- CI;
- smoke tests;
- validação de paridade;
- demonstração da plataforma sem dependência do AppHost;
- stack Grafana/Tempo/Prometheus completo.

A existência das duas opções é intencional.

## Consequências

### Benefícios

- onboarding local muito mais simples;
- um único ponto de entrada;
- dashboard central de recursos;
- debug dos projetos .NET sem rebuild de imagem;
- endpoints previsíveis;
- secrets locais gerados automaticamente;
- preservação do fluxo Vault dinâmico;
- menor conhecimento prévio de Docker Compose necessário para contribuir.

### Trade-offs

- existe um segundo modelo de orquestração que precisa permanecer coerente com Compose;
- portas locais fixas precisam estar livres;
- o AppHost é ferramenta de desenvolvimento e não define, sozinho, a estratégia de produção;
- mudanças de infraestrutura devem ser refletidas nas duas experiências quando forem relevantes.

## Regra de manutenção

Toda mudança que afete a topologia local deve validar:

1. Docker Compose seguro;
2. AppHost Aspire;
3. documentação PT/EN.

A IA de engenharia não pode remover Vault, Keycloak ou credenciais dinâmicas apenas para simplificar o AppHost.
