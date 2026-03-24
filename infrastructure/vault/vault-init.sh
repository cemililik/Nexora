#!/bin/sh
# Vault initialization script for development
# Waits for Vault to be ready, initializes, unseals, and seeds secrets

set -e

VAULT_ADDR="http://127.0.0.1:8200"
export VAULT_ADDR

echo "Waiting for Vault to start..."
until vault status 2>/dev/null | grep -q "Sealed"; do
  sleep 1
done

# Check if already initialized
if vault status 2>/dev/null | grep -q "Initialized.*true"; then
  echo "Vault already initialized, attempting unseal..."
  if [ -f /vault/data/init-keys.json ]; then
    UNSEAL_KEY=$(cat /vault/data/init-keys.json | grep -o '"unseal_keys_b64":\["[^"]*"' | cut -d'"' -f4)
    if ! vault operator unseal "$UNSEAL_KEY"; then
      echo "ERROR: Vault unseal failed"
      exit 1
    fi
    export VAULT_TOKEN=$(cat /vault/data/init-keys.json | grep -o '"root_token":"[^"]*"' | cut -d'"' -f4)
  fi
else
  echo "Initializing Vault..."
  vault operator init -key-shares=1 -key-threshold=1 -format=json > /vault/data/init-keys.json

  UNSEAL_KEY=$(cat /vault/data/init-keys.json | grep -o '"unseal_keys_b64":\["[^"]*"' | cut -d'"' -f4)
  export VAULT_TOKEN=$(cat /vault/data/init-keys.json | grep -o '"root_token":"[^"]*"' | cut -d'"' -f4)

  echo "Unsealing Vault..."
  vault operator unseal "$UNSEAL_KEY"

  echo "Enabling KV v2 secrets engine..."
  vault secrets enable -path=secret kv-v2

  echo "Seeding development secrets..."
  vault kv put secret/nexora/postgres \
    connection-string="Host=postgres;Port=5432;Database=nexora;Username=nexora;Password=nexora_dev"

  vault kv put secret/nexora/redis \
    connection-string="redis:6379"

  vault kv put secret/nexora/keycloak \
    base-url="http://keycloak:8080" \
    admin-username="admin" \
    admin-password="admin"

  vault kv put secret/nexora/minio \
    endpoint="minio:9000" \
    access-key="nexora" \
    secret-key="nexora_dev"

  vault kv put secret/nexora/kafka \
    bootstrap-servers="kafka:29092"

  # Create a policy for the Nexora application
  vault policy write nexora-app - <<EOF
path "secret/data/nexora/*" {
  capabilities = ["read", "list"]
}
EOF

  # Create an AppRole for Dapr
  vault auth enable approle
  vault write auth/approle/role/nexora-dapr \
    token_policies="nexora-app" \
    token_ttl=1h \
    token_max_ttl=4h

  echo "Vault initialization complete."
fi

echo "Vault is ready."
