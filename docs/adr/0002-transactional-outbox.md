[🇺🇸 English](0002-transactional-outbox.en.md)

# ADR-0002: Transactional Outbox em vez de transações distribuídas

- **Status:** Aceito
- **Data:** 2026-10-03

## Contexto

Criar um pedido exige duas operações:

1. persistir o pedido no PostgreSQL;
2. publicar `OrderSubmitted` no RabbitMQ.

Executar essas operações de forma independente cria um problema de dual-write.

## Decisão

Utilizar o suporte de outbox do MassTransit com EF Core.

A aplicação publica normalmente por meio de uma abstração, enquanto a infraestrutura captura a mensagem de saída dentro do mesmo limite de persistência do EF Core.

A entrega ao broker acontece de forma assíncrona após o commit do banco de dados.

Consumers também utilizam a integração inbox/outbox do EF Core quando precisam persistir estado e publicar eventos subsequentes.

## Consequências

### Positivas

- não há transação distribuída em duas fases;
- a intenção de envio da mensagem fica persistida de forma durável;
- o estado do serviço e a mensagem de saída permanecem consistentes;
- o código da aplicação permanece independente do transporte.

### Trade-offs

- serviços downstream observam as mudanças de forma assíncrona;
- tabelas de outbox exigem monitoramento operacional e estratégia de limpeza;
- a entrega é at-least-once, portanto os consumers precisam ser idempotentes.
