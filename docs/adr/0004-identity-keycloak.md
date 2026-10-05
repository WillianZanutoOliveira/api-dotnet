[🇺🇸 English](0004-identity-keycloak.en.md)

# ADR-0004 — Identidade e autorização com Keycloak

## Status

Aceito.

## Contexto

A Orders API era pública e recebia `customerId` diretamente do payload. Isso simplificava a demonstração inicial, mas não representava uma fronteira de segurança real: um cliente poderia tentar criar ou consultar recursos em nome de outro usuário.

## Decisão

Usar Keycloak como Identity Provider OpenID Connect e validar access tokens JWT na borda HTTP da Orders API.

O building block `Security` centraliza:

- validação de assinatura, emissor, audiência e expiração;
- mapeamento de `preferred_username`;
- roles do realm em uma claim plana `roles`;
- policies de autorização para leitura, escrita e administração.

O identificador do cliente de negócio passa a vir da claim `sub`, e não do corpo da requisição.

Leitura de pedido aplica autorização no nível do objeto:

- `customer` lê apenas pedidos cujo `CustomerId` corresponde ao próprio `sub`;
- `admin` pode consultar pedidos de qualquer cliente.

## Ambiente local

O Docker Compose sobe Keycloak em modo de desenvolvimento e importa um realm versionado para tornar a demonstração reproduzível.

O fluxo de senha direta existe **somente** para os usuários de smoke test locais. Clientes interativos reais devem usar Authorization Code + PKCE.

## Consequências

Benefícios:

- autenticação e autorização passam a fazer parte do desenho arquitetural;
- evita confiar em identidade fornecida pelo payload;
- RBAC e object-level authorization ficam demonstráveis;
- a configuração de identidade é reproduzível em CI.

Trade-offs:

- Keycloak adiciona uma dependência operacional;
- o modo `start-dev` e os usuários demo não representam configuração de produção;
- produção exige TLS, gestão de segredos, política de rotação e configuração endurecida do IdP.
