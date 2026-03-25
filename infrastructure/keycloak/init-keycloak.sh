#!/bin/sh
set -e

apk add --no-cache curl > /dev/null 2>&1

KC_URL="http://keycloak:8080"
KC_ADMIN="admin"
KC_ADMIN_PASS="admin"
REALM="nexora-dev"

echo "Waiting for Keycloak to be ready..."
until curl -sf "$KC_URL/realms/master" > /dev/null 2>&1; do
  sleep 2
done
echo "Keycloak is ready."

# ── Get admin token ──
get_token() {
  curl -s "$KC_URL/realms/master/protocol/openid-connect/token" \
    -d "client_id=admin-cli" \
    -d "username=$KC_ADMIN" \
    -d "password=$KC_ADMIN_PASS" \
    -d "grant_type=password" | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])"
}

TOKEN=$(get_token)

# ── Create realm (idempotent) ──
echo "Creating realm: $REALM"
REALM_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  "$KC_URL/admin/realms/$REALM" \
  -H "Authorization: Bearer $TOKEN")

if [ "$REALM_STATUS" = "200" ]; then
  echo " -> Already exists, skipping."
else
  curl -s -o /dev/null -w " -> HTTP %{http_code}\n" \
    "$KC_URL/admin/realms" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{
      "realm": "'"$REALM"'",
      "enabled": true,
      "displayName": "Nexora Development",
      "registrationAllowed": false,
      "loginWithEmailAllowed": true,
      "duplicateEmailsAllowed": false,
      "resetPasswordAllowed": true,
      "editUsernameAllowed": false,
      "bruteForceProtected": true,
      "accessTokenLifespan": 300,
      "ssoSessionIdleTimeout": 1800,
      "ssoSessionMaxLifespan": 36000
    }'
fi

TOKEN=$(get_token)

# ── Helper: create client (idempotent) ──
create_client() {
  CLIENT_ID=$1
  CLIENT_JSON=$2
  EXISTS=$(curl -s "$KC_URL/admin/realms/$REALM/clients?clientId=$CLIENT_ID" \
    -H "Authorization: Bearer $TOKEN" | python3 -c "import sys,json; print(len(json.load(sys.stdin)))")
  if [ "$EXISTS" != "0" ]; then
    echo " -> $CLIENT_ID already exists, skipping."
  else
    curl -s -o /dev/null -w " -> $CLIENT_ID: HTTP %{http_code}\n" \
      "$KC_URL/admin/realms/$REALM/clients" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "$CLIENT_JSON"
  fi
}

# ── Create clients ──
echo "Creating clients..."

create_client "nexora-admin" '{
  "clientId":"nexora-admin","name":"Nexora Admin Dashboard","enabled":true,
  "publicClient":true,"directAccessGrantsEnabled":false,"standardFlowEnabled":true,
  "implicitFlowEnabled":false,"serviceAccountsEnabled":false,"protocol":"openid-connect",
  "rootUrl":"http://localhost:3001","baseUrl":"/",
  "redirectUris":["http://localhost:3001/*"],
  "webOrigins":["http://localhost:3001"],
  "attributes":{"pkce.code.challenge.method":"S256","post.logout.redirect.uris":"http://localhost:3001/*"}
}'

create_client "nexora-portal" '{
  "clientId":"nexora-portal","name":"Nexora Public Portal","enabled":true,
  "publicClient":false,"directAccessGrantsEnabled":false,"standardFlowEnabled":true,
  "implicitFlowEnabled":false,"serviceAccountsEnabled":false,"protocol":"openid-connect",
  "secret":"nexora-portal-dev-secret",
  "rootUrl":"http://localhost:3000","baseUrl":"/",
  "redirectUris":["http://localhost:3000/*"],
  "webOrigins":["http://localhost:3000"],
  "attributes":{"pkce.code.challenge.method":"S256","post.logout.redirect.uris":"http://localhost:3000/*"}
}'

