[🇺🇸 English](0006-secrets-hashicorp-vault.en.md)

# ADR-0006 — Gestão centralizada de segredos com HashiCorp Vault

## Status

Aceito.

## Contexto

A primeira versão do ambiente local passava credenciais de PostgreSQL e RabbitMQ diretamente por variáveis de ambiente dos containers de aplicação. Isso é simples para estudos, mas não representa a postura esperada em ambientes corporativos.

Credenciais de aplicação precisam ter:

- armazenamento centralizado;
- políticas de menor privilégio;
- possibilidade de rotação;
- identidade por workload;
- trilha de auditoria;
- menor exposição em variáveis de ambiente e arquivos de configuração.

## Decisão

Adotar HashiCorp Vault como cofre de segredos vendor-neutral.

O projeto possui um overlay seguro:

```bash
docker compose -f docker-compose.yml -f docker-compose.vault.yml up --build
```

Nesse modo:

1. Vault sobe em modo de desenvolvimento exclusivamente para a demonstração local.
2. Um bootstrap grava no KV v2 somente os segredos que permanecem estáticos no demo, como RabbitMQ.
3. Cada serviço recebe um token Vault separado com policy de leitura apenas para seu próprio path.
4. O token é entregue por arquivo montado, e não por variável de ambiente da aplicação.
5. O building block `DistributedCommerce.Secrets` busca os segredos antes da composição das dependências.
6. PostgreSQL não usa mais senha de aplicação estática no KV: Orders, Inventory e Payments recebem credenciais temporárias do Database Secrets Engine.
7. Os valores carregados do Vault substituem as credenciais vazias do ambiente do container.

Paths atuais:

- `secret/data/platform/orders`
- `secret/data/platform/inventory`
- `secret/data/platform/payments`
- `secret/data/platform/notifications`

## Produção

O modo `server -dev`, o root token de bootstrap e os tokens locais de 24 horas **não são configuração de produção**.

Em Kubernetes, a preferência é autenticar workloads por identidade da própria plataforma, usando Kubernetes Auth / Vault Secrets Operator ou mecanismo equivalente. AppRole permanece uma alternativa quando não existe um autenticador de plataforma adequado.

Policies devem ser específicas por serviço e tokens devem ter vida curta e escopo mínimo.

As credenciais PostgreSQL já foram evoluídas para o Database Secrets Engine. TTL, lease renewal, revogação e roles de runtime estão documentados no [ADR-0007](0007-dynamic-postgresql-credentials.md).

## Consequências

Benefícios:

- reduz exposição direta de credenciais nos containers;
- demonstra least privilege por serviço;
- centraliza governança e rotação;
- aproxima o projeto de práticas reais de Platform Engineering / DevSecOps.

Trade-offs:

- adiciona dependência operacional crítica;
- disponibilidade do Vault passa a fazer parte do desenho da plataforma;
- bootstrap, unseal, HA, audit devices e disaster recovery exigem desenho próprio em produção.
