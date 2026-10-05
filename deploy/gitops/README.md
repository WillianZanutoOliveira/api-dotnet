[🇺🇸 English](README.en.md)

# GitOps e progressive delivery

Este diretório modela uma estratégia de produção com **Argo CD + Argo Rollouts + Vault Kubernetes Auth**.

A intenção não é usar CI para executar `kubectl apply` diretamente. O pipeline publica artefatos; Git continua sendo a fonte de verdade do deployment. Esse fluxo segue o modelo GitOps recomendado pelo Argo CD: alterações de imagem entram primeiro no repositório e o controller reconcilia o cluster a partir do estado versionado.

## Fluxo

```text
tag v1.2.3
    |
    v
Release OCI Images
    |
    +--> Orders:v1.2.3
    +--> DatabaseMigrator:v1.2.3
    +--> provenance attestations
    |
    v
GitOps Promotion workflow
    |
    +--> altera newTag no Kustomization
    +--> cria PR
    |
    v
human review + merge
    |
    v
Argo CD detecta mudança em main
    |
    v
PreSync Database Migration Job
    |
    v
Argo Rollouts
    |
    +--> 20%
    +--> pausa
    +--> análise health canary
    +--> 50%
    +--> pausa
    +--> análise health canary
    +--> 100%
```

Nenhum workflow faz auto-merge da promoção.

## Estrutura

- `argocd/orders-production.yaml` — Application declarativa do Argo CD.
- `orders-canary/kustomization.yaml` — versão desejada das imagens.
- `orders-canary/migration-job.yaml` — migration como hook `PreSync`.
- `orders-canary/rollout.yaml` — estratégia canary.
- `orders-canary/analysis-template.yaml` — análise automática do canary.
- `vault/` — policies e configuração esperada para Kubernetes Auth.

## Migration antes do workload

O Job de migration usa a imagem `database-migrator` e a identidade Vault `orders-migration`.

A aplicação normal usa `orders-app`.

Isso mantém a separação:

```text
orders-migrator ServiceAccount
        |
        v
Vault Kubernetes Auth
        |
        v
database/creds/orders-migration
        |
        v
orders_migrator PostgreSQL role
        |
        +--> DDL / EF migrations


orders-api ServiceAccount
        |
        v
Vault Kubernetes Auth
        |
        v
database/creds/orders-app
        |
        v
orders_runtime PostgreSQL role
        |
        +--> DML only
```

O Job usa `vault.hashicorp.com/agent-pre-populate-only: "true"`, evitando que um Agent sidecar permanente impeça o Job de terminar.

## Canary

Orders usa 5 réplicas para que pesos sem traffic router dedicado tenham granularidade razoável.

A sequência é:

1. 20%;
2. pausa de 60 segundos;
3. três checks no service `orders-api-canary`;
4. 50%;
5. pausa de 120 segundos;
6. nova análise;
7. 100%.

A análise chama:

```text
http://orders-api-canary:8080/health/deployment
```

O endpoint responde JSON e deriva seu estado do `HealthCheckService`.

Se a análise falhar, o Rollout não continua automaticamente.

Para controle de tráfego exato em ambientes de grande volume, a evolução natural é integrar Argo Rollouts com ingress/service mesh compatível. O exemplo atual evita adicionar service mesh sem necessidade comprovada.

## Argo CD

A Application acompanha:

```text
main:deploy/gitops/orders-canary
```

Com:

- automated sync;
- self-heal;
- prune;
- PruneLast;
- ApplyOutOfSyncOnly.

Em produção com políticas mais conservadoras, `Prune=confirm` pode ser aplicado a recursos críticos.

## Promotion workflow

Depois que uma tag `v*` for publicada e as imagens existirem no GHCR, execute manualmente:

```text
GitHub Actions
→ GitOps Promotion
→ version = v1.2.3
```

O workflow:

1. valida Semantic Version;
2. confirma que a tag existe;
3. altera as duas imagens no Kustomization;
4. abre uma PR;
5. não faz merge.

Isso mantém separação entre **build**, **promotion** e **deployment**.

## Pré-requisitos do cluster

O cluster precisa ter:

- Argo CD;
- Argo Rollouts CRDs/controller;
- HashiCorp Vault Agent Injector;
- Kubernetes Auth habilitado no Vault;
- PostgreSQL/RabbitMQ/Keycloak/OTel acessíveis pelos endereços configurados.

Veja [configuração Vault Kubernetes Auth](vault/README.md).
