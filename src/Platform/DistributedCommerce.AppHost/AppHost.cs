using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../.."));
var vaultScript = Path.Combine(repositoryRoot, "deploy", "vault", "vault-init.sh");
var keycloakRealmDirectory = Path.Combine(repositoryRoot, "deploy", "keycloak");
var tokenRoot = Path.Combine(repositoryRoot, ".aspire", "vault-tokens");

var tokenDirectories = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["orders"] = Path.Combine(tokenRoot, "orders"),
    ["inventory"] = Path.Combine(tokenRoot, "inventory"),
    ["payments"] = Path.Combine(tokenRoot, "payments"),
    ["notifications"] = Path.Combine(tokenRoot, "notifications")
};

foreach (var directory in tokenDirectories.Values)
    Directory.CreateDirectory(directory);

var vaultRootToken = CreateGeneratedSecret(builder, "vault-dev-root-token");
var ordersDbPassword = CreateGeneratedSecret(builder, "orders-db-password");
var inventoryDbPassword = CreateGeneratedSecret(builder, "inventory-db-password");
var paymentsDbPassword = CreateGeneratedSecret(builder, "payments-db-password");
var rabbitMqPassword = CreateGeneratedSecret(builder, "rabbitmq-password");
var keycloakAdminPassword = CreateGeneratedSecret(builder, "keycloak-admin-password");

var ordersDb = AddPostgres(
    builder,
    "orders-db",
    "orders",
    ordersDbPassword,
    port: 5432,
    Path.Combine(repositoryRoot, "deploy", "postgres", "orders-init.sql"));

var inventoryDb = AddPostgres(
    builder,
    "inventory-db",
    "inventory",
    inventoryDbPassword,
    port: 5433,
    Path.Combine(repositoryRoot, "deploy", "postgres", "inventory-init.sql"));

var paymentsDb = AddPostgres(
    builder,
    "payments-db",
    "payments",
    paymentsDbPassword,
    port: 5434,
    Path.Combine(repositoryRoot, "deploy", "postgres", "payments-init.sql"));

var rabbitMq = builder
    .AddContainer("rabbitmq", "rabbitmq", "4-management")
    .WithEnvironment("RABBITMQ_DEFAULT_USER", "platform")
    .WithEnvironment("RABBITMQ_DEFAULT_PASS", rabbitMqPassword)
    .WithEndpoint(port: 5672, targetPort: 5672, name: "amqp")
    .WithHttpEndpoint(port: 15672, targetPort: 15672, name: "management");