create_client "nexora-api" '{
  "clientId":"nexora-api","name":"Nexora API","enabled":true,
  "publicClient":false,"directAccessGrantsEnabled":true,"standardFlowEnabled":false,
  "implicitFlowEnabled":false,"serviceAccountsEnabled":true,"protocol":"openid-connect",
  "secret":"nexora-api-dev-secret"
}'

create_client "nexora-gateway" '{
  "clientId":"nexora-gateway","name":"APISIX Gateway","enabled":true,
  "publicClient":false,"directAccessGrantsEnabled":false,"standardFlowEnabled":false,
  "implicitFlowEnabled":false,"serviceAccountsEnabled":false,"protocol":"openid-connect",
  "secret":"nexora-gateway-dev-secret"
}'

TOKEN=$(get_token)

# ── Add JWT claim mappers to each client (upsert: delete existing then create) ──
add_mapper() {
  CLIENT_INTERNAL_ID=$1
  MAPPER_JSON=$2
  MAPPER_NAME=$(echo "$MAPPER_JSON" | python3 -c "import sys,json; print(json.load(sys.stdin)['name'])")

  # Find existing mapper by name and delete it (idempotent upsert)
  EXISTING_ID=$(curl -s "$KC_URL/admin/realms/$REALM/clients/$CLIENT_INTERNAL_ID/protocol-mappers/models" \
    -H "Authorization: Bearer $TOKEN" | python3 -c "
import sys, json
mappers = json.load(sys.stdin)
for m in mappers:
    if m.get('name') == '$MAPPER_NAME':
        print(m['id'])
        break
" 2>/dev/null || true)

  if [ -n "$EXISTING_ID" ]; then
    curl -s -o /dev/null -X DELETE "$KC_URL/admin/realms/$REALM/clients/$CLIENT_INTERNAL_ID/protocol-mappers/models/$EXISTING_ID" \
      -H "Authorization: Bearer $TOKEN" 2>/dev/null || true
  fi

  # Also remove legacy "org_id" mapper if adding "organization_id"
  if [ "$MAPPER_NAME" = "organization_id" ]; then
    LEGACY_ID=$(curl -s "$KC_URL/admin/realms/$REALM/clients/$CLIENT_INTERNAL_ID/protocol-mappers/models" \
      -H "Authorization: Bearer $TOKEN" | python3 -c "
import sys, json
mappers = json.load(sys.stdin)
for m in mappers:
    if m.get('name') == 'org_id':
        print(m['id'])
        break
" 2>/dev/null || true)
    if [ -n "$LEGACY_ID" ]; then
      curl -s -o /dev/null -X DELETE "$KC_URL/admin/realms/$REALM/clients/$CLIENT_INTERNAL_ID/protocol-mappers/models/$LEGACY_ID" \
        -H "Authorization: Bearer $TOKEN" 2>/dev/null || true
      echo "   Removed legacy org_id mapper from $MAPPER_NAME"
    fi
  fi

  MAPPER_HTTP=$(curl -s -o /tmp/mapper_resp.json -w "%{http_code}" \
    -X POST "$KC_URL/admin/realms/$REALM/clients/$CLIENT_INTERNAL_ID/protocol-mappers/models" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "$MAPPER_JSON")
  if [ "${MAPPER_HTTP#2}" = "$MAPPER_HTTP" ]; then
    echo "ERROR: Failed to create mapper $MAPPER_NAME (HTTP $MAPPER_HTTP)"
    cat /tmp/mapper_resp.json 2>/dev/null
    exit 1
  fi
}

add_all_mappers() {
  CLIENT_ID_NAME=$1
  CID=$(curl -s "$KC_URL/admin/realms/$REALM/clients?clientId=$CLIENT_ID_NAME" \
    -H "Authorization: Bearer $TOKEN" | python3 -c "import sys,json; print(json.load(sys.stdin)[0]['id'])")

  echo "Adding JWT mappers to $CLIENT_ID_NAME"

  add_mapper "$CID" '{"name":"tenant_id","protocol":"openid-connect","protocolMapper":"oidc-hardcoded-claim-mapper","config":{"claim.name":"tenant_id","claim.value":"00000000-0000-0000-0000-000000000001","jsonType.label":"String","id.token.claim":"true","access.token.claim":"true","userinfo.token.claim":"true"}}'
  add_mapper "$CID" '{"name":"organization_id","protocol":"openid-connect","protocolMapper":"oidc-usermodel-attribute-mapper","config":{"user.attribute":"org_id","claim.name":"organization_id","jsonType.label":"String","id.token.claim":"true","access.token.claim":"true","userinfo.token.claim":"true"}}'
  add_mapper "$CID" '{"name":"organizations","protocol":"openid-connect","protocolMapper":"oidc-usermodel-attribute-mapper","config":{"user.attribute":"organizations","claim.name":"organizations","jsonType.label":"String","multivalued":"true","id.token.claim":"true","access.token.claim":"true","userinfo.token.claim":"true"}}'
  add_mapper "$CID" '{"name":"permissions","protocol":"openid-connect","protocolMapper":"oidc-usermodel-attribute-mapper","config":{"user.attribute":"permissions","claim.name":"permissions","jsonType.label":"String","multivalued":"true","id.token.claim":"true","access.token.claim":"true","userinfo.token.claim":"true"}}'
}

add_all_mappers "nexora-admin"
add_all_mappers "nexora-portal"
add_all_mappers "nexora-api"

# ── Configure User Profile (Keycloak 26 requires attributes to be declared) ──
echo "Configuring User Profile for custom attributes..."
curl -s "$KC_URL/admin/realms/$REALM/users/profile" \
  -H "Authorization: Bearer $TOKEN" | python3 -c "
import sys, json

profile = json.load(sys.stdin)

existing_names = {a['name'] for a in profile.get('attributes', [])}

custom_attrs = [
    {'name': 'org_id', 'displayName': 'Organization ID',
     'permissions': {'view': ['admin', 'user'], 'edit': ['admin']}, 'multivalued': False},
    {'name': 'organizations', 'displayName': 'Organizations',
     'permissions': {'view': ['admin', 'user'], 'edit': ['admin']}, 'multivalued': True},
    {'name': 'permissions', 'displayName': 'Permissions',
     'permissions': {'view': ['admin'], 'edit': ['admin']}, 'multivalued': True}
]

for attr in custom_attrs:
    if attr['name'] not in existing_names:
        profile['attributes'].append(attr)

profile['unmanagedAttributePolicy'] = 'ADMIN_EDIT'

print(json.dumps(profile))
" > /tmp/profile.json

curl -s -o /dev/null -w " -> User Profile: HTTP %{http_code}\n" \
  -X PUT "$KC_URL/admin/realms/$REALM/users/profile" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @/tmp/profile.json

TOKEN=$(get_token)

# ── Create test admin user (idempotent) ──
echo "Creating test admin user..."
USER_EXISTS=$(curl -s "$KC_URL/admin/realms/$REALM/users?username=admin@nexora.dev" \
  -H "Authorization: Bearer $TOKEN" | python3 -c "import sys,json; print(len(json.load(sys.stdin)))")

if [ "$USER_EXISTS" != "0" ]; then
  echo " -> User admin@nexora.dev already exists."
  USER_ID=$(curl -s "$KC_URL/admin/realms/$REALM/users?username=admin@nexora.dev" \
    -H "Authorization: Bearer $TOKEN" | python3 -c "import sys,json; print(json.load(sys.stdin)[0]['id'])")
else
  curl -s -o /dev/null "$KC_URL/admin/realms/$REALM/users" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{
      "username":"admin@nexora.dev",
      "email":"admin@nexora.dev",
      "firstName":"Platform",
      "lastName":"Admin",
      "enabled":true,
      "emailVerified":true,
      "credentials":[{"type":"password","value":"Admin123!","temporary":false}]
    }'
  USER_ID=$(curl -s "$KC_URL/admin/realms/$REALM/users?username=admin@nexora.dev" \
    -H "Authorization: Bearer $TOKEN" | python3 -c "import sys,json; print(json.load(sys.stdin)[0]['id'])")
  echo " -> Created user: $USER_ID"
fi

# Set user attributes (separate PUT — Keycloak 26 requires user profile to be configured first)
echo "Setting user attributes..."
cat > /tmp/user-attrs.json << 'USEREOF'
{
  "firstName":"Platform",
  "lastName":"Admin",
  "email":"admin@nexora.dev",
  "emailVerified":true,
  "enabled":true,
  "attributes":{
    "org_id":["00000000-0000-0000-0000-000000000001"],
    "organizations":["00000000-0000-0000-0000-000000000001"],
    "permissions":[
      "identity.tenants.read","identity.tenants.create","identity.tenants.update","identity.tenants.delete",
      "identity.organizations.read","identity.organizations.create","identity.organizations.update","identity.organizations.delete",
      "identity.users.read","identity.users.create","identity.users.update","identity.users.delete",
      "identity.roles.read","identity.roles.create","identity.roles.update","identity.roles.delete",
      "identity.modules.read","identity.modules.manage",
      "contacts.contact.read","contacts.contact.create","contacts.contact.update","contacts.contact.delete",
      "contacts.tag.read","contacts.tag.create","contacts.tag.update","contacts.tag.delete",
      "contacts.custom-field.read","contacts.custom-field.manage",
      "contacts.note.create","contacts.note.update","contacts.note.read","contacts.note.delete",
      "contacts.relationship.create","contacts.relationship.delete",
      "contacts.import.execute","contacts.export.execute",
      "contacts.gdpr.export","contacts.gdpr.delete","contacts.merge.execute",
      "documents.document.read","documents.document.upload","documents.document.update","documents.document.delete",
      "documents.folder.read","documents.folder.manage",
      "documents.signature.read","documents.signature.create","documents.signature.manage",
      "documents.template.read","documents.template.manage",
      "notifications.notification.read","notifications.notification.send",
      "notifications.template.read","notifications.template.manage",
      "notifications.provider.read","notifications.provider.manage",
      "notifications.schedule.read","notifications.schedule.manage",
      "reporting.definition.read","reporting.definition.manage",
      "reporting.execution.run","reporting.execution.read",
      "reporting.schedule.manage",
      "reporting.dashboard.read","reporting.dashboard.manage"
    ]
  }
}
USEREOF

# Inject user ID into the JSON
python3 -c "
import json, sys
with open('/tmp/user-attrs.json') as f:
    data = json.load(f)
data['id'] = '$USER_ID'
print(json.dumps(data))
" > /tmp/user-update.json

curl -s -o /dev/null -w " -> Attributes: HTTP %{http_code}\n" \
  -X PUT "$KC_URL/admin/realms/$REALM/users/$USER_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @/tmp/user-update.json

# Cleanup
rm -f /tmp/profile.json /tmp/user-attrs.json /tmp/user-update.json

echo ""
echo "============================================"
echo "  Keycloak initialization complete!"
echo "============================================"
echo ""
echo "  Realm:       $REALM"
echo "  Clients:     nexora-admin (public, PKCE)"
echo "               nexora-portal (confidential)"
echo "               nexora-api (confidential, service account)"
echo "  Test user:   admin@nexora.dev / Admin123!"
echo "  Permissions: 63 (all modules)"
echo ""
echo "  Admin Console: http://localhost:8080/admin"
echo "  Account:       http://localhost:8080/realms/$REALM/account"
echo ""
