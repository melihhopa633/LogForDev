#!/usr/bin/env python3
"""
Log Generator for LogForDev
Generates large amounts of realistic log data directly into ClickHouse

Usage:
    python generate_logs.py --size 1GB        # Generate ~1GB of logs
    python generate_logs.py --count 10000000  # Generate 10 million logs
    python generate_logs.py --size 5GB --batch 100000  # 5GB with larger batches
"""

import argparse
import random
import uuid
import json
import time
from datetime import datetime, timedelta
from concurrent.futures import ThreadPoolExecutor, as_completed
import requests

# ClickHouse connection settings
CLICKHOUSE_HOST = "localhost"
CLICKHOUSE_PORT = 8123
CLICKHOUSE_USER = "admin"
CLICKHOUSE_PASSWORD = "admin"
CLICKHOUSE_DATABASE = "logfordev"

# Sample data for realistic logs
APP_NAMES = [
    "auth-service", "api-gateway", "user-service", "payment-service",
    "notification-service", "order-service", "inventory-service",
    "search-service", "analytics-service", "email-service",
    "file-service", "cache-service", "queue-worker", "scheduler",
    "web-frontend", "mobile-backend", "admin-panel", "webhook-handler"
]

ENVIRONMENTS = ["production", "staging", "development", "testing"]

HOSTS = [f"server-{i:02d}.prod.internal" for i in range(1, 21)]

LOG_LEVELS = ["Trace", "Debug", "Info", "Warning", "Error", "Fatal"]
LEVEL_WEIGHTS = [5, 15, 50, 20, 8, 2]  # Realistic distribution

# Message templates by log level
MESSAGE_TEMPLATES = {
    "Trace": [
        "Entering method {method} with params: {params}",
        "Variable state: {var}={value}",
        "Cache lookup for key: {key}",
        "SQL query executed: {query}",
        "HTTP request details: {details}",
    ],
    "Debug": [
        "Processing request for user {user_id}",
        "Cache hit for key: {key}",
        "Database query returned {count} rows",
        "Request payload size: {size} bytes",
        "Response time: {time}ms",
        "Session validated for user {user_id}",
        "Loading configuration from {source}",
    ],
    "Info": [
        "User {user_id} logged in successfully",
        "Order {order_id} created for amount ${amount}",
        "Email sent to {email}",
        "File {filename} uploaded successfully ({size} bytes)",
        "Payment processed: {transaction_id}",
        "API request completed: {method} {path} - {status}",
        "Scheduled job {job_name} started",
        "User {user_id} updated profile",
        "New user registered: {email}",
        "Webhook delivered to {url}",
        "Background task {task_id} completed",
        "Cache refreshed for {cache_name}",
    ],
    "Warning": [
        "High memory usage detected: {usage}%",
        "Slow query detected ({time}ms): {query}",
        "Rate limit approaching for client {client_id}",
        "Deprecated API endpoint called: {endpoint}",
        "Connection pool running low: {available}/{total}",
        "Retry attempt {attempt} for operation {operation}",
        "Cache miss rate high: {rate}%",
        "Request timeout extended for {path}",
        "Disk space running low: {available}GB remaining",
    ],
    "Error": [
        "Failed to process payment: {error}",
        "Database connection failed: {error}",
        "API request failed: {method} {path} - {status}",
        "Authentication failed for user {user_id}",
        "File upload failed: {error}",
        "Email delivery failed to {email}: {error}",
        "Webhook delivery failed to {url}: {status}",
        "Cache operation failed: {error}",
        "External service unavailable: {service}",
        "Validation error: {field} - {error}",
    ],
    "Fatal": [
        "System out of memory - shutting down",
        "Database connection pool exhausted",
        "Critical configuration missing: {config}",
        "Unrecoverable error in {component}: {error}",
        "Service health check failed - restarting",
        "Disk full - cannot write to {path}",
    ],
}

ERROR_MESSAGES = [
    "Connection refused", "Timeout exceeded", "Invalid credentials",
    "Resource not found", "Permission denied", "Rate limit exceeded",
    "Internal server error", "Bad gateway", "Service unavailable",
    "Network unreachable", "SSL certificate error", "DNS resolution failed",
]

