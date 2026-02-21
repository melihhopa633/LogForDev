# LogForDev — Senior-Level Refactoring Plan

Codebase tamamıyla incelendi. 5 kritik güvenlik açığı, birkaç anti-pattern, bir bug ve çok sayıda maintainability sorunu tespit edildi. Hiçbir yeni feature eklenmeyecek — sadece mevcut kod temizlenecek ve güvenli hale getirilecek.

---

## Faz 1 — Güvenlik (CRITICAL — önce bu yapılacak)

### 1A. TOTP "000000" bypass → TestMode flag'ına bağla
**Dosyalar:** `appsettings.json`, `Services/Options.cs`, `Services/UserService.cs`

Bypass tamamen kaldırılmaz; `appsettings.json`'daki `TestMode` değerine göre conditional çalışır.

**Adım 1 — `appsettings.json`'a `TestMode` ekle:**
```json
"LogForDev": {
  "RetentionDays": 90,
  "TestMode": false
}
```
> Production'da `false`, geliştirme ortamında `true` yapılır. Default `false` olmalı.

**Adım 2 — `Services/Options.cs`'e property ekle:**
```csharp
public class LogForDevOptions
{
    public int RetentionDays { get; set; } = 30;
    public bool TestMode { get; set; } = false;   // YENİ
}
```

**Adım 3 — `UserService`'e inject et ve bypass'ı koruma altına al:**
```csharp
// Constructor'a ekle:
private readonly LogForDevOptions _options;

public UserService(
    IOptions<ClickHouseOptions> clickHouseOptions,
    IOptions<LogForDevOptions> logForDevOptions,   // YENİ
    ILogger<UserService> logger)
{
    _options = logForDevOptions.Value;
}

// VerifyTotpCode() içinde:
public bool VerifyTotpCode(string secret, string code)
{
    try
    {
        // Sadece TestMode açıksa bypass kabul edilir
        if (_options.TestMode && code == "000000")
        {
            _logger.LogWarning("TOTP test bypass used — TestMode is enabled");
            return true;
        }

        var secretBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(secretBytes);
        return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error verifying TOTP code");
        return false;
    }
}
```

---

### 1B. Login view test bilgilerini TestMode'a bağla
**Dosyalar:** `Controllers/AuthController.cs`, `Views/Auth/Login.cshtml`

Hardcoded değerler doğrudan kaldırılmaz; sadece `TestMode: true` iken gösterilir.

**Adım 1 — `AuthController.Login()`'e ViewBag ekle:**
```csharp
// AuthController'a IOptions<LogForDevOptions> inject et
[HttpGet("/login")]
public IActionResult Login()
{
    if (User.Identity?.IsAuthenticated == true)
        return Redirect("/");

    ViewBag.TestMode = _options.TestMode;
    return View();
}
```

**Adım 2 — `Login.cshtml`'de değerleri conditional yap:**
```html
<!-- Email -->
<input type="email" id="email"
       value="@(ViewBag.TestMode == true ? "test@admin.com" : "")"
       placeholder="admin@example.com" ... />

<!-- Password -->
<input type="password" id="password"
       value="@(ViewBag.TestMode == true ? "testpassword" : "")"
       placeholder="••••••••" ... />

<!-- TOTP -->
<input type="text" id="totpCode"
       value="@(ViewBag.TestMode == true ? "000000" : "")"
       placeholder="000000" ... />

@if (ViewBag.TestMode == true)
{
    <p class="text-xs text-yellow-500 mt-1">⚠️ Test modu aktif — 000000 kullanılabilir</p>
}
```

**Sorun (mevcut):** Hardcoded bilgiler her ortamda görünüyor; `TestMode: false` iken production'da bile otomatik doluyor.
**Çözüm:** `TestMode: false` iken inputlar boş gelir, hint metni görünmez.

---

### 1C. [AllowAnonymous] güvenlik açığını kapat
**Dosyalar:** `Controllers/LogsController.cs`, `Middleware/AuthenticationMiddleware.cs`, `Program.cs`

