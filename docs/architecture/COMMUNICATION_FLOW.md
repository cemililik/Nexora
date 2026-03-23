# Nexora — Servis Iletisim Mimarisi

Bu belge, Nexora platformundaki tum bilesenler arasindaki iletisim akislarini detayli sekilde aciklar.

---

## 1. Genel Mimari Bakis

```mermaid
graph TB
    subgraph Browser["Tarayici (Browser)"]
        ADMIN["nexora-admin<br/>(React SPA)<br/>localhost:3001"]
        PORTAL["nexora-portal<br/>(Next.js SSR)<br/>localhost:3000"]
    end

    subgraph Docker["Docker Compose Network"]
        APISIX["APISIX Gateway<br/>:9080 → localhost:9080"]
        KC["Keycloak<br/>:8080 → localhost:8080"]
        API["Nexora API<br/>:5000 → localhost:5100"]
        DAPR["Dapr Sidecar<br/>:3500 (internal)"]
        PG["PostgreSQL<br/>:5432 → localhost:5433"]
        REDIS["Redis<br/>:6379 → localhost:6380"]
        KAFKA["Kafka<br/>:29092 → localhost:9092"]
        MINIO["MinIO<br/>:9000 → localhost:9000"]
        VAULT["HashiCorp Vault<br/>:8200 → localhost:8200"]
    end

    ADMIN -- "Auth (PKCE)" --> KC
    PORTAL -- "Auth (OAuth2)" --> KC
    ADMIN -- "API calls" --> APISIX
    PORTAL -- "API calls" --> APISIX
    APISIX -- "JWT ✓ → Proxy" --> API
    API --- DAPR
    DAPR --> REDIS
    DAPR --> KAFKA
    DAPR --> VAULT
    API --> PG
    API --> MINIO
    API --> KC

    style APISIX fill:#f59e0b,color:#000
    style API fill:#2563eb,color:#fff
```

---

## 2. Request Akis Ozeti

Tum API istekleri APISIX uzerinden gecer. Keycloak auth istekleri dogrudan tarayicidan Keycloak'a gider.

```mermaid
flowchart LR
    A[nexora-admin<br/>:3001] -->|"HTTP Bearer Token<br/>localhost:9080/api/v1/*"| GW[APISIX<br/>:9080]
    C[nexora-portal<br/>:3000] -->|"HTTP Bearer Token<br/>localhost:9080/api/v1/*"| GW
    GW -->|"JWT ✓  CORS ✓  Rate Limit ✓<br/>X-Correlation-Id ✓<br/>nexora-api:5000"| B[Nexora API<br/>:5000]
    B --> D[(PostgreSQL)]
    B --> E[Keycloak<br/>JWT validation]

    style GW fill:#f59e0b,color:#000
    style B fill:#2563eb,color:#fff
```

**APISIX'in sagladiklari:**

| Ozellik | Aciklama |
|---------|----------|
| JWT Validation | `openid-connect` plugin — Keycloak JWKS ile token dogrulama |
| CORS | `localhost:3000` ve `localhost:3001` originslerine izin verir |
| Rate Limiting | Genel API: 100 req/s, Health: 10 req/s, Localization: 50 req/s |
| Correlation ID | Her istege `X-Correlation-Id` header'i ekler, response'a da yansitir |
| Routing | Path-based routing, spesifik route'lar catch-all'dan once eslenir |

**Defense in depth:** JWT hem APISIX'te (signature + expiry) hem backend'de (claims + tenant context) dogrulanir. Gecersiz token'lar gateway'de reddedilir, backend'e ulasmaz.

---

## 3. Authentication Akislari

### 3.1 nexora-admin (Keycloak JS + PKCE)

Admin dashboard **public SPA** oldugu icin token'i memory'de tutar. `keycloak-js` kutuphanesi Authorization Code + PKCE flow kullanir.

