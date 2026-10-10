[🇺🇸 English](ai-first-operations.en.md)

# Operação segura dos agentes AI-First

## Estado da automação

O arquivo `.github/workflows/ai-evolution.yml` continua **protegido e aguardando correção de governança humana** na [issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15). O workflow atual tem erro de sintaxe shell e etapas duplicadas: **não considere o agente autônomo operacional** até que um mantenedor corrija o workflow em uma mudança exclusiva e valide uma execução controlada.

O guard de alterações `scripts/ai-change-guard.py` e seus testes são uma **preparação verificável**. A CI executa os testes, mas o workflow AI Evolution ainda não invoca o guard; isso só pode ocorrer após a revisão humana do arquivo protegido.

## Verificação local

Requer Python 3 e Git, sem dependências Python adicionais:

```bash
python3 -m unittest discover -s tests/ai_harness -p 'test_*.py' -v
BASE_SHA="$(git rev-parse HEAD)" # registre ANTES de iniciar o agente
# Após a execução do agente, use uma cópia confiável do guard:
python3 /trusted/path/ai-change-guard.py --repo . --base-sha "$BASE_SHA"
```

O segundo comando deve ser executado em um worktree com alterações do agente **antes** de `git add`, commit ou push:

| Código | Significado |
| --- | --- |
| 0 | Há mudanças e nenhum caminho protegido foi alterado |
| 1 | Caminho de governança protegido alterado, criado, removido ou renomeado |
| 2 | Nenhuma mudança elegível ou erro do Git; falha fechada |

O guard lê mudanças **já commitadas desde o SHA-base, staged, unstaged e untracked** usando nomes separados por NUL; usa `--no-renames` para não esconder a remoção de um arquivo protegido renomeado. O SHA-base precisa ser um hash completo e imutável, registrado pelo runner antes da execução do agente; uma branch como `origin/main` não é suficiente como referência de confiança. Ele protege os arquivos listados em `AGENTS.md`, além do próprio guard, seus testes e `ci.yml`. **Todos os arquivos `.yml` e `.yaml` em `.github/workflows/` também são protegidos**, inclusive workflows novos que o agente tente criar.

## Limite de confiança obrigatório

**Não execute uma cópia do guard que o próprio agente possa reescrever**. O mantenedor do workflow deve executar o verificador a partir de uma cópia confiável obtida da `main` **antes** do passo Codex (ou de um job isolado e confiável). Se o agente alterar `scripts/ai-change-guard.py`, a cópia confiável deverá bloquear a modificação. Guard e testes não substituem revisão humana nem um job de segurança independente.

## Correção pendente do workflow protegido

Somente um mantenedor, em um PR de governança separado, deve:

1. Corrigir a aspa não fechada no passo de proteção e remover blocos Restore/Build/Test/Create PR duplicados.
2. Garantir que o guard usado seja uma cópia confiável anterior à execução do agente; verificar mudanças **antes de staged/commit/push** e novamente antes de criar o PR.
3. Executar restore, Release build, testes, `dotnet format --verify-no-changes`, `scripts/security-config-check.sh` e validação Compose. Smoke e segurança completos continuam sendo gates do PR na CI.
4. Manter GitHub Actions por SHA imutável, permissões mínimas, isolamento de `OPENAI_API_KEY` e uma única branch/PR por tarefa.
5. Nunca enviar diretamente à `main` e nunca fazer auto-merge. Fazer teste de `workflow_dispatch` com documentação inofensiva e revisão humana.

O objetivo é **uma entrega revisável**, não autonomia irrestrita. Consulte [ADR-0005](adr/0005-ai-engineering-harness.md), [AGENTS.md](../AGENTS.md) e a [constituição de engenharia](../.ai/engineering-constitution.md).
