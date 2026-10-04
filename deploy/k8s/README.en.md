[🇧🇷 Português](README.md)

# Kubernetes deployment notes

These manifests are portfolio examples showing how the services are expected to behave in an orchestrated environment.

They intentionally do **not** deploy PostgreSQL or RabbitMQ into the cluster. In production those dependencies would normally come from managed services or dedicated operators.

## What the sample demonstrates

- independent service deployments;
- readiness/liveness probes using `/health`;
- resource requests and limits;
- configuration separated from secrets;
- horizontal scaling at service level;
- immutable container images.

## Required configuration

Create a Secret with values equivalent to:

```text
Orders__ConnectionString
Inventory__ConnectionString
Payments__ConnectionString
RabbitMq__Username
RabbitMq__Password
```

Create a ConfigMap for:

```text
RabbitMq__Host
OTEL_EXPORTER_OTLP_ENDPOINT
```

The included `services.yaml` shows representative deployments. Image names are placeholders and should be replaced by an actual registry/release pipeline.