Şu an **anonim erişime açık** olan ama olmaması gereken endpoint'ler:

| Endpoint | Risk |
|----------|------|
| `DELETE /api/logs` | Herkes tüm logları silebilir |
| `GET /api/logs/projects` | Herkes API key listesini görebilir |
| `POST /api/logs/projects` | Herkes proje oluşturabilir |
| `PUT /api/logs/projects/{id}` | Herkes proje adını değiştirebilir |
| `DELETE /api/logs/projects/{id}` | Herkes proje silebilir |
| `GET /api/logs/app` | Herkes internal app loglarını görebilir |
| `DELETE /api/logs/app` | Herkes internal logları silebilir |

**Çözüm adımları:**

**1.** `Program.cs`'e "DashboardOnly" policy eklenir (sadece cookie auth ile erişilebilir):
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DashboardOnly", policy =>
        policy.AddAuthenticationSchemes(CookieAuthenticationOptions.Scheme)
              .RequireAuthenticatedUser());
});
```

**2.** Yukarıdaki 7 endpoint'ten `[AllowAnonymous]` kaldırılır, `[Authorize(Policy = "DashboardOnly")]` eklenir.

**3.** `AuthenticationMiddleware.cs`'deki `PublicPaths` listesinden `"/api/logs"` kaldırılır. Bu prefix şu an `/api/logs/projects` gibi admin endpoint'leri de bypass ediyor. Log ingestion (`POST /api/logs`) zaten `[Authorize]` attribute'u ile ApiKey scheme üzerinden korunuyor, middleware'e ihtiyacı yok.

---

## Faz 2 — Constants Dosyası

### 2A. Magic string'leri tek yerde topla
**Yeni dosya:** `Core/AppConstants.cs`

Şu an birden fazla dosyada tekrarlanan string'ler:

```csharp
namespace LogForDev.Core;

public static class AppConstants
{
    public static class Auth
    {
        public const string CookieName = ".LogForDev.Auth";          // 2 dosyada tekrar
        public const string DataProtectorPurpose = "Auth.Cookie";    // 2 dosyada tekrar
        public const string DashboardOnlyPolicy = "DashboardOnly";
    }

    public static class Database
    {
        public const string HttpContextProjectKey = "Project";        // 2 dosyada tekrar
    }

    public static class Paths
    {
        public const string Login = "/login";
        public const string Setup = "/setup";
        public const string ApiAuth = "/api/auth";
        public const string ApiSetup = "/api/setup";
    }
}
```

Güncellenecek dosyalar:
- `Authentication/CookieAuthenticationHandler.cs`
- `Authentication/CookieAuthenticationOptions.cs`
- `Controllers/AuthController.cs`
- `Middleware/AuthenticationMiddleware.cs`
- `Extensions/HttpContextExtensions.cs`

---

## Faz 3 — Yapısal İyileştirmeler

### 3A. Setup DTO'larını Models klasörüne taşı
**Şu an:** `Controllers/SetupController.cs` dosyasının sonunda tanımlı (satır 261-288)
**Yeni dosya:** `Models/SetupModels.cs`

Taşınacak class'lar:
- `ConnectionTestRequest`
- `TestLogRequest`
- `SetupCompleteRequest`

**Neden:** Controller dosyasının içinde tanımlı DTO'lar `Models/` klasöründe arayanlar tarafından bulunamaz. Controller dosyasını gereksiz yere şişiriyor.

---

### 3B. Service Locator anti-pattern'i kaldır
**Dosya:** `Controllers/LogsController.cs`

Şu anki (yanlış) kullanım:
```csharp
// YANLIŞ — Service Locator pattern
var appLogService = HttpContext.RequestServices.GetRequiredService<IAppLogService>();
```

Doğru kullanım — constructor injection:
```csharp
// Constructor'a ekle:
private readonly IAppLogService _appLogService;

