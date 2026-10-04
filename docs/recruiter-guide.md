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

O código expõe spans e métricas customizados e pode exportar via OTLP.

## 6. Testes e entrega

Evidências úteis:

- [Testes do domínio de Order](../tests/Orders.Domain.Tests/OrderTests.cs)
- [GitHub Actions CI](../.github/workflows/ci.yml)
- [Docker Compose](../docker-compose.yml)
- [Exemplos de Kubernetes](../deploy/k8s/README.md)

O pipeline de CI valida:

- restore de dependências;
- build em Release;
- testes automatizados;
- cobertura de código;
- build de quatro imagens Docker.

## 7. Decisões de arquitetura

Os ADRs documentam trade-offs, e não apenas detalhes de implementação:

- [ADR-0001 — Serviços orientados a eventos e Clean Architecture](./adr/0001-event-driven-clean-architecture.md)
- [ADR-0002 — Transactional Outbox](./adr/0002-transactional-outbox.md)
- [ADR-0003 — Idempotência e consistência eventual](./adr/0003-idempotency-eventual-consistency.md)

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
