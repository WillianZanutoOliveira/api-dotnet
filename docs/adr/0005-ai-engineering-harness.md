[🇺🇸 English](0005-ai-engineering-harness.en.md)

# ADR-0005 — Harness de engenharia assistida por IA

## Status

Aceito.

## Contexto

Agentes de código conseguem alterar arquivos e executar o ciclo build/test, mas automação sem limites pode introduzir regressões, enfraquecer decisões arquiteturais ou modificar o próprio mecanismo de governança.

O objetivo do projeto é demonstrar automação de engenharia sem transformar a IA em um ator com poder irrestrito sobre a branch principal.

## Decisão

Adicionar um AI Engineering Harness baseado em Codex CLI executado por GitHub Actions.

O harness:
- é iniciado manualmente com uma tarefa pequena e explícita;
- lê AGENTS.md e a constituição de engenharia;
- pode editar o workspace do job;
- é impedido de alterar seus próprios arquivos de governança;
- precisa passar restore, build, testes e validação do Docker Compose;
- cria uma branch ai/evolution-* e um pull request;
- nunca faz merge automático.

## Credenciais

O primeiro estágio usa o secret OPENAI_API_KEY do repositório. A credencial não é gravada no código nem no artefato gerado.

Uma evolução futura pode substituir credencial de longa duração por workload identity federation.

## Evolução de segurança — preparação do guard

A CI passa a exercitar um guard de mudanças de governança com fixtures Git isoladas. Ele detecta alterações já commitadas desde um SHA-base imutável e confiável, staged, unstaged e untracked, além de renomeações e deleções de arquivos protegidos. A política bloqueia alterações ou novos arquivos dentro de `.github/workflows/`, `.ai/`, `docs/governance/` e `tests/ai_harness/`, inclusive tentativas de enfraquecer os próprios testes de governança. A implementação está em [scripts/ai-change-guard.py](../../scripts/ai-change-guard.py) e os testes em [tests/ai_harness](../../tests/ai_harness/test_ai_change_guard.py).

## Evolução aprovada — workflow instalado (10/10/2026)

Depois do guard integrado no [PR #17](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/17), a proposta documentada no [PR #18](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/18) foi aplicada ao workflow protegido em mudança separada, aprovada e integrada no [PR #19](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/19) (`d5f76b272d3b02826bd7b16eb4d032412bc5a015`). A revisão de governança está **concluída**; o guard confiável agora faz parte do workflow efetivo.

Decisão complementar: três runners isolados, `engineer` e `validate` com `contents: read`, `publish` com permissões de escrita para criar um PR e disparar checks, patches validados independentemente e vinculados por SHA-256, preservando revisão humana e proibição de auto-merge/deploy. CI, Security e DAST são disparados explicitamente na branch gerada.

**Status de aceite:** integração de código e checks de PR concluídos; primeira execução real de `workflow_dispatch` com Codex e publicação de um PR ainda não comprovada. A [issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15) só deve ser encerrada após smoke end-to-end. Ver [guia operacional](../ai-first-operations.md).

## Consequências

A automação passa a ser demonstrável como parte do SDLC, mantendo revisão humana e quality gates como fronteiras explícitas.
