# LogForDev

<p align="center">
  <img src="docs/logo.png" alt="LogForDev Logo" width="200">
</p>

<p align="center">
  <strong>🚀 Self-hosted, real-time logging system for developers</strong><br>
  Simple. Fast. Free.
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#api-reference">API</a> •
  <a href="#clients">Clients</a> •
  <a href="#configuration">Config</a> •
  <a href="#contributing">Contributing</a>
</p>

---

## Why LogForDev?

Tired of expensive logging services? Fed up with complex ELK stacks? **LogForDev** is the answer.

| Feature | Datadog | Seq | ELK Stack | LogForDev |
|---------|---------|-----|-----------|-----------|
| Self-hosted | ❌ | ✅ | ✅ | ✅ |
| Free | ❌ | ❌ | ✅ | ✅ |
| Easy setup | ✅ | ✅ | ❌ | ✅ |
| Real-time | ✅ | ✅ | ⚠️ | ✅ |
| Any language | ✅ | ⚠️ | ✅ | ✅ |

## Features

- ✅ **5-minute setup** - Docker compose and done
- ✅ **Works with any language** - Simple REST API
- ✅ **Real-time dashboard** - WebSocket powered live view
- ✅ **Blazing fast** - ClickHouse backend handles millions of logs
- ✅ **Self-hosted** - Your data stays on your server
- ✅ **100% free** - MIT licensed, forever

## Quick Start

### Using Docker (Recommended)

```bash
# Clone the repository
git clone https://github.com/melihhopa633/LogForDev.git
cd LogForDev

# Start everything
docker-compose up -d

# That's it! 🎉
```

Open http://localhost:5000 to see the dashboard.

### Send Your First Log

```bash
curl -X POST http://localhost:5000/api/logs \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "level": "info",
    "message": "Hello LogForDev!",
    "appName": "my-app"
  }'
```

## API Reference

### POST /api/logs

Send a single log entry.

```json
{
  "level": "info",
  "message": "User logged in",
  "appName": "auth-service",
  "metadata": {
    "userId": 123,
    "ip": "192.168.1.1"
  }
}
```

**Log Levels:** `trace`, `debug`, `info`, `warning`, `error`, `fatal`

### POST /api/logs/batch

Send multiple logs at once.

```json
{
  "logs": [
    { "level": "info", "message": "Request started", "appName": "api" },
    { "level": "info", "message": "Request completed", "appName": "api" }
  ]
}
```

### Response

```json
{
  "success": true,
  "id": "550e8400-e29b-41d4-a716-446655440000"
}
```

## Clients

Official client libraries for easy integration:

| Language | Package | Status |
|----------|---------|--------|
| .NET | `LogForDev.Client` | ✅ Ready |
| Python | `logfordev` | 🚧 Coming Soon |
| Node.js | `logfordev` | 🚧 Coming Soon |
| Java | `logfordev` | 🚧 Coming Soon |
| Go | `logfordev` | 🚧 Coming Soon |

### .NET Example

```csharp
// Install: dotnet add package LogForDev.Client

builder.Services.AddLogForDev(options =>
{
    options.ServerUrl = "http://localhost:5000";
    options.ApiKey = "your-api-key";
    options.AppName = "my-dotnet-app";
});

// Usage
public class MyService
{
    private readonly ILogForDevClient _logger;
    
    public MyService(ILogForDevClient logger)
    {
        _logger = logger;
    }
    
    public async Task DoSomething()
    {
        await _logger.LogAsync(LogLevel.Info, "Something happened", new { 
            UserId = 123 
        });
    }
}
```

### Python Example

```python
# Install: pip install logfordev

from logfordev import LogForDev

logger = LogForDev(
    server_url="http://localhost:5000",
    api_key="your-api-key",
    app_name="my-python-app"
)

logger.info("Hello from Python!", metadata={"user_id": 123})
```

### Node.js Example

```javascript
// Install: npm install logfordev

const { LogForDev } = require('logfordev');

const logger = new LogForDev({
  serverUrl: 'http://localhost:5000',
  apiKey: 'your-api-key',
  appName: 'my-node-app'
});

logger.info('Hello from Node.js!', { userId: 123 });
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `CLICKHOUSE_HOST` | ClickHouse server host | `localhost` |
| `CLICKHOUSE_PORT` | ClickHouse server port | `8123` |
| `CLICKHOUSE_DATABASE` | Database name | `logfordev` |
| `API_KEY` | API key for authentication | `change-me` |
| `RETENTION_DAYS` | Days to keep logs | `30` |

### appsettings.json

```json
{
  "LogForDev": {
    "ClickHouse": {
      "Host": "localhost",
      "Port": 8123,
      "Database": "logfordev"
    },
    "ApiKey": "your-secure-api-key",
    "RetentionDays": 30
  }
}
```

## Dashboard

Real-time log viewer with powerful filtering:

- 🔴 **Live mode** - See logs as they arrive
- 🔍 **Search** - Full-text search in messages
- 🏷️ **Filter** - By level, app, time range
- 📊 **Stats** - Log volume charts

![Dashboard Screenshot](docs/dashboard.png)

## Architecture

```
┌─────────────────┐     ┌──────────────────────────────────────┐
│   Your Apps     │     │           LogForDev Server           │
│                 │     │                                      │
│  .NET / Python  │     │  ┌─────────────┐  ┌──────────────┐   │
│  Node / Java    │────▶│  │   REST API  │  │  Dashboard   │   │
│  Go / PHP / ... │     │  │  /api/logs  │  │    (MVC)     │   │
│                 │     │  └──────┬──────┘  └───────┬──────┘   │
└─────────────────┘     │         │                 │          │
                        │         ▼                 │          │
                        │  ┌─────────────┐          │          │
                        │  │  ClickHouse │◀─────────┘          │
                        │  │  (Storage)  │                     │
                        │  └─────────────┘                     │
                        └──────────────────────────────────────┘
```

## Contributing

We love contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Setup

```bash
# Clone
git clone https://github.com/melihhopa633/LogForDev.git
cd LogForDev

# Start ClickHouse
docker-compose up -d clickhouse

# Run the app
cd src/LogForDev
dotnet run
```

## Roadmap

- [x] Core API
- [x] Real-time dashboard
- [x] Docker support
- [x] .NET client
- [ ] Python client
- [ ] Node.js client
- [ ] Alerting system
- [ ] Log aggregation
- [ ] Kubernetes Helm chart

## License

MIT License - see [LICENSE](LICENSE) for details.

---

<p align="center">
  Made with ❤️ by <a href="https://github.com/melihhopa633">Melih Hopa</a>
</p>
