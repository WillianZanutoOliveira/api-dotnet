[🇺🇸 English](0009-service-defaults-api-resilience.en.md)

# ADR-0009 — Service Defaults, health model, OpenAPI e proteção de borda

## Status

Aceito.

## Contexto

A plataforma já possuía OpenTelemetry e um endpoint `/health`, mas cada serviço configurava esses concerns individualmente. Com a adoção do .NET Aspire, manter configuração duplicada reduz a paridade entre serviços e aumenta o risco de um novo workload nascer sem health, telemetria ou resiliência HTTP.

A API pública também precisava tornar explícitos dois concerns operacionais comuns:

- contrato HTTP inspecionável;
- proteção de borda contra bursts de tráfego.

## Decisão

Criar `src/BuildingBlocks/ServiceDefaults` como building block compartilhado e alinhado ao modelo recomendado pelo Aspire.

Cada workload chama:

```csharp
builder.AddPlatformServiceDefaults("service-name");
```

e cada aplicação web mapeia:

```csharp
app.MapPlatformDefaultEndpoints();
```

## Service Defaults

O building block padroniza:

- OpenTelemetry existente da plataforma;
- readiness em `/health`;
- liveness em `/alive`;
- service discovery para `HttpClient`;
- Standard Resilience Handler para clientes HTTP.

O Standard Resilience Handler é configurado como default para `HttpClient`. Isso evita que novos clientes HTTP sejam introduzidos sem timeout/retry/circuit-breaker padrão.

O projeto não adiciona chamadas HTTP síncronas entre os serviços de negócio atuais. RabbitMQ continua sendo o mecanismo de integração entre bounded contexts.

## Health model

`/health` representa readiness.

`/alive` executa somente checks marcados como `live`.

Essa separação permite que orquestradores tratem de forma diferente:

- aplicação viva, mas temporariamente incapaz de atender tráfego;
- processo realmente travado ou morto.

O Aspire AppHost usa readiness para ordenar recursos locais.

## OpenAPI

Orders expõe o contrato OpenAPI nativo do ASP.NET Core em ambiente Development:

```text
/openapi/v1.json
```

O teste de topologia do AppHost verifica que o documento existe e é um documento OpenAPI válido o suficiente para o smoke contract.

A exposição fica limitada a Development para não transformar automaticamente o endpoint de documentação em superfície pública de produção.

## Rate limiting no Gateway

O YARP Gateway aplica token bucket por identidade autenticada.

Particionamento:

1. claim `sub`, quando disponível;
2. IP remoto como fallback;
3. chave `unknown` como último fallback.

Configuração inicial:

- burst máximo: 60 tokens;
- refill: 60 tokens por minuto;
- fila: zero;
- resposta de rejeição: HTTP 429.

A política é aplicada apenas à rota proxy protegida. Endpoints operacionais não consomem o bucket do usuário.

## Consequências

### Benefícios

- menor duplicação;
- onboarding mais previsível;
- health semantics consistentes;
- resiliência HTTP por default;
- contrato OpenAPI verificável;
- proteção básica contra abuso e bursts;
- melhor integração com Aspire e Kubernetes.

### Trade-offs

- o rate limit em memória é por instância do Gateway;
- produção multi-réplica que exija quota global deve usar um mecanismo distribuído;
- retries HTTP devem continuar sendo usados com cuidado em operações não idempotentes;
- OpenAPI de Development não substitui governança de versionamento de API.

## Validação

O CI deve validar:

- build do Service Defaults;
- `/health` e `/alive` no teste de topologia Aspire;
- `/openapi/v1.json` no Orders local;
- smoke test existente de autenticação/autorização pelo Gateway.
