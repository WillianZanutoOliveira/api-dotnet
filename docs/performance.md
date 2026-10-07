[🇺🇸 English](performance.en.md)

# Baseline de performance

O repositório usa k6 para detectar regressões de performance de forma automatizada sem tornar cada pull request mais lento.

## Estratégia

O workflow `.github/workflows/performance.yml` roda:

- semanalmente;
- sob demanda por `workflow_dispatch`.

Ele inicia a mesma stack segura usada pelo projeto, incluindo Vault, credenciais PostgreSQL dinâmicas, RabbitMQ e Keycloak.

O benchmark chama Orders diretamente em `http://localhost:8081`. Isso é intencional: o objetivo desta baseline é medir o serviço de Orders e seu caminho de persistência/outbox sem confundir o resultado com o rate limiter do YARP Gateway.

## Cenário

O script `tests/performance/orders-baseline.js`:

1. sobe gradualmente até 5 virtual users;
2. cria pedidos autenticados;
3. mantém uma carga pequena e reproduzível;
4. encerra gradualmente.

## Thresholds

O teste falha quando:

- `http_req_failed >= 1%`;
- `p95(http_req_duration) >= 1000 ms`;
- menos de 99% dos checks confirmam HTTP 201.

Esses limites são uma baseline de portfólio e não um SLO comercial definitivo. Produção real deve definir thresholds a partir de tráfego, capacidade e objetivos de negócio observados.

## Por que não roda em todo PR

Testes de performance são mais sensíveis a variação de infraestrutura compartilhada e aumentam bastante o tempo de feedback.

O pipeline principal continua rápido e determinístico. O workflow semanal identifica tendência/regressão, e qualquer mudança sensível a performance pode executar a baseline manualmente antes do merge.

## Evolução futura

Uma evolução natural é armazenar séries históricas e comparar p50/p95/p99 ao longo do tempo, além de cenários separados para leitura, escrita, mensageria e saturação.
