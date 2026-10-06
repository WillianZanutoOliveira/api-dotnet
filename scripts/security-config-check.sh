#!/usr/bin/env bash
set -euo pipefail

realm="deploy/keycloak/distributed-commerce-realm.json"

jq -e '
  .bruteForceProtected == true
  and .failureFactor <= 5
  and .accessTokenLifespan <= 300
  and .defaultSignatureAlgorithm == "RS256"
  and .registrationAllowed == false
  and all(.clients[]; (.fullScopeAllowed // false) == false)
  and any(.scopeMappings[]?; .client == "commerce-cli" and (.roles | index("customer")) != null and (.roles | index("admin")) != null)
' "$realm" >/dev/null

if grep -R --include='appsettings*.json' -nE   '(Password=postgres|"Username"[[:space:]]*:[[:space:]]*"guest"|"Password"[[:space:]]*:[[:space:]]*"guest")'   src/Services; then
  echo "Default development credentials must not exist in application settings." >&2
  exit 1
fi

if grep -R --include='*.cs' -nE   '(AllowAnyOrigin|SetIsOriginAllowed\([^)]*true|EnsureCreatedAsync)'   src; then
  echo "An insecure CORS or runtime schema-creation pattern was detected." >&2
  exit 1
fi

for dockerfile in   src/Gateway/ApiGateway/Dockerfile   src/Platform/DatabaseMigrator/Dockerfile   src/Services/Orders/Orders.Api/Dockerfile   src/Services/Inventory/Inventory.Service/Dockerfile   src/Services/Payments/Payments.Service/Dockerfile   src/Services/Notifications/Notifications.Service/Dockerfile; do
  grep -Fq 'USER $APP_UID' "$dockerfile"
done

grep -Fq 'RequireSignedTokens = true' src/BuildingBlocks/Security/KeycloakAuthenticationExtensions.cs
grep -Fq 'RequireExpirationTime = true' src/BuildingBlocks/Security/KeycloakAuthenticationExtensions.cs
grep -Fq 'ValidAlgorithms = [SecurityAlgorithms.RsaSha256]' src/BuildingBlocks/Security/KeycloakAuthenticationExtensions.cs
grep -Fq 'must use HTTPS outside Development' src/BuildingBlocks/Security/KeycloakAuthenticationExtensions.cs

echo "Static security invariants passed."