METHODS = ["GET", "POST", "PUT", "DELETE", "PATCH"]
PATHS = [
    "/api/users", "/api/orders", "/api/products", "/api/auth/login",
    "/api/auth/logout", "/api/payments", "/api/notifications",
    "/api/files/upload", "/api/search", "/api/analytics",
    "/api/webhooks", "/api/settings", "/api/health", "/api/metrics",
]


def generate_trace_id():
    return uuid.uuid4().hex[:32]


def generate_span_id():
    return uuid.uuid4().hex[:16]


def generate_metadata(level):
    """Generate realistic metadata based on log level"""
    metadata = {}

    if random.random() < 0.3:
        metadata["request_id"] = str(uuid.uuid4())

    if random.random() < 0.2:
        metadata["user_id"] = random.randint(1000, 999999)

    if random.random() < 0.1:
        metadata["duration_ms"] = round(random.uniform(1, 5000), 2)

    if level in ["Error", "Fatal"] and random.random() < 0.5:
        metadata["stack_trace"] = f"at Module.function (file.js:{random.randint(1, 500)}:{random.randint(1, 100)})\n" * random.randint(3, 10)

    if random.random() < 0.15:
        metadata["ip_address"] = f"{random.randint(1, 255)}.{random.randint(0, 255)}.{random.randint(0, 255)}.{random.randint(1, 254)}"

    return json.dumps(metadata) if metadata else None


def generate_message(level):
    """Generate a realistic log message"""
    template = random.choice(MESSAGE_TEMPLATES[level])

    replacements = {
        "{method}": random.choice(METHODS),
        "{path}": random.choice(PATHS),
        "{user_id}": str(random.randint(1000, 999999)),
        "{order_id}": f"ORD-{random.randint(100000, 999999)}",
        "{transaction_id}": f"TXN-{uuid.uuid4().hex[:12]}",
        "{email}": f"user{random.randint(1, 9999)}@example.com",
        "{filename}": f"file_{random.randint(1, 9999)}.{random.choice(['pdf', 'jpg', 'png', 'doc', 'xlsx'])}",
        "{amount}": f"{random.uniform(10, 1000):.2f}",
        "{size}": str(random.randint(100, 10000000)),
        "{time}": str(random.randint(1, 30000)),
        "{count}": str(random.randint(0, 10000)),
        "{status}": str(random.choice([200, 201, 400, 401, 403, 404, 500, 502, 503])),
        "{error}": random.choice(ERROR_MESSAGES),
        "{key}": f"cache:{random.choice(['user', 'session', 'data', 'config'])}:{random.randint(1, 9999)}",
        "{usage}": str(random.randint(70, 99)),
        "{rate}": str(random.randint(20, 80)),
        "{available}": str(random.randint(1, 100)),
        "{total}": str(random.randint(100, 500)),
        "{attempt}": str(random.randint(1, 5)),
        "{operation}": random.choice(["db_write", "api_call", "file_upload", "email_send"]),
        "{client_id}": f"client-{random.randint(1, 100)}",
        "{endpoint}": random.choice(PATHS),
        "{query}": "SELECT * FROM users WHERE ...",
        "{job_name}": random.choice(["cleanup", "sync", "backup", "report", "notify"]),
        "{task_id}": str(uuid.uuid4())[:8],
        "{cache_name}": random.choice(["users", "products", "sessions", "configs"]),
        "{url}": f"https://webhook.example.com/{random.randint(1, 100)}",
        "{service}": random.choice(["payment-gateway", "email-provider", "sms-service", "cdn"]),
        "{field}": random.choice(["email", "phone", "address", "amount"]),
        "{component}": random.choice(["database", "cache", "queue", "storage"]),
        "{config}": random.choice(["DATABASE_URL", "API_KEY", "SECRET_KEY"]),
        "{params}": json.dumps({"id": random.randint(1, 1000)}),
        "{var}": random.choice(["result", "counter", "state"]),
        "{value}": str(random.randint(0, 100)),
        "{details}": f"Headers: {random.randint(5, 20)}, Body: {random.randint(0, 10000)} bytes",
        "{source}": random.choice(["env", "file", "remote"]),
    }

    message = template
    for key, value in replacements.items():
        message = message.replace(key, value)

    return message