```mermaid
sequenceDiagram
    participant U as Kullanici
    participant A as nexora-admin<br/>(localhost:3001)
    participant KC as Keycloak<br/>(localhost:8080)
    participant GW as APISIX<br/>(localhost:9080)
    participant API as Nexora API<br/>(:5000)

    U->>A: Sayfayi ac
    A->>A: useAuth() → createKeycloak()
    A->>KC: keycloak.init({ onLoad: 'login-required', pkceMethod: 'S256' })
    Note over A,KC: Redirect to Keycloak login page

    U->>KC: Email + Password gir
    KC->>KC: Dogrula + Authorization Code uret
    KC-->>A: Redirect back + code + code_verifier
    A->>KC: POST /token (code + code_verifier)
    KC-->>A: { access_token, refresh_token, id_token }

    Note over A: Token memory'de (Zustand store)<br/>localStorage'a YAZILMAZ

    A->>A: parseTokenClaims(access_token)
    Note over A: Claims: sub, tenant_id,<br/>org_id, permissions[]

    A->>GW: GET /api/v1/identity/users/me<br/>Authorization: Bearer {token}
    GW->>GW: CORS check ✓<br/>Rate limit check ✓<br/>X-Correlation-Id inject
    GW->>API: Proxy request
    API->>API: JWT validate → TenantMiddleware<br/>→ schema select
    API-->>GW: 200 UserDetailDto
    GW-->>A: 200 + X-Correlation-Id

    A->>A: setSession({ user, token, tenantId, permissions })
    A-->>U: Dashboard renderla

    Note over A,KC: Token suresi dolunca (5 dk)
    A->>KC: keycloak.updateToken(30)
    KC-->>A: Yeni access_token
    A->>A: setAuthToken(newToken) + updateToken(store)
```

**Onemli detaylar:**
- Keycloak client: `nexora-admin` (publicClient: true, PKCE zorunlu)
- Token suresi: 300 saniye (5 dakika)
- Refresh mekanizmasi: `onTokenExpired` callback
- Fallback: `/me` endpoint basarisiz olursa JWT claims'ten user olusturulur

### 3.2 nexora-portal (NextAuth.js v5 + Keycloak)

Portal **server-side rendered** oldugu icin token httpOnly cookie'de tutulur. NextAuth.js Keycloak provider kullanir.

```mermaid
sequenceDiagram
    participant U as Kullanici
    participant P as nexora-portal<br/>(localhost:3000)
    participant NA as NextAuth.js<br/>(server-side)
    participant KC as Keycloak<br/>(localhost:8080)
    participant GW as APISIX<br/>(localhost:9080)
    participant API as Nexora API<br/>(:5000)

    U->>P: Sayfayi ac
    P->>NA: Session kontrol et
    NA-->>P: Session yok → login'e yonlendir
    P->>KC: OAuth2 Authorization Code flow
    U->>KC: Email + Password gir
    KC-->>P: Redirect + code
    P->>NA: Callback handler
    NA->>KC: POST /token (code + client_secret)
    KC-->>NA: { access_token, refresh_token }

    Note over NA: Token httpOnly cookie'de<br/>(JWT session strategy)

    NA-->>P: Session olusturuldu
    P->>GW: GET /api/v1/...<br/>Authorization: Bearer {token}
    GW->>API: Proxy
    API-->>GW: Response
    GW-->>P: Response + X-Correlation-Id

    Note over NA,KC: Token expired → session callback
    NA->>KC: POST /token (refresh_token + client_secret)
    KC-->>NA: Yeni access_token
```