var keycloak = builder
    .AddContainer("keycloak", "quay.io/keycloak/keycloak", "26.8.0")
    .WithArgs("start-dev", "--import-realm")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithBindMount(
        keycloakRealmDirectory,
        "/opt/keycloak/data/import",
        isReadOnly: true)
    .WithHttpEndpoint(port: 8180, targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/realms/distributed-commerce/.well-known/openid-configuration")
    .WithOtlpExporter();

var vault = builder
    .AddContainer("vault", "hashicorp/vault", "1.21.4")
    .WithArgs("server", "-dev")
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", vaultRootToken)
    .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
    .WithHttpEndpoint(port: 8200, targetPort: 8200, name: "http")
    .WithHttpHealthCheck("/v1/sys/health");

var vaultInit = builder
    .AddContainer("vault-init", "hashicorp/vault", "1.21.4")
    .WithEntrypoint("/bin/sh")
    .WithArgs("/bootstrap/vault-init.sh")
#pragma warning disable S5332 // Vault server-dev is intentionally HTTP-only inside the isolated local network.
    .WithEnvironment("VAULT_ADDR", "http://vault:8200")
#pragma warning restore S5332
    .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", vaultRootToken)
    .WithEnvironment("ORDERS_POSTGRES_USER", "postgres")
    .WithEnvironment("ORDERS_POSTGRES_PASSWORD", ordersDbPassword)
    .WithEnvironment("INVENTORY_POSTGRES_USER", "postgres")
    .WithEnvironment("INVENTORY_POSTGRES_PASSWORD", inventoryDbPassword)
    .WithEnvironment("PAYMENTS_POSTGRES_USER", "postgres")
    .WithEnvironment("PAYMENTS_POSTGRES_PASSWORD", paymentsDbPassword)
    .WithEnvironment("RABBITMQ_DEFAULT_USER", "platform")
    .WithEnvironment("RABBITMQ_DEFAULT_PASS", rabbitMqPassword)
    .WithBindMount(vaultScript, "/bootstrap/vault-init.sh", isReadOnly: true)
    .WithBindMount(tokenDirectories["orders"], "/tokens/orders")
    .WithBindMount(tokenDirectories["inventory"], "/tokens/inventory")
    .WithBindMount(tokenDirectories["payments"], "/tokens/payments")
    .WithBindMount(tokenDirectories["notifications"], "/tokens/notifications")
    .WaitFor(vault)
    .WaitFor(ordersDb)
    .WaitFor(inventoryDb)
    .WaitFor(paymentsDb);

var ordersApi = builder
    .AddProject(
        "orders-api",
        Path.Combine(repositoryRoot, "src", "Services", "Orders", "Orders.Api", "Orders.Api.csproj"))
    .WithHttpEndpoint(port: 8081, targetPort: 8081, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("RabbitMq__Host", "localhost")
    .WithEnvironment("RabbitMq__Username", "")
    .WithEnvironment("RabbitMq__Password", "")
    .WithEnvironment("Keycloak__Authority", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__MetadataAddress", "http://localhost:8180/realms/distributed-commerce/.well-known/openid-configuration")
    .WithEnvironment("Keycloak__Issuer", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__Audience", "distributed-commerce-api")
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("ConnectionStrings__orders-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["orders"], "token"))
    .WithEnvironment("Vault__SecretPath", "platform/orders")
    .WithEnvironment("Vault__DatabaseRole", "orders-app")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "orders-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5432")
    .WithEnvironment("Vault__DatabaseName", "orders")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "orders_runtime")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitFor(rabbitMq)
    .WaitFor(keycloak);

builder
    .AddProject(
        "inventory-service",
        Path.Combine(repositoryRoot, "src", "Services", "Inventory", "Inventory.Service", "Inventory.Service.csproj"))
    .WithHttpEndpoint(port: 8082, targetPort: 8082, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("RabbitMq__Host", "localhost")
    .WithEnvironment("RabbitMq__Username", "")
    .WithEnvironment("RabbitMq__Password", "")
    .WithEnvironment("ConnectionStrings__inventory-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["inventory"], "token"))
    .WithEnvironment("Vault__SecretPath", "platform/inventory")
    .WithEnvironment("Vault__DatabaseRole", "inventory-app")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "inventory-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5433")
    .WithEnvironment("Vault__DatabaseName", "inventory")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "inventory_runtime")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitFor(rabbitMq);

builder
    .AddProject(
        "payments-service",
        Path.Combine(repositoryRoot, "src", "Services", "Payments", "Payments.Service", "Payments.Service.csproj"))
    .WithHttpEndpoint(port: 8083, targetPort: 8083, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("RabbitMq__Host", "localhost")
    .WithEnvironment("RabbitMq__Username", "")
    .WithEnvironment("RabbitMq__Password", "")
    .WithEnvironment("ConnectionStrings__payments-db", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["payments"], "token"))
    .WithEnvironment("Vault__SecretPath", "platform/payments")
    .WithEnvironment("Vault__DatabaseRole", "payments-app")
    .WithEnvironment("Vault__DatabaseConnectionStringName", "payments-db")
    .WithEnvironment("Vault__DatabaseHost", "localhost")
    .WithEnvironment("Vault__DatabasePort", "5434")
    .WithEnvironment("Vault__DatabaseName", "payments")
    .WithEnvironment("Vault__DatabaseRuntimeRole", "payments_runtime")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitFor(rabbitMq);

builder
    .AddProject(
        "notifications-service",
        Path.Combine(repositoryRoot, "src", "Services", "Notifications", "Notifications.Service", "Notifications.Service.csproj"))
    .WithHttpEndpoint(port: 8084, targetPort: 8084, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("RabbitMq__Host", "localhost")
    .WithEnvironment("RabbitMq__Username", "")
    .WithEnvironment("RabbitMq__Password", "")
    .WithEnvironment("Vault__Address", "http://localhost:8200")
    .WithEnvironment("Vault__TokenFile", Path.Combine(tokenDirectories["notifications"], "token"))
    .WithEnvironment("Vault__SecretPath", "platform/notifications")
    .WithOtlpExporter()
    .WaitForCompletion(vaultInit)
    .WaitFor(rabbitMq);

builder
    .AddProject(
        "api-gateway",
        Path.Combine(repositoryRoot, "src", "Gateway", "ApiGateway", "ApiGateway.csproj"))
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http", env: "ASPNETCORE_HTTP_PORTS", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Keycloak__Authority", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__MetadataAddress", "http://localhost:8180/realms/distributed-commerce/.well-known/openid-configuration")
    .WithEnvironment("Keycloak__Issuer", "http://localhost:8180/realms/distributed-commerce")
    .WithEnvironment("Keycloak__Audience", "distributed-commerce-api")
    .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
    .WithEnvironment("ReverseProxy__Clusters__orders-cluster__Destinations__primary__Address", "http://localhost:8081/")
    .WithOtlpExporter()
    .WaitFor(keycloak)
    .WaitFor(ordersApi);

await builder.Build().RunAsync();

static IResourceBuilder<ParameterResource> CreateGeneratedSecret(
    IDistributedApplicationBuilder builder,
    string name)
{
    return builder.AddParameter(
        name,
        new GenerateParameterDefault { MinLength = 32 },
        secret: true,
        persist: true);
}

static IResourceBuilder<ContainerResource> AddPostgres(
    IDistributedApplicationBuilder builder,
    string resourceName,
    string databaseName,
    IResourceBuilder<ParameterResource> password,
    int port,
    string initScript)
{
    return builder
        .AddContainer(resourceName, "postgres", "18-alpine")
        .WithEnvironment("POSTGRES_DB", databaseName)
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", password)
        .WithEndpoint(port: port, targetPort: 5432, name: "postgres")
        .WithBindMount(
            initScript,
            "/docker-entrypoint-initdb.d/10-runtime-role.sql",
            isReadOnly: true);
}
