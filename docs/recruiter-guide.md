[🇺🇸 English](recruiter-guide.en.md)

# Walkthrough para recrutadores / engenharia

Esta página é um **guia de inspeção de 5 minutos** para recrutadores, engenheiros seniores e hiring managers avaliando o repositório.

O objetivo é facilitar a verificação das evidências de engenharia sem exigir a leitura de todo o código.

## 1. Limites entre serviços

Comece pela arquitetura de alto nível:

- [Visão geral da arquitetura](./architecture.md)
- [Contratos dos eventos de integração](../src/BuildingBlocks/Contracts/IntegrationEvents.cs)

A plataforma é dividida em quatro serviços implantáveis de forma independente:

- Orders
- Inventory
- Payments
- Notifications

Serviços com estado possuem seu próprio banco PostgreSQL. Nenhum serviço lê tabelas de outro serviço.

## 2. Clean Architecture

O bounded context de Orders é estruturado intencionalmente em:

```text
Orders.Domain
      ↑
Orders.Application
      ↑
Orders.Infrastructure
      ↑
Orders.Api
```

Arquivos úteis:

- [Agregado Order](../src/Services/Orders/Orders.Domain/Order.cs)
- [Portas da aplicação](../src/Services/Orders/Orders.Application/Abstractions.cs)
- [Caso de uso da aplicação](../src/Services/Orders/Orders.Application/OrderService.cs)
- [Composição da infraestrutura](../src/Services/Orders/Orders.Infrastructure/DependencyInjection.cs)

A camada de domínio não depende de ASP.NET Core, EF Core, RabbitMQ nem MassTransit.

## 3. RabbitMQ e colaboração orientada a eventos

O fluxo assíncrono principal é:

```text
POST /orders
   ↓
OrderSubmitted
   ↓
Inventory
   ↓
InventoryReserved / InventoryRejected
   ↓
Payments
   ↓
PaymentAuthorized / PaymentFailed
   ↓
Orders + Notifications
```

Arquivos úteis:

- [Orders API e configuração do MassTransit](../src/Services/Orders/Orders.Api/Program.cs)
- [Consumer de Inventory](../src/Services/Inventory/Inventory.Service/OrderSubmittedConsumer.cs)
- [Consumer de Payments](../src/Services/Payments/Payments.Service/InventoryReservedConsumer.cs)
- [Consumers de Notifications](../src/Services/Notifications/Notifications.Service/PaymentConsumers.cs)

## 4. Confiabilidade: Outbox, Inbox e idempotência

O repositório trata intencionalmente o problema de dual-write em sistemas distribuídos.

Orders usa MassTransit EF Core Bus Outbox para que a persistência do pedido e a intenção da mensagem de saída compartilhem o mesmo limite de persistência.

Consumers com estado usam suporte inbox/outbox e proteção contra duplicidade no nível de negócio.

Evidências úteis:

- [Orders DbContext / entidades de outbox](../src/Services/Orders/Orders.Infrastructure/OrdersDbContext.cs)
- [Inventory DbContext / inbox-outbox](../src/Services/Inventory/Inventory.Service/InventoryDbContext.cs)
- [Payments DbContext / inbox-outbox](../src/Services/Payments/Payments.Service/PaymentsDbContext.cs)
- [ADR: Transactional Outbox](./adr/0002-transactional-outbox.md)
- [ADR: Idempotência e consistência eventual](./adr/0003-idempotency-eventual-consistency.md)

## 5. Observabilidade

A plataforma usa um building block de OpenTelemetry independente de fornecedor.

Evidências úteis:

