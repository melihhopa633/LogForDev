# LogForDev

<p align="center">
  <strong>Open-source, self-hosted log management for developers</strong><br>
  One <code>docker-compose up</code>. Any language. Zero cost.
</p>

<p align="center">
  <a href="#why-logfordev">Why?</a> &middot;
  <a href="#quick-start">Quick Start</a> &middot;
  <a href="#how-it-works">How It Works</a> &middot;
  <a href="#api-reference">API</a> &middot;
  <a href="#examples">Examples</a> &middot;
  <a href="#configuration">Config</a> &middot;
  <a href="#architecture">Architecture</a> &middot;
  <a href="#contributing">Contributing</a>
</p>

---

## Why LogForDev?

Logging shouldn't cost thousands of dollars or take days to set up.

| | Datadog | Splunk | ELK Stack | Seq | Graylog | Grafana Loki | **LogForDev** |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Self-hosted** | - | - | Yes | Yes | Yes | Yes | **Yes** |
| **Free (no limits)** | - | - | Yes* | - | Yes* | Yes* | **Yes** |
| **Setup time** | Minutes | Hours | Days | Minutes | Hours | Hours | **Minutes** |
| **Containers needed** | - | - | 3-5 | 1 | 3 | 2+ | **2** |
| **Any language** | SDK | Agent | Beats | Serilog | GELF | Promtail | **HTTP** |
| **Full-text search** | Yes | Yes | Yes | Yes | Yes | - | **Yes** |
| **No vendor lock-in** | - | - | Yes | - | Yes | Yes | **Yes** |

<sub>* ELK: free but high ops burden. Seq: free tier limited to 1 user / 50 GB. Graylog: free tier limited to 1 node / 16 GB. Loki: label-only indexing, no full-text search.</sub>

**The gap LogForDev fills:** It sits between "expensive SaaS you can't afford" and "complex self-hosted stacks you can't maintain." If you want structured, searchable, multi-project log management with a single `docker-compose up` - this is it.

### Who is this for?

- Solo developers and indie hackers running side projects or SaaS apps
- Small startup teams (2-20 devs) who need real logging beyond `console.log`
- Backend / full-stack developers who want to add logging to any stack via a simple HTTP call
- Organizations with data privacy requirements who can't send logs to third-party clouds
- Microservice teams who need distributed tracing correlation without Jaeger + ELK + Grafana
- DevOps engineers tired of maintaining ELK clusters

---

## Quick Start

### 1. Start the server

```bash
git clone https://github.com/melihhopa633/LogForDev.git
cd LogForDev
docker-compose up -d
```

This starts **2 containers**: the LogForDev app + ClickHouse database.

### 2. Run the setup wizard

