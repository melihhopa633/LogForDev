# LogForDev Log Schema

## Philosophy

LogForDev uses a **dedicated column** approach for high-value, frequently queried fields (level, exception, HTTP context, user_id) while keeping a flexible `metadata` JSON column for everything else. This gives us:

- **Fast filtering** on dedicated columns via ClickHouse's columnar storage + `LowCardinality` optimization
- **Flexibility** for ad-hoc data in metadata without schema migrations
- **Industry alignment** with OpenTelemetry, ECS (Elastic), and Datadog conventions

---

## Full Schema

### `logs` Table — ClickHouse MergeTree

| Column | ClickHouse Type | Default | Description |
|--------|----------------|---------|-------------|
| `id` | `UUID` | `generateUUIDv4()` | Unique log entry ID |
| `timestamp` | `DateTime64(3)` | `now64(3)` | Log timestamp (millisecond precision) |
| `level` | `Enum8` | — | Log severity: Trace(0), Debug(1), Info(2), Warning(3), Error(4), Fatal(5) |
| `app_name` | `LowCardinality(String)` | — | Application/service name |
| `message` | `String` | — | Human-readable log message |
| `metadata` | `String` | `'{}'` | JSON blob for arbitrary structured data |
| `exception_type` | `LowCardinality(String)` | `''` | Exception class name (e.g. `NullReferenceException`) |
| `exception_message` | `String` | `''` | Exception message text |
| `exception_stacktrace` | `String` | `''` | Full stack trace |
| `source` | `LowCardinality(String)` | `''` | Log source class/module (e.g. `OrderService`) |
| `request_method` | `LowCardinality(String)` | `''` | HTTP method: GET, POST, PUT, DELETE |
| `request_path` | `String` | `''` | HTTP request path (e.g. `/api/orders/123`) |
| `status_code` | `UInt16` | `0` | HTTP response status code |
| `duration_ms` | `Float64` | `0` | Request/operation duration in milliseconds |
| `user_id` | `String` | `''` | User identifier for the action |
| `trace_id` | `String` | `''` | Distributed trace ID (OpenTelemetry compatible) |
| `span_id` | `String` | `''` | Span ID within a trace |
| `host` | `LowCardinality(String)` | `''` | Server hostname or IP |
| `environment` | `LowCardinality(String)` | `'production'` | Deployment environment |
| `project_id` | `UUID` | `00000000-...` | LogForDev project ID |
| `project_name` | `LowCardinality(String)` | `''` | LogForDev project name |
| `created_at` | `DateTime` | `now()` | Row insertion time (used for TTL) |

**Engine:** `MergeTree()`
**Partition:** `toYYYYMM(timestamp)`
**Order:** `(app_name, level, timestamp)`
**TTL:** `created_at + INTERVAL {retentionDays} DAY`

---

## Exception Fields

Three dedicated columns capture exception data for fast filtering and display:

| Field | Purpose | Example |
|-------|---------|---------|
| `exception_type` | Exception class/type name | `NullReferenceException`, `ValueError`, `TimeoutError` |
| `exception_message` | Human-readable error description | `Object reference not set to an instance of an object` |
| `exception_stacktrace` | Full stack trace for debugging | Multi-line trace string |

### JSON API Mapping

```json
{
  "exceptionType": "NullReferenceException",
  "exceptionMessage": "Object reference not set to an instance of an object",
  "exceptionStacktrace": "at OrderService.Process() in OrderService.cs:line 42\n   at Program.Main()"
}
```

---

## HTTP Context Fields

Four columns capture HTTP request/response context:

| Field | Purpose | Example |
|-------|---------|---------|
| `request_method` | HTTP verb | `GET`, `POST`, `PUT`, `DELETE` |
| `request_path` | Request URL path | `/api/orders/123` |
| `status_code` | Response status code | `200`, `404`, `500` |
| `duration_ms` | Request duration | `234.5` |

---

## Metadata JSON Structure

The `metadata` column accepts any valid JSON. Recommended keys (not enforced):

| Key | Type | Description |
|-----|------|-------------|
| `thread_id` | string | Thread/goroutine identifier |
| `process_id` | number | OS process ID |
| `file_name` | string | Source file name |
| `line_number` | number | Source line number |
| `function_name` | string | Function/method name |
| `service_version` | string | Application version |
| `machine_name` | string | Machine/container name |
| `user_agent` | string | HTTP User-Agent header |
| `parent_span_id` | string | Parent span for nested traces |
| `error_code` | string | Application-specific error code |
| `message_template` | string | Structured log template (Serilog-style) |

### Example

```json
{
  "thread_id": "worker-3",
  "file_name": "order_service.py",
  "line_number": 142,
  "function_name": "process_payment",
  "service_version": "2.1.0",
  "error_code": "PAY_TIMEOUT",
  "message_template": "Payment for order {orderId} failed after {duration}ms"
}
```

---

## ClickHouse Performance Notes

- **LowCardinality**: Used for columns with limited distinct values (`level`, `app_name`, `environment`, `source`, `request_method`, `exception_type`, `host`, `project_name`). Provides dictionary encoding for 5-10x compression and faster filtering.
- **ORDER BY `(app_name, level, timestamp)`**: Optimized for the most common query patterns — filtering by app, then level, then time range.
- **PARTITION BY `toYYYYMM(timestamp)`**: Monthly partitions enable efficient time-based queries and automatic data lifecycle via TTL.
- **TTL**: Automatic data expiration based on `created_at` field. Configurable via `RetentionDays` setting.

---

## Industry Comparison

| LogForDev Field | OpenTelemetry | ECS (Elastic) | Datadog |
|----------------|---------------|---------------|---------|
| `level` | `SeverityText` | `log.level` | `status` |
| `message` | `Body` | `message` | `message` |
| `app_name` | `Resource.service.name` | `service.name` | `service` |
| `exception_type` | `exception.type` | `error.type` | `error.kind` |
| `exception_message` | `exception.message` | `error.message` | `error.message` |
| `exception_stacktrace` | `exception.stacktrace` | `error.stack_trace` | `error.stack` |
| `source` | `InstrumentationScope.name` | `log.logger` | `logger.name` |
| `request_method` | `http.request.method` | `http.request.method` | `http.method` |
| `request_path` | `url.path` | `url.path` | `http.url_details.path` |
| `status_code` | `http.response.status_code` | `http.response.status_code` | `http.status_code` |
| `duration_ms` | `— (span duration)` | `event.duration` | `duration` |
| `user_id` | `enduser.id` | `user.id` | `usr.id` |
| `trace_id` | `TraceId` | `trace.id` | `dd.trace_id` |
| `span_id` | `SpanId` | `span.id` | `dd.span_id` |
| `host` | `Resource.host.name` | `host.name` | `hostname` |
| `environment` | `Resource.deployment.environment` | `service.environment` | `env` |
| `metadata` | `Attributes` | `labels` | `custom tags` |

---

## Backward Compatibility

All new fields are **optional** with sensible defaults:
- String fields default to `''` (empty string)
- `status_code` defaults to `0`
- `duration_ms` defaults to `0`

Existing integrations sending logs without the new fields will continue to work without any changes.
