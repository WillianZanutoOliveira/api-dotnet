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

## Consequências

A automação passa a ser demonstrável como parte do SDLC, mantendo revisão humana e quality gates como fronteiras explícitas.
