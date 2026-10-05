[🇧🇷 Português](code-quality.md)

# Code quality and Clean Code

The repository applies quality controls at three complementary levels.

## IDE

**SonarQube for IDE** is recommended in `.vscode/extensions.json`, providing feedback while code is being written.

## Build

`SonarAnalyzer.CSharp` is referenced globally through `Directory.Build.props`, so every C# project executes the analyzer rules during compilation.

The repository also enables:

- nullable reference types;
- latest analyzer level;
- code-style enforcement during build;
- warnings as errors;
- shared rules in `.editorconfig`.

## CI

The pipeline runs restore, Release build and `dotnet format --verify-no-changes`. Formatting or analyzer violations promoted to warnings therefore cannot silently enter the codebase.

## Goal

The configuration is not intended to chase an artificial score. It makes engineering practices inspectable:

- low accidental complexity;
- no dead code;
- explicit error handling;
- consistent naming;
- uniform style;
- early feedback in the IDE and pull request.