Open [http://localhost:5000](http://localhost:5000). The setup wizard will guide you through:
- Testing the ClickHouse connection
- Creating your admin account with **TOTP two-factor authentication**
- Generating your first API key

### 3. Send your first log

```bash
curl -X POST http://localhost:5000/api/logs \
  -H "Content-Type: application/json" \
  -H "X-API-Key: YOUR_API_KEY" \
  -d '{
    "level": "info",
    "message": "Hello from LogForDev!",
    "appName": "my-app"
  }'
```

That's it. Open the dashboard to see your log.

---

## How It Works

LogForDev is a plain **REST API**. There is no SDK, no agent, no special library. If your language can send an HTTP POST with JSON, it can send logs to LogForDev.

```
Your App  ──HTTP POST──>  LogForDev API  ──buffer──>  ClickHouse
                              |
                         Dashboard (MVC)
```

**Key design decisions:**
- **ClickHouse** as the storage engine - a columnar OLAP database designed for billions of rows and fast analytical queries
- **Async write buffer** - logs are queued in memory and flushed to ClickHouse in batches (100 logs / 1 second), reducing write pressure
- **Project isolation** - each project gets its own API key (`X-API-Key` header). Logs are tagged with project ID automatically
- **Dual authentication** - cookie-based auth for the dashboard, API key auth for log ingestion

---

## API Reference

All endpoints are under `/api/logs`. Log ingestion requires an API key in the `X-API-Key` header (or `?apiKey=` query param).

### Log Ingestion

#### `POST /api/logs` — Send a single log

```json
{
  "level": "error",
  "message": "Payment processing failed",
  "appName": "checkout-service",
  "environment": "production",
  "userId": "usr_8492",
  "source": "PaymentController.cs",
  "exceptionType": "PaymentException",
  "exceptionMessage": "Card declined by issuer",
  "exceptionStacktrace": "at PaymentService.Charge() ...",
  "requestMethod": "POST",
  "requestPath": "/api/payments",
  "statusCode": 500,
  "durationMs": 342.5,
  "traceId": "abc-123-def",
  "spanId": "span-456",
  "metadata": { "orderId": "ord_1234", "amount": 99.99 }
}
```

**Response:** `{ "success": true, "id": "550e8400-..." }`

#### `POST /api/logs/batch` — Send multiple logs

```json
{
  "logs": [
    { "level": "info", "message": "Request started", "appName": "api-gateway" },
    { "level": "info", "message": "Auth validated", "appName": "auth-service", "durationMs": 12.3 },
    { "level": "error", "message": "DB timeout", "appName": "user-service", "exceptionType": "TimeoutException" }
  ]
}
```

**Response:** `{ "success": true, "count": 3 }`

### Log Entry Fields

| Field | Type | Required | Description |
|-------|------|:--------:|-------------|
| `level` | string | Yes | `trace` `debug` `info` `warning` `error` `fatal` |
| `message` | string | Yes | The log message |
| `appName` | string | Yes | Application name (used for filtering) |
| `environment` | string | | `production` `staging` `development` (default: `production`) |
| `metadata` | object | | Any additional JSON data |
| `source` | string | | Source file, class, or module name |
| `userId` | string | | Associated user identifier |
| `traceId` | string | | Distributed trace ID for request correlation |
| `spanId` | string | | Span ID within a trace |
| `host` | string | | Server/host name (auto-detected from IP if omitted) |
| `exceptionType` | string | | Exception class name (e.g. `NullReferenceException`) |
| `exceptionMessage` | string | | Exception message text |
| `exceptionStacktrace` | string | | Full stack trace string |
| `requestMethod` | string | | HTTP method (`GET`, `POST`, etc.) |
| `requestPath` | string | | HTTP request path |
| `statusCode` | int | | HTTP response status code |
| `durationMs` | float | | Request duration in milliseconds |

### Querying

#### `GET /api/logs` — Query logs with filters

| Parameter | Type | Description |
|-----------|------|-------------|
| `appName` | string | Filter by application name |
| `level` | string | Filter by single level |
| `levels` | string | Filter by multiple levels (comma-separated) |
| `environment` | string | Filter by environment |
| `search` | string | Full-text search in message |
| `traceId` | string | Filter by trace ID |
| `projectId` | GUID | Filter by project |
| `exceptionType` | string | Filter by exception type |
| `source` | string | Filter by source |
| `userId` | string | Filter by user ID |
| `requestMethod` | string | Filter by HTTP method |
| `statusCodeMin` | int | Minimum status code |
| `statusCodeMax` | int | Maximum status code |
| `from` | datetime | Start of time range (ISO 8601) |
| `to` | datetime | End of time range (ISO 8601) |
| `page` | int | Page number (default: `1`) |
| `pageSize` | int | Results per page (default: `50`) |

**Response:**
```json
{
  "data": [ { "id": "...", "timestamp": "...", "level": 2, "message": "...", ... } ],
  "totalCount": 1250,
  "page": 1,
  "pageSize": 50,
  "totalPages": 25
}
```

#### `GET /api/logs/stats` — Dashboard statistics

Returns: `totalLogs`, `errorCount`, `warningCount`, `logsPerMinute`, `topApps`

#### `GET /api/logs/patterns` — Log pattern detection

Detects recurring log patterns automatically. Parameters: `level`, `levels`, `appName`, `hours` (default: 24), `minCount` (default: 2), `limit` (default: 50)

#### `GET /api/logs/trace/{traceId}` — Distributed trace timeline

Returns all logs for a given trace ID, ordered chronologically with offset timing and service list.

#### `GET /api/logs/apps` — List all application names

#### `GET /api/logs/environments` — List all environments

### Project Management (Dashboard only)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/logs/projects` | List all projects |
| `POST` | `/api/logs/projects` | Create project (`{ "name": "...", "expiryDays": 90 }`) |
| `PUT` | `/api/logs/projects/{id}` | Update project name |
| `DELETE` | `/api/logs/projects/{id}` | Delete project |
| `DELETE` | `/api/logs` | Delete logs (`?olderThanDays=30`) |

---

## Examples

LogForDev works with **any language** - no SDK or library to install. Just HTTP.

### cURL

```bash
curl -X POST http://localhost:5000/api/logs \
  -H "Content-Type: application/json" \
  -H "X-API-Key: lfdev_your_api_key" \
  -d '{"level":"info","message":"Deploy completed","appName":"ci-pipeline"}'
```

### Python

```python
import requests

LOGFORDEV = "http://localhost:5000/api/logs"
HEADERS = {"Content-Type": "application/json", "X-API-Key": "lfdev_your_api_key"}

def log(level, message, app, **extra):
    requests.post(LOGFORDEV, json={"level": level, "message": message, "appName": app, **extra}, headers=HEADERS)

log("info", "User registered", "auth-service", userId="usr_42")
log("error", "Connection refused", "db-worker", exceptionType="ConnectionError")
```

### JavaScript / Node.js

```javascript
const LOG_URL = "http://localhost:5000/api/logs";
const HEADERS = { "Content-Type": "application/json", "X-API-Key": "lfdev_your_api_key" };

async function log(level, message, appName, extra = {}) {
  await fetch(LOG_URL, {
    method: "POST",
    headers: HEADERS,
    body: JSON.stringify({ level, message, appName, ...extra }),
  });
}

await log("info", "Server started on port 3000", "express-api");
await log("error", "Unhandled rejection", "express-api", {
  exceptionType: "TypeError",
  exceptionMessage: "Cannot read properties of undefined",
});
```

### C# / .NET

```csharp
using var client = new HttpClient();
client.BaseAddress = new Uri("http://localhost:5000");
client.DefaultRequestHeaders.Add("X-API-Key", "lfdev_your_api_key");

await client.PostAsJsonAsync("/api/logs", new {
    level = "info",
    message = "Order created",
    appName = "order-service",
    userId = "usr_123",
    metadata = new { orderId = "ord_456", total = 59.99 }
});
```

### Go

```go
body, _ := json.Marshal(map[string]any{
    "level": "error", "message": "Redis timeout", "appName": "cache-service",
    "exceptionType": "RedisTimeoutError", "durationMs": 5000,
})
req, _ := http.NewRequest("POST", "http://localhost:5000/api/logs", bytes.NewBuffer(body))
req.Header.Set("Content-Type", "application/json")
req.Header.Set("X-API-Key", "lfdev_your_api_key")
http.DefaultClient.Do(req)
```

### PHP

```php
$ch = curl_init('http://localhost:5000/api/logs');
curl_setopt_array($ch, [
    CURLOPT_POST => true,
    CURLOPT_POSTFIELDS => json_encode([
        'level' => 'warning',
        'message' => 'Disk usage above 90%',
        'appName' => 'monitoring',
        'host' => gethostname(),
    ]),
    CURLOPT_HTTPHEADER => ['Content-Type: application/json', 'X-API-Key: lfdev_your_api_key'],
]);
curl_exec($ch);
```

### Java

```java
HttpClient client = HttpClient.newHttpClient();
String json = """
    {"level":"info","message":"Batch job completed","appName":"scheduler","durationMs":4523}
    """;
HttpRequest request = HttpRequest.newBuilder()
    .uri(URI.create("http://localhost:5000/api/logs"))
    .header("Content-Type", "application/json")
    .header("X-API-Key", "lfdev_your_api_key")
    .POST(HttpRequest.BodyPublishers.ofString(json))
    .build();
client.send(request, HttpResponse.BodyHandlers.ofString());
```

### Ruby

```ruby
require 'net/http'
require 'json'

uri = URI('http://localhost:5000/api/logs')
req = Net::HTTP::Post.new(uri, 'Content-Type' => 'application/json', 'X-API-Key' => 'lfdev_your_api_key')
req.body = { level: 'info', message: 'Background job finished', appName: 'sidekiq-worker' }.to_json
Net::HTTP.start(uri.hostname, uri.port) { |http| http.request(req) }
```

---

## Configuration

### docker-compose.yml environment variables

```yaml
environment:
  - ClickHouse__Host=clickhouse       # ClickHouse hostname
  - ClickHouse__Port=8123             # ClickHouse HTTP port
  - ClickHouse__Database=logfordev    # Database name
  - ClickHouse__Username=admin        # ClickHouse user
  - ClickHouse__Password=admin        # ClickHouse password
  - LogForDev__RetentionDays=30       # Auto-delete logs older than N days
```

### appsettings.json

```json
{
  "ClickHouse": {
    "Host": "localhost",
    "Port": 8123,
    "Database": "logfordev",
    "Username": "admin",
    "Password": "admin"
  },
  "LogForDev": {
    "RetentionDays": 90
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `ClickHouse.Host` | `localhost` | ClickHouse server address |
| `ClickHouse.Port` | `8123` | ClickHouse HTTP port |
| `ClickHouse.Database` | `logfordev` | Database name (created automatically) |
| `ClickHouse.Username` | `admin` | ClickHouse username |
| `ClickHouse.Password` | `admin` | ClickHouse password |
| `LogForDev.RetentionDays` | `90` | Days to keep logs (ClickHouse TTL) |

---

## Architecture

```
                          ┌──────────────────────────────────────────────┐
                          │              LogForDev Server                │
┌──────────────┐          │                                              │
│  Your Apps   │          │  ┌────────────┐    ┌──────────────────────┐  │
│              │  HTTP    │  │  REST API  │    │     Dashboard        │  │
│  Python      │─────────>│  │  /api/logs │    │  (Razor MVC + Auth)  │  │
│  Node.js     │  POST    │  └─────┬──────┘    └──────────┬───────────┘  │
│  Go / Java   │          │        │                      │              │
│  C# / PHP    │          │        v                      │              │
│  Ruby / ...  │          │  ┌───────────┐                │              │
│              │          │  │  Buffer   │                │              │
└──────────────┘          │  │  Service  │                │              │
                          │  └─────┬─────┘                │              │
                          │        │ batch write           │              │
                          │        v                      v              │
                          │  ┌──────────────────────────────────────┐    │
                          │  │          ClickHouse 24.1             │    │
                          │  │  ┌──────┐ ┌────────┐ ┌──────────┐   │    │
                          │  │  │ logs │ │projects│ │  users   │   │    │
                          │  │  └──────┘ └────────┘ └──────────┘   │    │
                          │  └──────────────────────────────────────┘    │
                          └──────────────────────────────────────────────┘
```

### Database Tables

| Table | Engine | Purpose |
|-------|--------|---------|
| `logs` | MergeTree (partitioned by month, TTL) | User-submitted log entries (20+ fields) |
| `projects` | MergeTree (ordered by api_key) | API keys and project metadata |
| `users` | MergeTree (ordered by email) | Dashboard users with TOTP secrets |
| `app_logs` | MergeTree (partitioned by month, TTL) | LogForDev's own internal logs |

### Tech Stack

| Component | Technology |
|-----------|------------|
| Framework | ASP.NET Core 10.0 (.NET 10) |
| Database | ClickHouse 24.1 |
| Data Access | Dapper + ClickHouse.Client |
| Auth | Cookie (dashboard) + API Key (ingestion) + TOTP 2FA |
| Password Hashing | BCrypt |
| Logging | Serilog |
| Deployment | Docker Compose |
| License | MIT |

---

## Security

- **TOTP Two-Factor Authentication** for dashboard login (Google Authenticator, Authy, etc.)
- **BCrypt** password hashing
- **Encrypted cookies** via ASP.NET Core Data Protection API
- **API key isolation** per project with optional expiration dates
- **Account lockout** after failed login attempts

---

## Development

```bash
# Clone the repo
git clone https://github.com/melihhopa633/LogForDev.git
cd LogForDev

# Start only ClickHouse
docker-compose up -d clickhouse

# Run the app in development mode
cd src/LogForDev
dotnet run
```

The app will be available at `http://localhost:5000`.

---

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

## License

MIT License - see [LICENSE](LICENSE) for details.

---

<p align="center">
  Made by <a href="https://github.com/melihhopa633">Melih Hopa</a>
</p>
