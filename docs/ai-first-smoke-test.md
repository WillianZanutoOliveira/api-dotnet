# Teste inofensivo do AI-First

Este documento é um exemplo simples e seguro de uma alteração de documentação que pode ser proposta para validar o fluxo AI-First do projeto **Distributed Commerce Platform**.

## Objetivo

Verificar que uma tarefa pequena e explícita pode produzir documentos em português e inglês, sujeitos à revisão humana antes de qualquer integração à branch `main`.

## Escopo e segurança

- Apenas conteúdo demonstrativo de documentação, sem mudanças em código, infraestrutura ou produção.
- Nenhum segredo, credencial, token ou dado pessoal é necessário.
- A alteração deve permanecer em uma branch e em um pull request até a revisão humana; **não deve ser mesclada automaticamente**.
- O arquivo equivalente em inglês é [ai-first-smoke-test.en.md](ai-first-smoke-test.en.md).

## Limite desta evidência

Estes arquivos foram criados por solicitação direta no GitHub e **não comprovam**, por si só, que o workflow `AI Evolution Harness` executou seus jobs `engineer`, `validate` e `publish`. Para validar o fluxo real de ponta a ponta, é necessária uma execução `workflow_dispatch` bem-sucedida, com um PR gerado e os checks de CI, Security e DAST aprovados.
