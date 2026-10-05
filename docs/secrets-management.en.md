[🇧🇷 Português](secrets-management.md)

# Secrets management

## Running the secure profile

```bash
cp .env.example .env
docker compose -f docker-compose.yml -f docker-compose.vault.yml up --build
```

The `.env` file contains only **local infrastructure bootstrap credentials**. Application services do not use PostgreSQL and RabbitMQ passwords directly from their environment: the secure overlay blanks those values and the secrets building block loads the effective values from Vault.

## Flow

```text
local .env (bootstrap)
        |
        v
Vault dev + vault-init
        |
        +--> orders policy ------> token file ------> Orders API
        +--> inventory policy ---> token file ------> Inventory
        +--> payments policy ----> token file ------> Payments
        +--> notifications policy > token file -----> Notifications
```

Each token can read only one KV v2 path.

## What to highlight in an interview

- Vault is not just another container: there is an identity/policy boundary per workload.
- Tokens are delivered through mounted files.
- The application fails closed when Vault is configured but its token/secret cannot be loaded.
- CI executes the secure stack.
- Production requires platform identity, TLS, HA, audit logging, rotation and appropriate TTLs.
