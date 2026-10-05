[🇺🇸 English](0010-software-supply-chain.en.md)

# ADR-0010 — Automação de segurança da cadeia de software e releases atestados

## Status

Aceito.

## Contexto

CodeQL, Trivy e SBOM já verificavam o repositório, mas uma cadeia de entrega madura também deve reduzir risco no próprio CI e permitir verificar a origem dos artefatos publicados.

Referências GitHub Actions por tags mutáveis também criam uma dependência indireta entre o workflow e o estado futuro daquela tag.

## Decisão

Adotar quatro controles adicionais.

### 1. Actions pinadas por commit

As actions utilizadas por CI, Security e AI Harness são referenciadas por SHA de commit imutável e mantêm comentário da versão humana correspondente.

Exemplo:

```yaml
uses: actions/checkout@<commit-sha> # v7.0.1
```

Dependabot continua responsável por propor atualizações futuras.

### 2. OpenSSF Scorecard

O workflow `.github/workflows/scorecard.yml` executa semanalmente e em alterações de `main`.

O resultado:

- é gerado em SARIF;
- fica disponível como artifact temporário;
- é enviado para GitHub Code Scanning;
- pode publicar o resultado público do Scorecard.

O Scorecard não substitui CodeQL nem Trivy. Ele avalia práticas do projeto e da cadeia de desenvolvimento.

### 3. Ownership e política de segurança

`.github/CODEOWNERS` torna ownership de código, plataforma, automação e segurança explícito.

`SECURITY.md` documenta canal de reporte e regras para não expor vulnerabilidades ou segredos publicamente.

### 4. Releases OCI com proveniência

Tags `v*` disparam `.github/workflows/release.yml`.

O workflow:

1. constrói Gateway + quatro workloads;
2. publica imagens versionadas no GitHub Container Registry;
3. publica uma segunda tag baseada no SHA;
4. gera attestation criptográfica de build para cada digest;
5. usa GitHub OIDC/Sigstore, sem chave privada de assinatura persistente.

A attestation é vinculada ao digest OCI, não apenas à tag mutável.

## Permissões

Os workflows seguem least privilege.

A emissão de attestation recebe explicitamente:

- `contents: read`;
- `packages: write`;
- `id-token: write`;
- `attestations: write`;
- `artifact-metadata: write`.

O OIDC token é curto e emitido apenas durante o job.

## Verificação

Uma imagem publicada pode ter sua origem validada com GitHub CLI, por exemplo:

```bash
gh attestation verify oci://ghcr.io/<owner>/<repo>/orders:<version> \
  --repo <owner>/<repo>
```

A política de consumo de produção deve verificar attestations; apenas gerar uma attestation não torna um artefato automaticamente seguro.

## Consequências

### Benefícios

- menor risco de action mutable;
- avaliação contínua de postura OpenSSF;
- provenance verificável;
- melhor transparência de dependências;
- releases reproduzíveis por tag + SHA;
- sinal forte de DevSecOps e software supply-chain engineering.

### Trade-offs

- publicar imagens cria artefatos adicionais no GHCR;
- as permissões de attestation só são necessárias no workflow de release;
- controles de repositório como branch rulesets ainda dependem de configuração administrativa do GitHub;
- provenance prova origem/processo, não ausência de vulnerabilidades.
