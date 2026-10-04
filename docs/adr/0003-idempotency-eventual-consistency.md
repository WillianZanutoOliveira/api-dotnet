[🇺🇸 English](0003-idempotency-eventual-consistency.en.md)

# ADR-0003: Consumers idempotentes e consistência eventual

- **Status:** Aceito
- **Data:** 2026-10-03

## Contexto

Fluxos distribuídos baseados em RabbitMQ devem assumir que mensagens podem ser entregues mais de uma vez.

Um consumer não pode criar reservas ou pagamentos duplicados, nem transições inválidas de pedido, quando uma mensagem é reenviada ou reproduzida.

## Decisão

Utilizar proteções tanto de infraestrutura quanto de nível de negócio:

- suporte de inbox do MassTransit com EF Core;
- registros únicos de decisão por pedido em Inventory e Payments;
- verificações de estado terminal no agregado Order;
- políticas de retry nos receive endpoints.

A jornada de checkout é modelada como eventualmente consistente, em vez de tentar uma transação ACID global.

## Consequências

### Positivas

- retries e replays podem ocorrer com segurança;
- não há transação global de banco de dados;
- os serviços permanecem implantáveis de forma independente;
- o comportamento de falhas fica explícito.

### Trade-offs

- clientes precisam tolerar o estado `Pending`;
- observabilidade é necessária para compreender o progresso assíncrono;
- caminhos de compensação/falha precisam ser modelados explicitamente.
