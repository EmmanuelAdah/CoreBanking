# Core Banking System (.NET 8 / Clean Architecture)

Production-oriented skeleton of a core banking platform with:

- **Paystack** payment integration
- **Kafka** for asynchronous transaction processing
- **Transactional Outbox** pattern for reliable webhook handling
- Webhook endpoint that returns **HTTP 202 Accepted**
- Rule-based **Fraud Detection**
- **Dispute resolution** & **Refunds**
- **Rate limiting** (AspNetCoreRateLimit)
- **Idempotency** support on payment endpoints (`Idempotency-Key` header)
- **Email notification** service (MailKit)
- **Loan management** with **credit score validation** before approval
- Docker Compose + `.env` secrets
- GitHub Actions CI

> Note: Target framework is **net8.0** (stable). The architecture is ready to move to .NET 9/10 when the SDK is available in your environment.

## Solution Structure

```
CoreBanking/
├── CoreBanking.Api/              # Web API, controllers, middleware, DI
├── CoreBanking.Application/      # Services, DTOs, interfaces
├── CoreBanking.Domain/           # Entities, enums, repository contracts
├── CoreBanking.Infrastructure/   # EF Core, Paystack, Kafka, Outbox, Email
├── docker-compose.yml
├── Dockerfile
├── .env.example
└── .github/workflows/ci.yml
```

## Quick Start

### 1. Prerequisites
- .NET 8 SDK
- Docker & Docker Compose

### 2. Configuration

```bash
cp .env.example .env
# Edit .env with your Paystack keys, SMTP, etc.
```

### 3. Run with Docker Compose

```bash
docker compose up -d --build
```

API will be available at `http://localhost:8080`  
Swagger: `http://localhost:8080/swagger`

### 4. Local development (without full Docker)

```bash
# Start only infrastructure
docker compose up -d postgres kafka zookeeper

# Run API
cd CoreBanking.Api
dotnet run
```

## Key Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/accounts` | Create account |
| GET  | `/api/v1/accounts/{accountNumber}` | Get account |
| POST | `/api/v1/payments/initialize` | Initiate Paystack payment (supports `Idempotency-Key`) |
| POST | `/api/v1/payments/webhook` | Paystack webhook → **202 Accepted** |
| GET  | `/api/v1/payments/{reference}` | Get transaction |
| POST | `/api/v1/payments/refund` | Refund transaction |
| POST | `/api/v1/payments/disputes` | Open dispute |
| POST | `/api/v1/loans/apply` | Apply for loan (credit score checked) |
| GET  | `/api/v1/loans/{id}` | Get loan |
| POST | `/api/v1/loans/{id}/disburse` | Disburse approved loan |
| GET  | `/health` | Health check |


## Authentication & Security (JWT)

The API is secured with **JWT Bearer** authentication and **role-based authorization**.

### Roles
| Role | Capabilities |
|------|----------------|
| **Customer** | Initiate payments, view transactions, apply for loans, open disputes |
| **Officer** | Everything Customer can do + create accounts, refunds, disburse loans |
| **Admin** | Full access (same as Officer in this skeleton) |

### Auth endpoints
| Method | Path | Auth |
|--------|------|------|
| POST | `/api/v1/auth/register` | Anonymous (Customer only outside Development) |
| POST | `/api/v1/auth/login` | Anonymous |
| POST | `/api/v1/auth/refresh` | Anonymous (refresh token) |
| GET  | `/api/v1/auth/me` | Bearer token |
| POST | `/api/v1/auth/change-password` | Bearer token |
| POST | `/api/v1/auth/logout` | Bearer token |

### Using Swagger
1. Call `POST /api/v1/auth/register` or `login`
2. Copy the `accessToken`
3. Click **Authorize** in Swagger and enter: `Bearer <accessToken>`
4. Call protected endpoints

### Example login
```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"YourPassword123!"}'
```

Then:
```bash
curl http://localhost:8080/api/v1/accounts/ACC123 \
  -H "Authorization: Bearer <accessToken>"
```

### Security notes
- Passwords hashed with **PBKDF2** (100k iterations, SHA-256)
- Access tokens expire (default 30 min) – use refresh tokens
- Refresh tokens stored hashed in DB and revoked on logout / password change
- Webhook endpoint is **anonymous** (secure with Paystack signature in production)
- Rate limiting applied on auth and payment endpoints
- Set a strong `JWT_SECRET` (≥ 32 characters) in `.env`


## Architecture Highlights

### Transactional Outbox + Kafka
1. Webhook is received → payload is written to `OutboxMessages` table **in the same DB transaction**.
2. API immediately returns **202 Accepted**.
3. `OutboxProcessor` (BackgroundService) polls the outbox, processes the event, and publishes to Kafka topics (`payment.webhooks`, `transactions.completed`).
4. Failed messages are retried with exponential backoff.

### Idempotency
Send header `Idempotency-Key: <unique-value>` on `/payments/initialize`. Duplicate requests return the original result.

### Fraud Detection
Simple rule engine (high-value threshold). Easily extensible to velocity checks or ML models.

### Credit Score Gate
Before a loan is approved, `CreditScoreService` is consulted. Scores < 550 (or high amount + low score) are rejected automatically.

## Environment Variables

See `.env.example` for the full list. Critical ones:

- `DATABASE_URL`
- `PAYSTACK_SECRET_KEY`
- `KAFKA_BOOTSTRAP_SERVERS`
- `SMTP_*`

## CI

GitHub Actions workflow:
- Restore → Build → Test (with Postgres service)
- Docker image build on `main`

## Next Steps / Production Hardening

- Add Paystack signature verification on the webhook
- Implement proper EF migrations (`dotnet ef migrations add Initial`)
- Add authentication (JWT / OAuth2)
- Replace mock credit score with real bureau API
- Add distributed locking / Redis for rate limiting & idempotency at scale
- Observability (OpenTelemetry, Serilog + Seq/ELK)
- Unit & integration tests

---

Built as a clean, extensible foundation for a real core banking platform.