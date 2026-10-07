[🇺🇸 English](0012-architecture-contract-chaos-gitops.en.md)

# ADR-0012 — Guardrails arquiteturais, contratos, fault injection e progressive delivery

## Status

Aceito.

## Contexto

Build verde e testes unitários não comprovam sozinhos propriedades importantes de uma plataforma distribuída:

- direção de dependências;
- estabilidade dos contratos de eventos;
- comportamento diante de falhas de rede;
- segurança de uma promoção gradual.

Essas propriedades precisam ser executáveis, não apenas descritas em documentação.

## Decisão

Adicionar quatro camadas de validação.

## Architecture tests

`tests/Architecture.Tests` usa reflexão para bloquear dependências proibidas.

Exemplos:

- Domain não referencia Application/Infrastructure/EF/MassTransit;
- Application não referencia Infrastructure;
- Contracts não referencia implementações de serviços;
- serviços de negócio não criam referências diretas entre si.

Isso transforma limites arquiteturais em quality gates.

## Contract compatibility

`tests/Contracts.Compatibility.Tests` mantém uma assinatura explícita das propriedades dos integration events publicados.

Uma remoção, rename ou mudança de tipo quebra o teste e força revisão consciente.

Essa abordagem é deliberadamente simples e local. Quando contratos forem compartilhados entre múltiplos repositórios/organizações, a evolução natural é um registry/schema compatibility service ou consumer-driven contracts.

## Fault injection

`tests/Chaos.Tests` usa Testcontainers e Toxiproxy.

O cenário atual:

1. sobe PostgreSQL real;
2. aplica migrations;
3. coloca Toxiproxy no caminho;
4. confirma conectividade;
5. corta a rede;
6. exige falha de conexão;
7. restaura o proxy;
8. exige recuperação.

Isso valida comportamento de infraestrutura descartável sem depender de um ambiente compartilhado.

## GitOps

O diretório `deploy/gitops` modela Argo CD como reconciliador.

CI publica artefatos. Uma promoção:

1. seleciona uma tag já publicada;
2. altera Kustomize por PR;
3. exige review/merge;
4. Argo CD reconcilia `main`.

CI não executa deployment imperativo em produção.

## Canary

Orders usa um Argo Rollout com:

- stable service;
- canary service;
- 20% → análise;
- 50% → análise;
- 100%.

O AnalysisTemplate chama somente o canary service em `/health/deployment`.

Falha de análise interrompe a progressão automática.

O exemplo não adiciona service mesh. Sem traffic router dedicado, os pesos são aproximados pela quantidade de réplicas. Um ambiente que exija traffic splitting exato deve integrar ingress/service mesh suportado pelo Argo Rollouts.

## Migration + progressive delivery

A migration é um Argo CD `PreSync` Job.

Isso exige migrations compatíveis com coexistência entre versão antiga e nova durante o canary. Para alterações destrutivas, usar padrão expand/contract:

1. expandir schema de forma backward-compatible;
2. publicar aplicação nova;
3. migrar/backfill;
4. remover estrutura antiga em release posterior.

## Vault Kubernetes Auth

Runtime e migration usam ServiceAccounts diferentes.

Vault Agent Injector compartilha um token curto em `/vault/secrets/token`.

Para Job de migration, `agent-pre-populate-only` evita sidecar persistente bloqueando conclusão.

## Consequências

### Benefícios

- arquitetura executável;
- contratos protegidos;
- teste real de falha/recovery;
- deployment declarativo e auditável;
- migration antes do rollout;
- canary com gate automático;
- separação clara entre build, promotion e deployment.

### Trade-offs

- testes de arquitetura por reflexão não detectam todos os acoplamentos semânticos;
- shape guard não substitui semantic versioning;
- chaos local não substitui game days em produção;
- o canary básico não oferece traffic shaping exato;
- cluster real precisa instalar/configurar Argo CD, Argo Rollouts e Vault Injector.
