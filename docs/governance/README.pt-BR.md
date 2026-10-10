# Proposta de governança AI-First

[English](README.md)

**Status:** Apenas proposta para revisão. Este diretório não é um workflow executável do GitHub Actions. A [issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15) controla a alteração humana e separada do arquivo protegido `.github/workflows/ai-evolution.yml`.

## Projeto da automação

A proposta [ai-evolution-proposed.yml](ai-evolution-proposed.yml) substitui o fluxo quebrado por **três jobs com permissões isoladas**:

1. **Engineer:** checkout de SHA imutável, Codex com `OPENAI_API_KEY` e **sem token de escrita no GitHub**; entrega somente um patch versionado como artefato de curta duração.
2. **Validate:** em runner separado e com acesso somente de leitura, copia o guard confiável **antes de aplicar o patch**, bloqueia alterações em arquivos protegidos (inclusive mudanças já commitadas, todos os arquivos sob `.ai/`, `.github/workflows/`, `docs/governance/` e `tests/ai_harness/`), verifica SHA-base e executa testes, restore/build/format, configurações de segurança e Compose. Produz o SHA-256 do patch validado.
3. **Publish:** possui o único token com escrita e só roda após validação; confirma o hash do artefato, reaplica o patch, repete o guard confiável e abre **uma branch e um PR**, depois dispara CI, Security e DAST nessa branch, sem merge automático.

Todas as ações usam SHA completo, tarefas são disparadas manualmente, uma execução por vez. Não há deploy em produção ou escrita direta na `main`.

## Ativação exclusiva pelo mantenedor

1. Revisar e integrar o [PR #17](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/17) para disponibilizar o guard na `main`.
2. Revisar este PR de proposta e seus testes; exigir CI e Security aprovados.
3. Criar **outra branch exclusiva de governança**, partindo da `main`. Somente o mantenedor deve copiar manualmente o conteúdo revisado de `docs/governance/ai-evolution-proposed.yml` para `.github/workflows/ai-evolution.yml`. O agente **não** deve alterar esse arquivo.
4. Validar em ambiente local e abrir PR de governança com aprovação humana:
   ```bash
   ruby -e 'require "yaml"; YAML.parse_file(".github/workflows/ai-evolution.yml")'
   actionlint .github/workflows/ai-evolution.yml
   python3 -m unittest discover -s tests/ai_harness -p 'test_*.py' -v
   ```
5. Conferir os escopos do GitHub Actions, configurar `OPENAI_API_KEY` como secret do repositório (nunca em código, logs ou arquivos), e integrar somente após os gates.
6. Executar um `workflow_dispatch` **na `main`** com tarefa inofensiva de documentação, revisar o PR criado, confirmar que o agente não recebeu token de escrita nem mudou políticas e que não houve auto-merge.

**Importante:** PRs criados com `GITHUB_TOKEN` padrão podem deixar workflows de `pull_request` aguardando aprovação humana. Para não depender apenas desse evento, a proposta autoriza `actions: write` **somente no job Publish**, que dispara explicitamente CI, Security e DAST via `workflow_dispatch` na branch gerada. O mantenedor ainda precisa confirmar que **as três verificações terminaram com sucesso** e aprovar eventuais execuções pendentes antes do merge.

O workflow proposto é uma demonstração controlada, não um mecanismo autorizado para alterar produção ou contornar revisão humana. Testes automáticos não substituem revisão de código e segurança independente.

## Critérios de aceite da ativação

O mantenedor deve confirmar evidências no GitHub Actions para a execução de demonstração: task gerada sem credenciais de escrita no job do agente, artefato de patch com SHA-256 verificado, testes do guard e controles de autorização aprovados, um único PR publicado, revisão humana e **nenhuma alteração na branch principal sem merge aprovado**. Em uma execução separada e isolada, os testes automatizados do guard cobrem criação de workflows novos, alteração de governança já commitada, exclusão, renomeação e erros de SHA-base.

A correção do workflow não deve ser considerada concluída apenas porque o YAML é válido: a execução controlada precisa passar em ambiente real, e CI, Security e DAST disparados explicitamente devem ser confirmados como aprovados antes do merge.
