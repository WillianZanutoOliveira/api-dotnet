[🇺🇸 English](0013-customers-pf-pj-brasilapi-cep.en.md)

# ADR-0013 — Cadastro PF/PJ e consulta de CEP com BrasilAPI

## Status

Aceito.

## Contexto

A plataforma precisa de um bounded context de cadastro capaz de representar pessoa física e pessoa jurídica sem acoplar Orders a dados cadastrais nem compartilhar tabelas. O cadastro contém PII e precisa preservar as mesmas garantias de autenticação, segredos, migrations e database-per-service já adotadas na plataforma.

Endereços brasileiros se beneficiam de preenchimento por CEP, mas integrar diretamente um provedor ao domínio criaria acoplamento e poderia tornar testes e desenvolvimento dependentes de uma API externa.

## Decisão

Criar o bounded context **Customers** em Clean Architecture:

```text
Customers.Domain
      ↑
Customers.Application
      ↑
Customers.Infrastructure
      ↑
Customers.Api
```

O agregado `Customer` diferencia:

- `Individual`: CPF, nome, nascimento, e-mail, telefone;
- `Company`: CNPJ, nome fantasia, razão social, fundação, inscrições estadual/municipal, e-mail e telefone;
- 1 a 10 endereços, com exatamente um principal.

CPF e CNPJ são normalizados e validados localmente pelos dígitos verificadores. O documento possui índice único no banco. O tipo da pessoa é imutável após a criação.

O serviço possui PostgreSQL próprio, `customers_runtime` para DML e `customers_migrator` para DDL. Vault emite logins temporários independentes para runtime e migration.

As rotas de cadastro são protegidas por JWT e exigem role `admin`. O Gateway valida o token na borda e Customers valida novamente.

Para CEP, a aplicação define a porta `IPostalCodeLookup`; a infraestrutura implementa essa porta com **BrasilAPI CEP v2**. O adapter usa HTTPS, cliente HTTP resiliente, timeout, múltiplos campos de endereço, código IBGE e coordenadas opcionais.

CPF/CNPJ **não são enviados a serviços externos**.

## API

```text
POST   /api/customers
GET    /api/customers
GET    /api/customers/{id}
PUT    /api/customers/{id}
DELETE /api/customers/{id}
GET    /api/customers/address/cep/{cep}
```

A listagem é paginada e suporta busca textual, documento e tipo de pessoa.

## Consequências

### Positivas

- isolamento de PII em bounded context e banco próprios;
- validação determinística de CPF/CNPJ sem dependência externa;
- provedor de CEP substituível sem alterar domínio/aplicação;
- mesma postura Vault/migrations/least-privilege do restante da plataforma;
- CRUD protegido por RBAC e testado através do Gateway;
- CEP v2 pode enriquecer endereço com IBGE e geolocalização quando disponível.

### Trade-offs

- BrasilAPI é uma dependência externa e pode ficar indisponível;
- coordenadas de CEP não representam uma residência exata e podem estar ausentes ou imprecisas;
- exclusão física deve ser revisitada caso requisitos legais/auditoria exijam retenção ou anonimização;
- cadastro de clientes não é, neste momento, fonte automática de identidade do Keycloak.

## Validação

- testes de domínio para CPF/CNPJ e invariantes de endereço;
- testes do adapter BrasilAPI com fixtures e coordenadas ausentes;
- smoke CRUD PF/PJ autenticado;
- architecture tests;
- CodeQL, Trivy, secret scan e DAST autenticado do CRUD.
