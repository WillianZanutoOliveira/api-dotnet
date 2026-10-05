[🇺🇸 English](secrets-management.en.md)

# Gestão de segredos

## Executando o perfil seguro

```bash
cp .env.example .env
docker compose -f docker-compose.yml -f docker-compose.vault.yml up --build
```

O arquivo `.env` contém apenas credenciais de **bootstrap local da infraestrutura**. Os serviços de aplicação não recebem as senhas de PostgreSQL e RabbitMQ como valores efetivos: o overlay as zera e o building block de secrets carrega os valores do Vault.

## Fluxo

```text
.env local (bootstrap)
        |
        v
Vault dev + vault-init
        |
        +--> policy orders ------> token file ------> Orders API
        +--> policy inventory ---> token file ------> Inventory
        +--> policy payments ----> token file ------> Payments
        +--> policy notifications > token file -----> Notifications
```

Cada token lê somente um path KV v2.

## O que observar em uma entrevista

- Vault não é apenas um container adicional: existe um boundary de identidade e policy por workload.
- Tokens são entregues por arquivos montados.
- O código falha se Vault estiver configurado mas o token/segredo não puder ser carregado.
- O CI executa o stack seguro.
- Produção deve usar identidade de plataforma, TLS, HA, audit log, rotação e TTLs adequados.