def generate_log_entry(base_time, time_offset_seconds):
    """Generate a single log entry"""
    level = random.choices(LOG_LEVELS, weights=LEVEL_WEIGHTS)[0]
    timestamp = base_time + timedelta(seconds=time_offset_seconds)

    # Generate trace context (30% of logs have trace context)
    trace_id = generate_trace_id() if random.random() < 0.3 else None
    span_id = generate_span_id() if trace_id else None

    return {
        "id": str(uuid.uuid4()),
        "timestamp": timestamp.strftime("%Y-%m-%d %H:%M:%S.%f")[:-3],
        "level": level,
        "app_name": random.choice(APP_NAMES),
        "message": generate_message(level),
        "metadata": generate_metadata(level),
        "trace_id": trace_id,
        "span_id": span_id,
        "host": random.choice(HOSTS),
        "environment": random.choices(ENVIRONMENTS, weights=[70, 15, 10, 5])[0],
    }


def insert_batch_clickhouse(logs, host="localhost", port=8123):
    """Insert a batch of logs directly into ClickHouse"""
    # Build INSERT query
    values = []
    for log in logs:
        metadata = log["metadata"] if log["metadata"] else ""
        trace_id = log["trace_id"] if log["trace_id"] else ""
        span_id = log["span_id"] if log["span_id"] else ""

        # Escape single quotes in message and metadata
        message = log["message"].replace("'", "\\'")
        metadata = metadata.replace("'", "\\'") if metadata else ""

        values.append(
            f"('{log['id']}', '{log['timestamp']}', '{log['level']}', "
            f"'{log['app_name']}', '{message}', '{metadata}', "
            f"'{trace_id}', '{span_id}', '{log['host']}', '{log['environment']}')"
        )

    query = f"""
        INSERT INTO logfordev.logs
        (id, timestamp, level, app_name, message, metadata, trace_id, span_id, host, environment)
        VALUES {','.join(values)}
    """

    response = requests.post(
        f"http://{host}:{port}/",
        params={"user": "admin", "password": "admin"},
        data=query,
        timeout=120
    )

    if response.status_code != 200:
        raise Exception(f"ClickHouse error: {response.text}")

    return len(logs)


def insert_batch_api(logs, api_url, api_key):
    """Insert a batch of logs via API"""
    payload = {
        "logs": [
            {
                "level": log["level"].lower(),
                "message": log["message"],
                "appName": log["app_name"],
                "metadata": json.loads(log["metadata"]) if log["metadata"] else None,
                "traceId": log["trace_id"],
                "spanId": log["span_id"],
                "host": log["host"],
                "environment": log["environment"],
            }
            for log in logs
        ]
    }

    response = requests.post(
        f"{api_url}/api/logs/batch",
        json=payload,
        headers={"X-API-Key": api_key},
        timeout=60
    )

    if response.status_code != 200:
        raise Exception(f"API error: {response.text}")

    return len(logs)


def parse_size(size_str):
    """Parse size string like '1GB' to bytes"""
    size_str = size_str.upper().strip()
    multipliers = [
        ("TB", 1024**4),
        ("GB", 1024**3),
        ("MB", 1024**2),
        ("KB", 1024),
        ("B", 1),
    ]

    for suffix, multiplier in multipliers:
        if size_str.endswith(suffix):
            number = float(size_str[:-len(suffix)])
            return int(number * multiplier)

    return int(size_str)


def estimate_log_size():
    """Estimate average size of a single log entry in bytes"""
    # Generate some sample logs and measure
    samples = [generate_log_entry(datetime.utcnow(), 0) for _ in range(100)]
    total_size = sum(len(json.dumps(log)) for log in samples)
    return total_size / len(samples)


