[🇺🇸 English](README.en.md)

# Notas de deployment no Kubernetes

Estes manifests são exemplos de portfólio que mostram como os serviços devem se comportar em um ambiente orquestrado.

Eles intencionalmente **não** fazem o deploy do PostgreSQL nem do RabbitMQ dentro do cluster. Em produção, essas dependências normalmente seriam fornecidas por serviços gerenciados ou operadores dedicados.

## O que o exemplo demonstra

- deployments independentes por serviço;
- probes de readiness/liveness usando `/health`;
- requests e limits de recursos;
- configuração separada de segredos;
- escalabilidade horizontal no nível de serviço;
- imagens de container imutáveis.

## Configuração necessária

Crie um Secret com valores equivalentes a:

```text
Orders__ConnectionString
Inventory__ConnectionString
Payments__ConnectionString
RabbitMq__Username
RabbitMq__Password
```

Crie um ConfigMap para:

```text
RabbitMq__Host
OTEL_EXPORTER_OTLP_ENDPOINT
```

O arquivo `services.yaml` incluído mostra deployments representativos. Os nomes das imagens são placeholders e devem ser substituídos por imagens publicadas por um registry/pipeline real.
