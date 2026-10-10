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

A CI passa a exercitar um guard de mudanças de governança com fixtures Git isoladas. Ele detecta alterações já commitadas desde um SHA-base imutável e confiável, staged, unstaged e untracked, além de renomeações e deleções de arquivos protegidos. A implementação está em [scripts/ai-change-guard.py](../../scripts/ai-change-guard.py) e os testes em [tests/ai_harness](../../tests/ai_harness/test_ai_change_guard.py).

**Esta evolução não ativa o workflow AI Evolution existente:** a [issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15) exige correção humana separada do workflow protegido. Uma futura integração deve executar uma cópia confiável do guard, imune a modificações feitas pelo próprio agente, antes de commitar.

## Consequências

A automação passa a ser demonstrável como parte do SDLC, mantendo revisão humana e quality gates como fronteiras explícitas.