def main():
    parser = argparse.ArgumentParser(description="Generate log data for LogForDev")
    parser.add_argument("--size", type=str, help="Target data size (e.g., 1GB, 500MB)")
    parser.add_argument("--count", type=int, help="Number of log entries to generate")
    parser.add_argument("--batch", type=int, default=10000, help="Batch size (default: 10000)")
    parser.add_argument("--workers", type=int, default=4, help="Number of parallel workers")
    parser.add_argument("--mode", choices=["clickhouse", "api"], default="clickhouse",
                        help="Insert mode: clickhouse (direct) or api")
    parser.add_argument("--api-url", default="http://localhost:5000", help="API URL for api mode")
    parser.add_argument("--api-key", default="change-me-in-production", help="API key for api mode")
    parser.add_argument("--days", type=int, default=30, help="Spread logs over N days (default: 30)")
    parser.add_argument("--host", default="localhost", help="ClickHouse host")
    parser.add_argument("--port", type=int, default=8123, help="ClickHouse port")

    args = parser.parse_args()

    clickhouse_host = args.host
    clickhouse_port = args.port

    # Calculate number of logs to generate
    if args.size:
        target_bytes = parse_size(args.size)
        avg_log_size = estimate_log_size()
        total_logs = int(target_bytes / avg_log_size)
        print(f"Target size: {args.size} (~{target_bytes:,} bytes)")
        print(f"Estimated log size: {avg_log_size:.0f} bytes")
    elif args.count:
        total_logs = args.count
    else:
        print("Error: Specify either --size or --count")
        return

    print(f"Generating {total_logs:,} log entries...")
    print(f"Batch size: {args.batch:,}")
    print(f"Workers: {args.workers}")
    print(f"Mode: {args.mode}")
    print(f"Time range: last {args.days} days")
    print("-" * 50)

    # Time range setup
    end_time = datetime.utcnow()
    start_time = end_time - timedelta(days=args.days)
    time_range_seconds = (end_time - start_time).total_seconds()

    # Progress tracking
    generated = 0
    inserted = 0
    start = time.time()
    last_report = start

    # Generate and insert in batches
    num_batches = (total_logs + args.batch - 1) // args.batch

    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        futures = []

        for batch_num in range(num_batches):
            batch_start = batch_num * args.batch
            batch_size = min(args.batch, total_logs - batch_start)

            # Generate batch
            batch_logs = []
            for i in range(batch_size):
                offset = random.uniform(0, time_range_seconds)
                batch_logs.append(generate_log_entry(start_time, offset))

            generated += batch_size

            # Submit for insertion
            if args.mode == "clickhouse":
                future = executor.submit(insert_batch_clickhouse, batch_logs, clickhouse_host, clickhouse_port)
            else:
                future = executor.submit(insert_batch_api, batch_logs, args.api_url, args.api_key)
            futures.append(future)

            # Process completed futures and report progress
            done_futures = [f for f in futures if f.done()]
            for f in done_futures:
                try:
                    inserted += f.result()
                except Exception as e:
                    print(f"\nError: {e}")
                futures.remove(f)

            # Progress report every 5 seconds
            now = time.time()
            if now - last_report >= 5:
                elapsed = now - start
                rate = inserted / elapsed if elapsed > 0 else 0
                eta = (total_logs - inserted) / rate if rate > 0 else 0
                print(f"Progress: {inserted:,}/{total_logs:,} ({100*inserted/total_logs:.1f}%) "
                      f"| Rate: {rate:,.0f} logs/sec | ETA: {eta:.0f}s")
                last_report = now

        # Wait for remaining futures
        for future in as_completed(futures):
            try:
                inserted += future.result()
            except Exception as e:
                print(f"\nError: {e}")

    # Final report
    elapsed = time.time() - start
    rate = inserted / elapsed if elapsed > 0 else 0
    estimated_size = inserted * estimate_log_size()

    print("-" * 50)
    print(f"Completed!")
    print(f"Total logs inserted: {inserted:,}")
    print(f"Estimated data size: {estimated_size / (1024**3):.2f} GB")
    print(f"Total time: {elapsed:.1f} seconds")
    print(f"Average rate: {rate:,.0f} logs/second")


if __name__ == "__main__":
    main()
