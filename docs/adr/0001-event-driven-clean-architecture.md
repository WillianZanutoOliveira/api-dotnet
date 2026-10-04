[🇺🇸 English](0001-event-driven-clean-architecture.en.md)

# ADR-0001: Serviços orientados a eventos com Clean Architecture

- **Status:** Aceito
- **Data:** 2026-10-03

## Contexto

O projeto precisa demonstrar limites entre serviços e preocupações de sistemas distribuídos sem acoplar as regras de negócio à tecnologia de transporte ou persistência.

## Decisão

Utilizar serviços implantáveis de forma independente, conectados por eventos de integração via RabbitMQ.

O bounded context de Orders usa camadas explícitas de Clean Architecture:

- Domain;
- Application;
- Infrastructure;
- API.

Consumers menores usam uma organização interna mais leve para evitar cerimônia que não agrega valor à complexidade atual.

## Consequências

### Positivas

- o código de domínio é independente de frameworks;
- a infraestrutura pode mudar sem alterar as regras de domínio;
- os limites entre serviços são explícitos;
- consumers assíncronos podem evoluir de forma independente;
- o projeto demonstra julgamento arquitetural em vez de aplicar camadas mecanicamente em todos os lugares.

### Trade-offs

- deployments distribuídos são operacionalmente mais complexos do que um monólito modular;
- os fluxos de negócio passam a ser eventualmente consistentes;
- debugging exige correlação e observabilidade.
