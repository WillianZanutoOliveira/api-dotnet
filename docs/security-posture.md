[🇺🇸 English](security-posture.en.md)

# Postura de segurança e pentest readiness

## Objetivo

A plataforma não declara ser "invulnerável". Nenhum sistema distribuído sério consegue garantir que um pentest futuro encontrará zero falhas.

O objetivo verificável deste repositório é mais forte e auditável:

> nenhuma mudança deve introduzir findings conhecidos High/Critical nos gates automatizados, enfraquecer limites de autorização, reintroduzir credenciais padrão ou remover controles de least privilege.

O baseline se apoia no **OWASP ASVS 5.0.0**, no **OWASP API Security Top 10 2023** e em práticas atuais de ASP.NET Core, Keycloak, Vault, containers e Kubernetes.

## Trust boundaries

```text
Internet / client
      |
      | untrusted
      v
TLS ingress
      |
      v
Keycloak -------- JWT --------+
      |                        |
      |                        v
      +--------------------> YARP Gateway
                                |
                                | authenticated / rate limited
                                v
                            Orders API
                                |
                 +--------------+--------------+
                 |                             |
                 v                             v
             PostgreSQL                    RabbitMQ
                 ^                             |
                 |                             v
              Vault                  Inventory / Payments /
                 |                      Notifications
                 |
          migration identity
                 |
                 v
          DatabaseMigrator
```

Cada seta que cruza um boundary exige autenticação, autorização, policy ou configuração explícita. Nenhum componente interno é considerado confiável somente por estar "dentro da rede".

## Controles HTTP/API

O building block `ServiceDefaults` aplica:

- remoção do header `Server`;
- limite de body em 1 MiB;
- limite de request line;
- limite de quantidade/tamanho total de headers;
- timeout para recebimento dos headers;
- HSTS fora de Development;
- `X-Content-Type-Options: nosniff`;
- `X-Frame-Options: DENY`;
- `Referrer-Policy: no-referrer`;
- Content Security Policy restritiva;
- Permissions Policy restritiva;
- bloqueio explícito de TRACE e CONNECT;
- `Cache-Control: no-store` em requests autenticadas.

Orders adicionalmente:

- rejeita JSON com propriedades desconhecidas;
- limita profundidade JSON;
- limita número de itens;
- limita tamanho de SKU;
- limita quantidade e preço unitário;
- não aceita `CustomerId` do payload.

## Autenticação

JWTs são validados no Gateway e novamente em Orders e Customers.

Validações:

- assinatura obrigatória;
- algoritmo RS256 explicitamente aceito;
- issuer;
- audience;
- lifetime;
- `exp` obrigatório;
- metadata HTTPS obrigatória fora de Development;
- error details de autenticação desabilitados;
- token não é salvo pelo middleware.

O realm local também habilita proteção contra brute force e access tokens curtos.

O password grant de `commerce-cli` existe **somente para smoke/DAST local**. Produção deve usar Authorization Code + PKCE para clientes interativos.

Keycloak em produção deve usar `start`, TLS e hostname explícito; Admin Console/API deve ficar em hostname/rede administrativa separada quando possível.

## Autorização / BOLA

A criação de pedidos deriva `CustomerId` da claim `sub`.

Para leitura:

- admin pode consultar transversalmente;
- customer usa query SQL escopada por `OrderId + CustomerId`;
- pedido de outro customer resulta em `404`, evitando revelar existência do recurso.

Isso reduz a chance de regressão de Broken Object Level Authorization.

## Cadastro PF/PJ e proteção de dados pessoais

Customers possui PostgreSQL exclusivo, exige role `admin` em **todas** as rotas `/api/customers` e revalida JWT no serviço. CPF/CNPJ são normalizados e validados sem chamadas externas; apenas o CEP é enviado à BrasilAPI para completar endereço. Índice único evita duplicidade de documentos, e entradas inválidas geram respostas de validação. O domínio exige entre 1 e 10 endereços e exatamente um principal.

A função de consulta de CEP usa saída HTTPS fixa para BrasilAPI e não aceita hosts ou URLs arbitrários fornecidos pelo cliente. Falhas de upstream retornam indisponibilidade ao consumidor; coordenadas geográficas, quando presentes, são estimativas por CEP, não localização exata. A API de cadastro ainda não é integrada automaticamente à identidade do Keycloak. Consulte [ADR-0013](./adr/0013-customers-pf-pj-brasilapi-cep.md).

## Rate limiting e resource consumption

YARP usa token bucket particionado por `sub`.

Baseline:

- 60 tokens;
- refill de 60/minuto;
- sem queue;
- rejeição HTTP 429;
- `Retry-After`.

O CI envia tráfego suficiente para confirmar que a proteção realmente bloqueia bursts; também executa `scripts/customers-smoke.sh` para validar o CRUD PF/PJ e o bloqueio a usuários sem role `admin`.

O DAST usa um limite elevado em uma stack descartável para não impedir o scanner de explorar a API; o teste funcional de rate limiting continua separado.