public LogsController(..., IAppLogService appLogService, ...)
{
    _appLogService = appLogService;
}
```

DI kaydı değişmez (`AppLogService` zaten `IAppLogService` olarak registered).

**Neden:** Service locator bağımlılığı gizler, test edilemez hale getirir, DI container'ın scope validasyonunu bypass eder.

---

### 3C. LogService thin delegation layer'ı kaldır
**Silinecek dosya:** `Services/LogService.cs`
**Güncellenecek:** `Controllers/LogsController.cs`, `Services/LogBufferService.cs`, `Program.cs`

`LogService`'in her metodu sadece şunu yapıyor:
```csharp
// Sıfır business logic — sadece wrap ediyor
try { return await _repository.SomeMethod(); }
catch (ex) { _logger.LogError(ex, "..."); throw; }
```

**Değişiklikler:**
- `LogsController` → `ILogService` yerine `ILogRepository` inject eder
  - Not: `GetLogsAsync` → `GetPagedAsync` olarak rename edilir
- `LogBufferService` → scope'dan `ILogRepository` resolve eder
- `Program.cs` → `ILogService` kaydı silinir
- `Services/LogService.cs` → tamamen silinir

**Neden:** Gereksiz soyutlama katmanı. Hem call stack'i derinleştiriyor hem de future developer'ların "iş mantığı nerede?" sorusuna yanlış sinyal veriyor.

---

### 3D. RetentionDays konfigürasyon bug'ı
**Dosya:** `Services/ClickHouseService.cs`

**Bug:** `appsettings.json`'da `"RetentionDays": 90` yazıyor, `LogForDevOptions` class'ı var, DI'ye kayıtlı — ama `ClickHouseService` bu option'ı **inject etmiyor**. Tablolar `TTL created_at + INTERVAL 30 DAY` ile sabit kodlanmış. Loglar 60 gün erken siliniyor.

**Çözüm:**
```csharp
// Constructor'a ekle:
private readonly LogForDevOptions _logForDevOptions;

public ClickHouseService(
    IOptions<ClickHouseOptions> options,
    IOptions<LogForDevOptions> logForDevOptions,  // YENİ
    ILogger<ClickHouseService> logger)
{
    _logForDevOptions = logForDevOptions.Value;
}

// InitializeAsync içinde:
var retentionDays = _logForDevOptions.RetentionDays > 0 ? _logForDevOptions.RetentionDays : 30;
// Sonra SQL'de: TTL created_at + INTERVAL {retentionDays} DAY
```

Mevcut tablolar için de TTL güncellenir:
```sql
ALTER TABLE logs MODIFY TTL created_at + INTERVAL {retentionDays} DAY
ALTER TABLE app_logs MODIFY TTL created_at + INTERVAL {retentionDays} DAY
```

---

## Faz 4 — Thread Safety

### 4A. ProjectService cache refresh race condition
**Dosya:** `Services/ProjectService.cs`

**Mevcut sorun:**
```csharp
_cache.Clear();          // ← buradan sonra cache boş
foreach (var p in projects)
    _cache[p.ApiKey] = p; // ← buraya gelene kadar ~30ms geçiyor
```

Bu 30ms window'da gelen her API isteği boş cache görüp direkt DB'ye gidiyor. Her 5 dakikada bir `thundering herd` yaşanıyor.

**Çözüm — atomic diff:**
```csharp
var newEntries = projects.ToDictionary(p => p.ApiKey, p => p);

// Silinenleri kaldır
foreach (var key in _cache.Keys.Where(k => !newEntries.ContainsKey(k)).ToList())
    _cache.TryRemove(key, out _);

// Yenileri/güncellenenleri ekle
foreach (var (key, value) in newEntries)
    _cache[key] = value;
```

Cache hiçbir zaman boşalmıyor. Concurrent request'ler her zaman ya eski ya yeni tam state'i görüyor.

---

## Faz 5 — Code Quality

### 5A. Tekrarlanan EscapeString'i tek yerde topla
**Şu an 4 dosyada aynı private method:**
- `Data/LogRepository.cs`
- `Data/ClickHouseQueryBuilder.cs`
- `Services/AppLogService.cs`
- `Services/ProjectService.cs`

**Yeni dosya:** `Data/ClickHouseStringHelper.cs`
```csharp
namespace LogForDev.Data;