**Onemli detaylar:**
- Keycloak client: `nexora-portal` (confidential, client_secret gerekli)
- Client secret: `nexora-portal-dev-secret`
- Token storage: httpOnly cookie (tarayici JS'i erisemez)
- Server-side token refresh: NextAuth session callback'inde

### 3.3 Admin vs Portal Auth Karsilastirmasi

| Ozellik | nexora-admin | nexora-portal |
|---------|-------------|---------------|
| Framework | React SPA (Vite) | Next.js SSR |
| Auth kutuphanesi | keycloak-js | NextAuth.js v5 |
| OAuth2 flow | Authorization Code + PKCE | Authorization Code + client_secret |
| Token storage | Memory (Zustand) | httpOnly cookie |
| Client type | Public | Confidential |
| Token refresh | Client-side (keycloak.updateToken) | Server-side (session callback) |
| XSS riski | Token memory'de, XSS ile calinamaz | Token cookie'de, JS erisemez |

---

## 4. APISIX Gateway Detayi

### 4.1 Calisma Modu

APISIX **standalone mode** ile calisir — etcd'ye bagimliligi yoktur. Route tanimlari `apisix.yaml` dosyasindan yuklenir ve dosya degisikliklerinde hot-reload yapilir.

```text
infrastructure/apisix/
├── config.yaml      # APISIX core config (standalone mode, plugin list)
└── apisix.yaml      # Route tanimlari (hot-reloaded)
```

### 4.2 Tanimli Route'lar

```mermaid
flowchart LR
    subgraph APISIX["APISIX Gateway (:9080)"]
        R1["id:1 /health<br/>🔓 Public<br/>Rate: 10/s"]
        R2["id:2 /api/v1/localization/*<br/>🔓 Public + CORS<br/>Rate: 50/s"]
        R3["id:3 /openapi/*<br/>🔓 Public"]
        R4["id:4 /admin/hangfire*<br/>🔒 Dev only"]
        R5["id:100 /api/v1/*<br/>🔐 JWT + CORS + Rate + CorrelationId<br/>Rate: 100/s"]
    end

    R1 --> UP["nexora-api:5000<br/>(Docker internal)"]
    R2 --> UP
    R3 --> UP
    R4 --> UP
    R5 --> UP

    style R5 fill:#2563eb,color:#fff
    style R1 fill:#16a34a,color:#fff
    style R2 fill:#16a34a,color:#fff
```

### 4.3 Request Islem Sirasi

```mermaid
sequenceDiagram
    participant FE as Frontend
    participant GW as APISIX<br/>(:9080)
    participant API as Nexora API<br/>(:5000)

    FE->>GW: GET /api/v1/contacts/contacts<br/>Origin: http://localhost:3001<br/>Authorization: Bearer {jwt}

    Note over GW: 1. Route match: /api/v1/* (id:100)
    GW->>GW: 2. CORS plugin<br/>Origin allowed? ✓ localhost:3001
    GW->>GW: 3. openid-connect plugin<br/>JWT signature ✓ expiry ✓ issuer ✓
    GW->>GW: 4. limit-req plugin<br/>Rate limit check (100/s) ✓
    GW->>GW: 5. request-id plugin<br/>X-Correlation-Id: uuid inject

    GW->>API: Proxy request<br/>+ X-Correlation-Id header<br/>+ Original Authorization header

    Note over API: ASP.NET Middleware Pipeline
    API->>API: JWT Authentication ✓
    API->>API: TenantMiddleware → schema select
    API->>API: MediatR → Handler → DB

    API-->>GW: 200 OK { data: ... }
    GW-->>FE: 200 OK<br/>Access-Control-Allow-Origin: http://localhost:3001<br/>X-Correlation-Id: uuid
```

### 4.4 CORS Preflight Akisi

Tarayici cross-origin isteklerden once OPTIONS preflight gonderir. APISIX bunu backend'e proxy etmeden dogrudan cevaplar.

```mermaid
sequenceDiagram
    participant FE as Frontend<br/>(localhost:3001)
    participant GW as APISIX<br/>(localhost:9080)
    participant API as Nexora API

    FE->>GW: OPTIONS /api/v1/contacts/contacts<br/>Origin: http://localhost:3001<br/>Access-Control-Request-Method: GET<br/>Access-Control-Request-Headers: Authorization

    GW->>GW: CORS plugin handles preflight
    GW-->>FE: 200 OK<br/>Access-Control-Allow-Origin: http://localhost:3001<br/>Access-Control-Allow-Methods: GET,POST,PUT,...<br/>Access-Control-Allow-Headers: Authorization,...<br/>Access-Control-Max-Age: 3600

    Note over API: Backend'e istek GITMEZ<br/>Preflight APISIX'te cevaplanir

    FE->>GW: GET /api/v1/contacts/contacts<br/>Authorization: Bearer {jwt}
    GW->>API: Proxy
    API-->>GW: 200
    GW-->>FE: 200 + CORS headers
```

---

## 5. Backend Middleware Pipeline

### 5.1 Basarili Request

```mermaid
sequenceDiagram
    participant GW as APISIX
    participant API as Nexora API
    participant MW as Middleware Pipeline
    participant MOD as Module Handler
    participant DB as PostgreSQL

    GW->>API: GET /api/v1/contacts/contacts?page=1<br/>Authorization: Bearer {jwt}<br/>Accept-Language: tr<br/>X-Correlation-Id: abc-123

    Note over API,MW: ASP.NET Middleware Pipeline

    API->>MW: 1. GlobalExceptionHandler
    MW->>MW: 2. CorrelationId Middleware<br/>(X-Correlation-Id from APISIX)
    MW->>MW: 3. JWT Authentication<br/>(token dogrulama)
    MW->>MW: 4. TenantMiddleware<br/>(JWT'den tenant_id → schema sec)
    MW->>MW: 5. LanguageMiddleware<br/>(Accept-Language → CultureInfo)

    MW->>MOD: MediatR → GetContactsQuery
    MOD->>DB: SELECT FROM<br/>"tenant_0000...0001"."contacts_contacts"
    DB-->>MOD: Rows
    MOD-->>API: Result<PagedResult<ContactListDto>>

    API-->>GW: 200 OK<br/>{ data: { items: [...], totalCount: 42 }, message: "lockey_..." }
```

### 5.2 TenantMiddleware Detayi

```mermaid
flowchart TD
    REQ[Incoming Request] --> CHECK{Path kontrol}
    CHECK -->|"/health, /admin/hangfire"| SKIP[Middleware'i atla]
    CHECK -->|Diger pathler| AUTH{Authenticated?}
    AUTH -->|Hayir| PASS[Devam et<br/>anonim endpoint olabilir]
    AUTH -->|Evet| CLAIM{JWT'de tenant_id<br/>claim var mi?}
    CLAIM -->|Hayir| ERR401[401 Unauthorized<br/>Missing tenant context]
    CLAIM -->|Evet| SET[TenantContextAccessor.SetTenant<br/>SchemaName = tenant_GUID]
    SET --> NEXT[Sonraki middleware]

    style ERR401 fill:#dc2626,color:#fff
    style SET fill:#16a34a,color:#fff
```

### 5.3 Schema-per-Tenant Veri Izolasyonu

```mermaid
graph TB
    subgraph PostgreSQL
        subgraph public["public schema"]
            PT[platform_tenants]
            PM[platform_tenant_modules]
            HF[hangfire_*]
        end
        subgraph T1["tenant_0000...0001 schema"]
            IU1[identity_users]
            IR1[identity_roles]
            CC1[contacts_contacts]
            DD1[documents_documents]
            NT1[notifications_templates]
        end
        subgraph T2["tenant_0000...0002 schema"]
            IU2[identity_users]
            IR2[identity_roles]
            CC2[contacts_contacts]
            DD2[documents_documents]
            NT2[notifications_templates]
        end
    end

    GW[APISIX] --> API[Nexora API]
    API -->|"tenant_id=0001"| T1
    API -->|"tenant_id=0002"| T2
    API --> public

    style T1 fill:#dbeafe
    style T2 fill:#fef3c7
    style GW fill:#f59e0b,color:#000
```

---

## 6. Dapr Sidecar Iletisimi

Nexora API, harici servislere Dapr sidecar uzerinden erisir. Sidecar, API container'in network namespace'ini paylasir.

```mermaid
flowchart LR
    subgraph API Container Network
        API["Nexora API<br/>:5000"]
        DAPR["Dapr Sidecar<br/>:3500 HTTP<br/>:50001 gRPC"]
    end

    API -->|"HTTP localhost:3500"| DAPR

    DAPR -->|"State Store"| REDIS[(Redis)]
    DAPR -->|"Pub/Sub"| KAFKA[Kafka]
    DAPR -->|"Secret Store"| VAULT[HashiCorp Vault]

    style DAPR fill:#7c3aed,color:#fff
```

| Bilesen | Dapr Component | Kullanim |
|---------|---------------|----------|
| State Store | Redis | Cache (L2), distributed state |
| Pub/Sub | Kafka | Cross-module integration events |
| Secret Store | HashiCorp Vault | API key'ler, Keycloak admin credentials |

**Onemli:** Dapr sidecar, API container ile ayni network namespace'i paylasir (`network_mode: "service:nexora-api"`). API restart olunca sidecar da recreate edilmelidir.

---

## 7. Observability Akisi

```mermaid
flowchart LR
    GW[APISIX] -->|"X-Correlation-Id"| API[Nexora API]
    API -->|"OTLP gRPC :4317"| OTEL[OTel Collector]
    OTEL -->|"Traces"| TEMPO[Tempo]
    OTEL -->|"Metrics"| PROM[Prometheus]
    API -->|"Logs (stdout)"| LOKI[Loki]
    GW -->|"Prometheus :9091"| PROM

    GRAFANA[Grafana<br/>localhost:3300] --> TEMPO
    GRAFANA --> LOKI
    GRAFANA --> PROM

    style GRAFANA fill:#f59e0b,color:#000
    style OTEL fill:#4f46e5,color:#fff
```

---

## 8. Tam Port Haritasi

| Servis | Internal Port | External Port | Erisim | Aciklama |
|--------|:---:|:---:|--------|----------|
| nexora-admin | — | 3001 | Tarayici | Vite dev server |
| nexora-portal | — | 3000 | Tarayici | Next.js dev server |
| **APISIX** | **9080** | **9080** | **Tarayici → API** | **Tum API trafigi buradan gecer** |
| Nexora API | 5000 | 5100 | Docker internal | Dogrudan erisim sadece debug icin |
| Keycloak | 8080 | 8080 | Tarayici + Docker | Auth (login, token) |
| PostgreSQL | 5432 | 5433 | Docker + dev tools | Database |
| Redis | 6379 | 6380 | Docker + dev tools | Cache |
| Kafka | 29092 | 9092 | Docker + dev tools | Event bus |
| MinIO | 9000/9001 | 9000/9001 | Docker + console | Object storage |
| Vault | 8200 | 8200 | Docker + UI | Secrets |
| Grafana | 3300 | 3300 | Tarayici | Observability dashboard |
| APISIX Metrics | 9091 | 9091 | Prometheus | Gateway metrikleri |
| Dapr Sidecar | 3500 | — | Sadece API container | Service mesh |
| Dapr Placement | 50006 | 50006 | Docker internal | Actor placement |
| OTel Collector | 4317 | 4327 | Docker internal | Telemetry |

```mermaid
graph LR
    subgraph External["Kullanici Erisimi"]
        P3000["localhost:3000<br/>Portal"]
        P3001["localhost:3001<br/>Admin"]
        P9080["localhost:9080<br/>APISIX ⭐"]
        P8080["localhost:8080<br/>Keycloak"]
        P3300["localhost:3300<br/>Grafana"]
    end

    subgraph Debug["Debug / Dev Tools"]
        P5100["localhost:5100<br/>API (direct)"]
        P5433["localhost:5433<br/>PostgreSQL"]
        P9001["localhost:9001<br/>MinIO Console"]
        P8200["localhost:8200<br/>Vault UI"]
    end

    subgraph Internal["Sadece Docker Network"]
        I5000["nexora-api:5000"]
        I6379["redis:6379"]
        I29092["kafka:29092"]
        I3500["dapr:3500"]
    end

    style External fill:#dbeafe
    style Debug fill:#fef3c7
    style Internal fill:#fee2e2
```

---

## 9. Konfigrasyon Dosyalari

### Frontend Environment Variables

**nexora-admin** (`.env.local`):

```bash
VITE_API_BASE_URL=http://localhost:9080/api/v1    # APISIX uzerinden
VITE_KEYCLOAK_URL=http://localhost:8080            # Dogrudan Keycloak
VITE_KEYCLOAK_REALM=nexora-dev
VITE_KEYCLOAK_CLIENT_ID=nexora-admin
```

**nexora-portal** (`.env.local`):

```bash
NEXT_PUBLIC_API_URL=http://localhost:9080/api/v1   # APISIX uzerinden
AUTH_KEYCLOAK_ISSUER=http://localhost:8080/realms/nexora-dev  # Dogrudan Keycloak
AUTH_KEYCLOAK_ID=nexora-portal
AUTH_KEYCLOAK_SECRET=nexora-portal-dev-secret
```

### APISIX Konfigurasyonu

**Calisma modu:** Standalone (etcd bagimliligi yok)

```text
infrastructure/apisix/
├── config.yaml     # deployment.role: data_plane, config_provider: yaml
└── apisix.yaml     # Route tanimlari, hot-reload destekli
```

### Keycloak Clients

| Client ID | Tip | Kullanim |
|-----------|-----|----------|
| `nexora-admin` | Public (PKCE) | Admin SPA auth |
| `nexora-portal` | Confidential | Portal SSR auth |
| `nexora-api` | Confidential + Service Account | Backend-to-Keycloak admin calls |
| `nexora-gateway` | Confidential | APISIX openid-connect plugin (OIDC discovery) |

### Keycloak Hostname Konfigurasyonu

Keycloak, Docker icinden ve disarindan farkli hostname'lerle erisilir. Issuer mismatch'i onlemek icin:

```bash
KC_HOSTNAME=http://localhost:8080            # Frontend issuer (JWT iss claim)
KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true         # Backchannel URL'ler request hostname'den
```

Bu sayede:
- **Browser** → discovery'de issuer: `http://localhost:8080/...` ✓
- **APISIX** → discovery'de issuer: `http://localhost:8080/...` (ayni, JWT ile eslesiyor) ✓
- **APISIX** → jwks_uri: `http://keycloak:8080/...` (Docker'dan erisilebilir) ✓

---

## 10. Production'a Gecis Icin Gerekenler

Development ortaminda JWT validation, CORS, rate limiting, correlation ID injection tamamiyla aktif. Production'da ek olarak:

| Gorev | Aciklama |
|-------|----------|
| TLS termination | APISIX'te SSL sertifikasi, backend HTTP kalabilir |
| Keycloak hostname | `KC_HOSTNAME=https://auth.nexora.io` (gercek domain) |
| etcd mode (opsiyonel) | Standalone yerine etcd-backed, Admin API ile dynamic config |
| Stricter CORS | Sadece production domain'lerine izin (localhost kaldirilir) |
| Rate limit tuning | IP-based → consumer-based rate limiting |

---

## 11. Sik Sorulan Sorular

**S: Neden auth istekleri APISIX'ten gecmiyor?**
Keycloak auth flow'u (login, token exchange) tarayici ile Keycloak arasinda dogrudan gerceklesir. Bu OAuth2 standardinin geregine uygundur — auth provider'a dogrudan erisim gereklidir. APISIX sadece **API isteklerini** proxy eder.

**S: JWT iki kez mi dogrulaniyor?**
Evet, defense in depth prensibi. APISIX signature + expiry dogruluyor, backend ise claims (tenant_id, permissions) cikariyor. Gecersiz token'lar gateway'de reddedilir — backend'e ulasmaz.

**S: Neden Dapr sidecar ayri container?**
Dapr sidecar pattern'i geregi, her uygulama kendi sidecar'ina sahiptir. `network_mode: "service:nexora-api"` ile API container'in network namespace'ini paylasir — boylece API `localhost:3500`'e istek atarak Dapr'a erisir.

**S: Frontend neden `localhost:5100`'e degil de `localhost:9080`'e gidiyor?**
Tum API trafigi APISIX uzerinden gecmeli cunku APISIX CORS, rate limiting, ve correlation ID injection saglar. `localhost:5100` sadece debug/troubleshooting icin aciktir.