## Segredos e dados

Não existem credenciais PostgreSQL/RabbitMQ default em `appsettings.json`.

Ausência de credencial gera falha de startup.

No perfil seguro:

- Vault KV v2 fornece secrets não-database;
- Database Secrets Engine gera logins PostgreSQL temporários;
- tokens são separados por workload;
- connection strings efetivos não ficam no environment;
- runtime database roles são DML-only;
- migrations usam identidade Vault/role PostgreSQL distinta com DDL;
- falha de lease renewal é fail closed.

## Containers

Workloads .NET:

- executam como non-root;
- dropam Linux capabilities no perfil seguro;
- usam `no-new-privileges`;
- usam root filesystem read-only no perfil seguro;
- recebem somente `/tmp` efêmero gravável.

Portas publicadas pelo Docker Compose local são vinculadas a `127.0.0.1`, evitando exposição acidental na LAN do desenvolvedor.

## Kubernetes

Os exemplos mantêm:

- `runAsNonRoot`;
- seccomp `RuntimeDefault`;
- `allowPrivilegeEscalation: false`;
- capabilities removidas;
- probes separadas de readiness/liveness;
- Vault Kubernetes Auth separado entre runtime e migrator no modelo GitOps.

Produção deve adicionar NetworkPolicies compatíveis com o CNI/arquitetura real e TLS/mTLS onde a plataforma exigir.

## Security automation

### Em todo PR

- build com warnings-as-errors;
- SonarAnalyzer + `dotnet format`;
- architecture tests;
- contract compatibility tests;
- Testcontainers;
- chaos/fault injection;
- static security invariant check;
- authentication/authorization smoke;
- adversarial HTTP security smoke;
- CodeQL `security-extended`;
- Gitleaks sobre histórico Git;
- Trivy vulnerabilities + secrets;
- Trivy IaC misconfiguration scan;
- SPDX SBOM.

### DAST

`.github/workflows/dast.yml` sobe uma stack local descartável e executa **OWASP ZAP API Scan ativo autenticado** através do YARP Gateway usando o OpenAPI de Orders.

O scan gera relatórios HTML/JSON/Markdown como artifacts.

Exceções ZAP versionadas só podem representar diferenças conscientes do fixture local, como HSTS testado no boundary TLS de produção e ausência de CSRF em API Bearer-token sem cookie auth.

### Periódico / supply chain

- OpenSSF Scorecard;
- Dependabot;
- k6 performance baseline;
- OCI image provenance attestations;
- Trivy;
- SBOM.

## OWASP API Security Top 10 — cobertura

| Risco | Controles do projeto |
| --- | --- |
| API1 Broken Object Level Authorization | query escopada por customer, admin explícito, cross-customer smoke |
| API2 Broken Authentication | Keycloak, dupla validação JWT, RS256, issuer/audience/lifetime, HTTPS obrigatório em produção |
| API3 Broken Object Property Level Authorization | DTO dedicado, CustomerId vindo do token, propriedades JSON desconhecidas rejeitadas |
| API4 Unrestricted Resource Consumption | rate limiting, request/body/header limits, bounded collections/números, performance baseline |
| API5 Broken Function Level Authorization | policies por role e autorização explícita |
| API6 Sensitive Business Flows | rate limiting por identidade e business constraints |
| API7 SSRF | API pública não aceita URL/host remoto controlado pelo usuário |
| API8 Security Misconfiguration | security headers, non-root, Trivy misconfig, fail-closed config, no default credentials |
| API9 Improper Inventory Management | OpenAPI versionado/testado, README/ADRs, endpoints de Development restritos por ambiente |
| API10 Unsafe Consumption of APIs | Service Defaults, HTTP resilience, configuração explícita de dependencies e sem URLs externas vindas do usuário |

## Critério para afirmar "security gate verde"

O repositório pode afirmar que o **security gate está verde** somente quando:

1. CI funcional está verde;
2. Security workflow está verde;
3. DAST ZAP está verde para o head relevante;
4. não há findings High/Critical conhecidos aceitos sem decisão documentada;
5. nenhuma exception de segurança foi escondida apenas para fazer o pipeline passar.

Isso não substitui pentest manual.

## Pentest manual

Um pentest independente continua recomendado antes de qualquer uso real com dados sensíveis, pagamento real ou exposição pública.

O pentest deve receber:

- arquitetura;
- OpenAPI;
- roles/personas;
- escopo autorizado;
- ambiente descartável;
- versão/commit exato;
- lista de findings automatizados conhecidos;
- regras para testes de carga/DoS.

Qualquer finding deve virar:

```text
finding
  -> reprodução automatizada quando possível
  -> correção
  -> regression test
  -> ADR/documentação quando houver trade-off
```

Essa é a garantia sustentável: **não prometer ausência absoluta de vulnerabilidades, e sim transformar cada classe conhecida de falha em um controle verificável e regressivo.**
