[🇺🇸 English](code-quality.en.md)

# Qualidade de código e Clean Code

O repositório aplica qualidade em três níveis complementares.

## IDE

A extensão **SonarQube for IDE** é recomendada em `.vscode/extensions.json`. Ela fornece feedback durante a escrita do código.

## Build

`SonarAnalyzer.CSharp` é referenciado globalmente por `Directory.Build.props`, portanto todos os projetos C# executam as regras do analisador durante compilação.

O repositório também habilita:

- nullable reference types;
- analyzers no nível mais recente;
- code style no build;
- warnings tratados como erros;
- regras compartilhadas em `.editorconfig`.

## CI

O pipeline executa restore, build Release e `dotnet format --verify-no-changes`. Assim, código que viola formatação ou regras tratadas como warning não entra silenciosamente.

## Objetivo

A configuração não existe para perseguir uma pontuação artificial. O objetivo é tornar legíveis e verificáveis práticas como:

- baixa complexidade acidental;
- ausência de código morto;
- tratamento explícito de erros;
- nomenclatura consistente;
- estilo uniforme;
- feedback antecipado no IDE e no pull request.
