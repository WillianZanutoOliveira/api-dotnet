[🇺🇸 English](0004-identity-keycloak.en.md)

# ADR-0004 — Identidade e autorização com Keycloak

## Status

Aceito.

## Contexto

A primeira versão da Orders API era pública e recebia `customerId` diretamente do payload. Isso não representava uma fronteira de segurança real: um cliente poderia tentar criar ou consultar recursos em nome de outro usuário.

Com a introdução do YARP Gateway, a identidade também não poderia ficar confiada apenas à borda. Um serviço interno deve continuar verificando a identidade recebida.

## Decisão

Usar Keycloak como Identity Provider OpenID Connect/OAuth 2.0 e aplicar **dupla validação JWT**:

```text
Cliente
  |
  v
Keycloak
  |
  | JWT
  v
YARP Gateway
  |
  | JWT validado + rate limiting
  v
Orders API
  |
  | JWT validado novamente
  v
caso de uso
```

O building block `Security` centraliza:

- assinatura obrigatória;
- algoritmo RS256 explicitamente aceito;
- issuer;
- audience;
- lifetime;
- claim `exp` obrigatória;
- `preferred_username`;
- realm roles em claim plana `roles`;
- policies de leitura, escrita e administração;
- detalhes de falha de autenticação não expostos ao cliente.

Fora de `Development`, Authority, Issuer e MetadataAddress precisam usar HTTPS e `RequireHttpsMetadata` não pode ser desligado.

## Identidade de negócio

O identificador do cliente vem da claim `sub`, nunca do payload.

JSON desconhecido também é rejeitado pela API. Um payload tentando injetar `customerId` é inválido em vez de ser silenciosamente aceito.

## Object-level authorization

Leituras de pedido têm dois caminhos explícitos:

- `admin` usa consulta transversal por `OrderId`;
- `customer` usa consulta PostgreSQL por `OrderId + CustomerId`.

Assim, o caso comum não carrega primeiro o recurso de outro cliente para só depois decidir em memória se pode acessá-lo.

Pedidos fora do escopo do customer retornam `404`, reduzindo resource-existence disclosure.

## Proteção contra abuso

O Gateway usa token bucket particionado por `sub`.

O realm local habilita brute-force protection e usa access tokens curtos.

O CI verifica:

- request sem token → `401`;
- token adulterado → `401`;
- cross-customer read → `404`;
- admin cross-customer → sucesso;
- burst acima do limite → `429`.

## Ambiente local

Docker Compose/Aspire sobem Keycloak em modo de desenvolvimento e importam um realm versionado.

O fluxo Resource Owner Password Credentials em `commerce-cli` existe **somente** para smoke test/DAST descartável.

Clientes interativos reais devem usar Authorization Code + PKCE.

O ambiente local usa HTTP apenas para loopback. O modo de produção da aplicação rejeita metadata Keycloak sem HTTPS.

## Produção

Keycloak deve executar em modo `start`, com TLS e hostname explícito.

Quando possível, Admin Console/API devem ficar em hostname ou rede administrativa separada da superfície pública do IdP.

Produção não deve importar os usuários/senhas demo nem tratar o realm local como configuração final.

## Consequências

Benefícios:

- identidade deixa de vir do payload;
- defesa em profundidade entre Gateway e serviço;
- RBAC e BOLA controls verificáveis;
- resource existence não é exposta para outros customers;
- configuração insegura de metadata falha no startup de produção;
- autenticação/autorização são reproduzíveis em CI e DAST.

Trade-offs:

- Keycloak adiciona dependência operacional;
- o fixture local de password grant não é um fluxo para produção;
- disponibilidade do IdP/JWKS passa a fazer parte do desenho operacional.

Veja também [postura de segurança e pentest readiness](../security-posture.md).