internal static class ClickHouseStringHelper
{
    public static string Escape(string input) =>
        string.IsNullOrEmpty(input) ? input
            : input.Replace("\\", "\\\\").Replace("'", "\\'");
}
```

Her 4 dosyadan local method silinir, `ClickHouseStringHelper.Escape(...)` çağrılır.

---

### 5B. CancellationToken propagation ekle
**Dosyalar:** `IClickHouseService` → `ClickHouseService` → `ILogRepository` → `LogRepository` → Controller action'ları

Tüm async metodlara `CancellationToken cancellationToken = default` eklenir. ASP.NET Core controller action'larına token otomatik inject edilir. Client bağlantıyı kestiğinde ClickHouse query'si de iptal edilir.

---

### 5C. SetupController.Complete() methodunu ayır
**Yeni dosya:** `Services/SetupOrchestrator.cs`

Şu an `Complete()` 135 satırda 6 farklı iş yapıyor:
1. `appsettings.json` oku/yaz
2. Direkt ClickHouse bağlantısı aç
3. Tablo init
4. İlk proje oluştur
5. Admin kullanıcı oluştur
6. Setup'ı tamamlandı işaretle

`ISetupOrchestrator` service'ine taşınır. Controller sadece HTTP concern'leri ile kalır (request parse et, response döndür).

---

## Değişmeyen Dosyalar

| Dosya | Neden dokunulmadı |
|-------|-------------------|
| `Data/ClickHouseQueryBuilder.cs` | İyi yapılandırılmış |
| `Data/LogRepository.cs` | ClickHouse-specific SQL, builder ile ifade edilemez |
| `Services/SetupStateService.cs` | Basit ve doğru |
| `Authentication/ApiKeyAuthenticationHandler.cs` | Temiz |
| Tüm diğer Razor view'ler | Çalışıyor, değişiklik gerektirmiyor |

---

## Uygulama Sırası

| # | İş | Paralel Yapılabilir mi? |
|---|-----|------------------------|
| 1 | **1A** TOTP bypass → TestMode'a bağla | 1B, 1C ile paralel |
| 2 | **1B** Login test bilgileri → TestMode'a bağla | 1A, 1C ile paralel |
| 3 | **1C** AllowAnonymous düzelt | 1A, 1B ile paralel |
| 4 | **2A** AppConstants.cs | Faz 1 bittikten sonra |
| 5 | **3A** Setup DTO'ları taşı | 3B, 3C, 3D ile paralel |
| 6 | **3B** Service locator kaldır | 3A, 3C, 3D ile paralel |
| 7 | **3C** LogService kaldır | 3A, 3B, 3D ile paralel |
| 8 | **3D** RetentionDays bug | 3A, 3B, 3C ile paralel |
| 9 | **4A** Cache race condition | Faz 3 bittikten sonra |
| 10 | **5A** EscapeString consolidate | 5B, 5C ile paralel |
| 11 | **5B** CancellationToken ekle | 5A, 5C ile paralel |
| 12 | **5C** SetupOrchestrator | 5A, 5B ile paralel |

---

## Doğrulama Checklist

- [ ] `dotnet build` — hata yok
- [ ] `TestMode: false` → Login'de inputlar boş, hint metni yok
- [ ] `TestMode: false` → TOTP "000000" reddediliyor
- [ ] `TestMode: true` → Login'de test değerleri geliyor, "000000" kabul ediliyor
- [ ] `curl -X DELETE http://localhost:5000/api/logs` → 401 dönüyor
- [ ] `curl http://localhost:5000/api/logs/projects` → 401 dönüyor
- [ ] `curl -X POST http://localhost:5000/api/logs -H "X-API-Key: lfdev_xxx" -d '...'` → 200 dönüyor (log ingestion çalışıyor)
- [ ] Dashboard'da loglar görüntüleniyor
- [ ] Proje CRUD çalışıyor
- [ ] Setup wizard çalışıyor