- [Building block compartilhado de telemetria](../src/BuildingBlocks/Observability/PlatformTelemetry.cs)
- [Notas de arquitetura sobre observabilidade](./architecture.md#observabilidade)

O código expõe spans e métricas customizados via OTLP. O ambiente local inclui OpenTelemetry Collector, Tempo, Prometheus e Grafana.

## 6. Testes e entrega

Evidências úteis:

- [Testes do domínio de Order](../tests/Orders.Domain.Tests/OrderTests.cs)
- [Integração real com PostgreSQL via Testcontainers](../tests/Orders.Persistence.IntegrationTests/OrderRepositoryTests.cs)
- [GitHub Actions CI](../.github/workflows/ci.yml)
- [Docker Compose](../docker-compose.yml)
- [Exemplos de Kubernetes](../deploy/k8s/README.md)

O pipeline de CI valida:

- restore de dependências;
- build em Release;
- testes automatizados;
- cobertura de código;
- quality gate Sonar/.editorconfig/dotnet format;
- testes unitários e Testcontainers;
- build do Gateway + quatro serviços;
- smoke test do fluxo Keycloak → YARP → Orders usando o perfil Vault;
- existência de usuário PostgreSQL efêmero criado pelo Vault;
- ausência de connection string efetivo no environment da aplicação;
- renovação real do lease durante o CI.

## 7. Segurança e identidade

Evidências úteis:

- [Building block de segurança](../src/BuildingBlocks/Security/KeycloakAuthenticationExtensions.cs)
- [Realm local versionado](../deploy/keycloak/distributed-commerce-realm.json)
- [Smoke test de autenticação/autorização](../scripts/auth-smoke.sh)
- [ADR de identidade](./adr/0004-identity-keycloak.md)

O projeto demonstra validação JWT, audience/issuer checks, RBAC e object-level authorization. O CustomerId não é confiado ao payload: ele vem do sub autenticado.

A camada de segredos usa HashiCorp Vault com token/policy por workload. RabbitMQ fica no KV v2; PostgreSQL usa credenciais dinâmicas. Orders, Inventory e Payments recebem um login temporário com TTL + lease renovável, enquanto as permissões ficam em roles PostgreSQL `NOLOGIN` estáveis.

Evidências:

- [Loader de secrets e credenciais dinâmicas](../src/BuildingBlocks/Secrets/VaultConfigurationExtensions.cs)
- [Renovação de lease](../src/BuildingBlocks/Secrets/VaultLeaseRenewalService.cs)
- [Bootstrap Database Secrets Engine](../deploy/vault/vault-init.sh)
- [Overlay Vault](../docker-compose.vault.yml)
- [ADR-0006 — cofre de segredos](./adr/0006-secrets-hashicorp-vault.md)
- [ADR-0007 — credenciais PostgreSQL dinâmicas](./adr/0007-dynamic-postgresql-credentials.md)

## 8. Experiência de desenvolvimento com .NET Aspire

O repositório também demonstra preocupação com **developer experience** e onboarding técnico.

Evidências:

- [Aspire AppHost](../src/Platform/DistributedCommerce.AppHost/AppHost.cs)
- [Projeto do AppHost](../src/Platform/DistributedCommerce.AppHost/DistributedCommerce.AppHost.csproj)
- [Guia de desenvolvimento local](./local-development.md)
- [ADR-0008 — Aspire local](./adr/0008-dotnet-aspire-local-orchestration.md)

Um novo desenvolvedor pode subir a topologia local com:

```bash
dotnet run --project src/Platform/DistributedCommerce.AppHost
```

O AppHost mantém Keycloak, Vault, RabbitMQ e os três PostgreSQL em containers, enquanto Gateway e os quatro serviços .NET rodam como projetos locais. Isso preserva credenciais PostgreSQL dinâmicas e lease renewal sem sacrificar breakpoints, logs por recurso e telemetria central no Aspire Dashboard.

Docker Compose continua sendo o caminho de paridade exercitado pelo CI.

## 9. Automação de engenharia com IA

Evidências úteis:

- [AGENTS.md](../AGENTS.md)
- [Constituição de engenharia](../.ai/engineering-constitution.md)
- [Workflow AI Evolution Harness](../.github/workflows/ai-evolution.yml)
- [ADR do harness](./adr/0005-ai-engineering-harness.md)

O agente pode implementar mudanças em workspace isolado, mas precisa passar quality gates e só pode entregar via branch + pull request. Não existe auto-merge.

## 10. Clean Code e DevSecOps

Evidências úteis:

- [Regras compartilhadas](../.editorconfig)
- [SonarAnalyzer no build](../Directory.Build.props)
- [Pipeline de segurança](../.github/workflows/security.yml)
- [Guia de qualidade](./code-quality.md)

O build trata warnings como erros e o CI verifica formatação/analyzers. O pipeline separado executa CodeQL, Trivy e gera SBOM SPDX.

## 11. Decisões de arquitetura

Os ADRs documentam trade-offs, e não apenas detalhes de implementação:

- [ADR-0001 — Serviços orientados a eventos e Clean Architecture](./adr/0001-event-driven-clean-architecture.md)
- [ADR-0002 — Transactional Outbox](./adr/0002-transactional-outbox.md)
- [ADR-0003 — Idempotência e consistência eventual](./adr/0003-idempotency-eventual-consistency.md)
- [ADR-0004 — Identidade e autorização com Keycloak](./adr/0004-identity-keycloak.md)
- [ADR-0005 — Harness de engenharia assistida por IA](./adr/0005-ai-engineering-harness.md)
- [ADR-0006 — Gestão centralizada de segredos com HashiCorp Vault](./adr/0006-secrets-hashicorp-vault.md)
- [ADR-0007 — Credenciais PostgreSQL dinâmicas com Vault Database Secrets Engine](./adr/0007-dynamic-postgresql-credentials.md)
- [ADR-0008 — .NET Aspire como orquestrador de desenvolvimento local](./adr/0008-dotnet-aspire-local-orchestration.md)

## O que este repositório pretende demonstrar

Este repositório não é apresentado como um sistema de produção nem como prova de que todo produto deveria usar microsserviços.

Ele é uma referência pública de engenharia criada para tornar inspecionáveis as seguintes preocupações:

- limites arquiteturais;
- mensageria assíncrona;
- entrega confiável de eventos;
- idempotência;
- consistência eventual;
- observabilidade;
- entrega containerizada;
- quality gates em CI;
- trade-offs técnicos explícitos.


## 12. Service Defaults, contrato e proteção de borda

Evidências:

- [Service Defaults](../src/BuildingBlocks/ServiceDefaults/PlatformServiceDefaults.cs)
- [Gateway com rate limiting](../src/Gateway/ApiGateway/Program.cs)
- [Orders OpenAPI](../src/Services/Orders/Orders.Api/Program.cs)
- [Teste de topologia Aspire](../tests/DistributedCommerce.AppHost.Tests/LocalTopologyTests.cs)
- [ADR-0009](./adr/0009-service-defaults-api-resilience.md)

O projeto demonstra readiness/liveness separados, service discovery, resiliência HTTP default, rate limiting por identidade e contrato OpenAPI verificável.

## 13. Software supply chain e container hardening

Evidências:

- [OpenSSF Scorecard](../.github/workflows/scorecard.yml)
- [Security pipeline](../.github/workflows/security.yml)
- [Release OCI atestado](../.github/workflows/release.yml)
- [CODEOWNERS](../.github/CODEOWNERS)
- [Security Policy](../SECURITY.md)
- [Kubernetes hardening](../deploy/k8s/services.yaml)
- [ADR-0010](./adr/0010-software-supply-chain.md)

O repositório demonstra pinning de GitHub Actions por commit, CodeQL, Trivy, SBOM, OpenSSF Scorecard, containers non-root, Kubernetes securityContext e provenance attestations para imagens publicadas.


## 14. Migrations com least privilege

Evidências:

- [DatabaseMigrator](../src/Platform/DatabaseMigrator/Program.cs)
- [Orders migrations](../src/Services/Orders/Orders.Infrastructure/Migrations)
- [Vault runtime/migration roles](../deploy/vault/vault-init.sh)
- [PostgreSQL role split](../deploy/postgres/orders-init.sql)
- [ADR-0011](./adr/0011-ef-migrations-vault-deployment-identity.md)

Ponto de entrevista: a aplicação não possui DDL. Um migrator one-shot recebe credencial dinâmica curta do Vault, aplica EF Migrations e termina; runtime recebe somente DML.

## 15. Architecture, contract e chaos testing

Evidências:

- [Architecture Tests](../tests/Architecture.Tests)
- [Contract Compatibility Tests](../tests/Contracts.Compatibility.Tests)
- [Chaos Tests](../tests/Chaos.Tests)

Esses gates mostram que arquitetura, compatibilidade de eventos e comportamento diante de falha de rede são verificáveis no pipeline.

## 16. GitOps e canary

Evidências:

- [Argo CD Application](../deploy/gitops/argocd/orders-production.yaml)
- [Argo Rollout](../deploy/gitops/orders-canary/rollout.yaml)
- [Canary analysis](../deploy/gitops/orders-canary/analysis-template.yaml)
- [PreSync migration Job](../deploy/gitops/orders-canary/migration-job.yaml)
- [GitOps Promotion workflow](../.github/workflows/gitops-promote.yml)
- [ADR-0012](./adr/0012-architecture-contract-chaos-gitops.md)

A história técnica é: build e assinatura do artefato são separados da promoção; promoção gera PR; Argo CD reconcilia Git; migration precede o rollout; Argo Rollouts promove gradualmente com análise automática.
