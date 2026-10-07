[🇺🇸 English](0011-ef-migrations-vault-deployment-identity.en.md)

# ADR-0011 — EF Core Migrations com identidade Vault de deployment separada

## Status

Aceito.

## Contexto

Os serviços inicialmente criavam schema durante o startup com `EnsureCreatedAsync`. Isso simplificava o demo, mas misturava duas responsabilidades:

- executar a aplicação;
- alterar a estrutura do banco.

Esse desenho obrigava a identidade de runtime a possuir `CREATE` no schema e impedia usar corretamente o histórico/versionamento de EF Core Migrations.

O objetivo é aplicar **least privilege** também ao lifecycle do schema.

## Decisão

Versionar EF Core Migrations no repositório e executar as migrations por um processo one-shot chamado `DatabaseMigrator`, antes do workload iniciar.

Os serviços de negócio:

- não chamam `EnsureCreatedAsync`;
- não chamam `MigrateAsync`;
- não recebem DDL;
- iniciam somente depois que seu migrator termina com sucesso.

## Roles PostgreSQL

Cada banco possui duas roles estáveis `NOLOGIN`.

### Runtime

- `orders_runtime`
- `inventory_runtime`
- `payments_runtime`

Permissões:

- CONNECT no próprio banco;
- USAGE no schema `public`;
- DML nos objetos criados pelo migrator;
- sem `CREATE` no schema.

### Migration

- `orders_migrator`
- `inventory_migrator`
- `payments_migrator`

Permissões:

- CONNECT;
- USAGE + CREATE no schema;
- ownership dos objetos gerados pelas migrations.

`ALTER DEFAULT PRIVILEGES` concede automaticamente às roles runtime o DML necessário em novas tabelas e sequences criadas pela role migrator.

## Identidades dinâmicas Vault

Cada banco possui dois tipos de credencial dinâmica.

Runtime:

```text
database/creds/orders-app
        |
        +--> temporary LOGIN
        +--> member of orders_runtime
        +--> default TTL 5m
        +--> max TTL 24h
        +--> renewable
```

Migration:

```text
database/creds/orders-migration
        |
        +--> temporary LOGIN
        +--> member of orders_migrator
        +--> default TTL 15m
        +--> max TTL 1h
```

O token Vault do migrator:

- possui policy própria;
- só pode ler sua role de migration;
- não lê RabbitMQ/KV;
- TTL local de 30 minutos;
- não é reutilizado pelo workload.

## DatabaseMigrator

`src/Platform/DatabaseMigrator` é um processo console one-shot.

Ele recebe:

- `MIGRATION_TARGET`;
- configuração Vault;
- token da identidade migration.

Depois:

1. obtém credencial PostgreSQL dinâmica;
2. monta connection string apenas em memória;
3. seleciona o DbContext correto;
4. executa `Database.MigrateAsync()`;
5. termina.

O processo não hospeda API e não permanece ativo depois do deployment step.

## Ordem de startup

### Docker Compose seguro

```text
PostgreSQL
   |
   v
Vault bootstrap
   |
   v
DatabaseMigrator
   |
   v
Application workload
```

O workload depende de `service_completed_successfully` do migrator.

### Aspire

O AppHost modela o mesmo fluxo com `WaitForCompletion(migrator)`.

### GitOps / Kubernetes

O migrator é executado como Argo CD `PreSync` Job antes do Rollout.

O Job usa ServiceAccount e role Vault Kubernetes Auth diferentes do workload.

## Migrations versionadas

Migrations iniciais existem para Orders, Inventory e Payments e incluem `__EFMigrationsHistory`.

Factories `IDesignTimeDbContextFactory<T>` permitem gerar novas migrations sem inicializar Keycloak, RabbitMQ ou Vault.

Exemplo:

```bash
dotnet ef migrations add AddOrderReference \
  --project src/Services/Orders/Orders.Infrastructure/Orders.Infrastructure.csproj \
  --context OrdersDbContext \
  --output-dir Migrations
```

Toda migration gerada deve ser revisada antes do merge.

## CI

O pipeline comprova que:

- nenhuma aplicação contém `EnsureCreatedAsync`;
- migrations são aplicadas;
- `__EFMigrationsHistory` existe;
- `orders_runtime` não possui `CREATE`;
- `orders_migrator` possui `CREATE`;
- existe login Vault temporário associado à role migration;
- o fluxo normal Keycloak → YARP → Orders continua funcionando.

## Produção

EF Core recomenda separar a identidade de deployment com permissão de schema da identidade de runtime com apenas as permissões necessárias à aplicação.

Para uma plataforma real de alta governança, uma evolução possível é gerar **EF migration bundles** ou scripts SQL revisáveis no CI e executar esse artefato no PreSync Job. O modelo de identidade deste ADR permanece o mesmo.

## Consequências

### Benefícios

- least privilege real no banco;
- schema versionado;
- migration auditável;
- aplicação não possui DDL;
- falha de migration impede rollout;
- mesma semântica em Compose, Aspire e GitOps;
- ownership estável, sem objetos pertencendo ao login efêmero Vault.

### Trade-offs

- o deployment ganha uma etapa adicional;
- migration incompatível pode bloquear rollout;
- mudanças destrutivas exigem estratégia expand/contract;
- migrations precisam ser compatíveis com versões coexistentes durante canary.
