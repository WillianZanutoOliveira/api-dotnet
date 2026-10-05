[🇺🇸 English](local-development.en.md)

# Desenvolvimento local com .NET Aspire

## Caminho recomendado

Para desenvolvimento diário, use o AppHost:

```bash
dotnet run --project src/Platform/DistributedCommerce.AppHost
```

O Aspire inicia a infraestrutura, os serviços e o dashboard.

Na primeira execução, parâmetros secretos são gerados automaticamente pelo AppHost e armazenados no secret store local.

## Pré-requisitos

- .NET 10 SDK;
- Docker ou Podman compatível com Aspire;
- certificado de desenvolvimento ASP.NET Core confiável para a melhor experiência HTTPS do Dashboard.

Não é necessário instalar um workload Aspire legado. O projeto usa o SDK `Aspire.AppHost.Sdk/13.6.0`.

O projeto mantém `AspireUseCliBundle=false` para preservar o fluxo direto com `dotnet run` sem exigir a instalação separada do Aspire CLI. Essa é uma escolha consciente de onboarding; deve ser revisitada quando o bundle do CLI se tornar requisito obrigatório em uma versão futura do Aspire.

## O que sobe

O Dashboard deve mostrar:

```text
Infrastructure
├── orders-db
├── inventory-db
├── payments-db
├── rabbitmq
├── keycloak
├── vault
└── vault-init

Applications
├── orders-api
├── inventory-service
├── payments-service
├── notifications-service
└── api-gateway
```

## URLs principais

- Gateway: `http://localhost:8080`
- Orders: `http://localhost:8081`
- Inventory: `http://localhost:8082`
- Payments: `http://localhost:8083`
- Notifications: `http://localhost:8084`
- Keycloak: `http://localhost:8180`
- Vault: `http://localhost:8200`
- RabbitMQ Management: `http://localhost:15672`

O URL do Aspire Dashboard é aberto automaticamente pelo launch profile.

## Segurança do Dashboard

O Dashboard é uma ferramenta de desenvolvimento e pode exibir configuração, endpoints e telemetria sensível. Ele deve permanecer restrito à máquina/rede de desenvolvimento. O AppHost mantém o modo de autenticação padrão do Dashboard; não configure o frontend como `Unsecured` para expô-lo publicamente.

## Debug

Os serviços .NET rodam como projetos, não como imagens publicadas.

Isso permite colocar breakpoint diretamente em:

- Orders API;
- Inventory;
- Payments;
- Notifications;
- API Gateway.

A infraestrutura continua em containers.

## Vault continua obrigatório

Aspire não injeta diretamente as senhas dos bancos nos serviços.

Fluxo de Orders:

```text
Aspire gera senha admin do PostgreSQL
          |
          v
orders-db
          |
          +------> vault-init configura Database Secrets Engine
                           |
                           v
                  Vault gera login temporário
                           |
                           v
                    Orders API em memória
```

O token da aplicação fica em:

```text
.aspire/vault-tokens/orders/token
```

Esse diretório não entra no Git.

## Observabilidade

O Aspire Dashboard concentra o inner loop de observabilidade.

Use-o para:

- acompanhar stdout/stderr;
- abrir endpoints;
- consultar traces distribuídos;
- consultar métricas;
- identificar recurso parado ou reiniciando.

Para demonstrar Grafana + Tempo + Prometheus, use o Docker Compose seguro.

## Quando usar Docker Compose

Use:

```bash
cp .env.example .env
docker compose -f docker-compose.yml -f docker-compose.vault.yml up --build
```

quando quiser reproduzir a mesma topologia usada pelo CI ou demonstrar a observabilidade externa completa.

## Portas ocupadas

O AppHost usa portas estáveis para que Keycloak, Gateway e scripts locais tenham URLs previsíveis.

Se uma porta estiver ocupada, pare a instância anterior:

```bash
docker ps
```

Também verifique se outro `dotnet run` do projeto continua ativo.

## Reset local

Tokens do Vault são efêmeros. Para limpar apenas o estado gerado pelo Aspire, pare o AppHost e apague a pasta `.aspire/`. Ela será recriada no próximo start.

Containers e volumes devem ser limpos pelo Docker/Podman quando necessário.

## Regra prática

Para codificar:

```bash
dotnet run --project src/Platform/DistributedCommerce.AppHost
```

Para validar como o CI:

```bash
docker compose -f docker-compose.yml -f docker-compose.vault.yml config
dotnet build DistributedCommerce.slnx --configuration Release
dotnet test DistributedCommerce.slnx --configuration Release
```


### Endpoints operacionais padronizados

Todos os workloads web usam:

- `/health` — readiness;
- `/alive` — liveness.

Orders também publica, somente em Development:

- `/openapi/v1.json` — contrato OpenAPI.

O teste de topologia Aspire valida automaticamente esses endpoints.
