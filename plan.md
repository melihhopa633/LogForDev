# LogForDev - Guvenlik Analizi ve Iyilestirme Plani

> **Tarih:** 2026-02-22
> **Kapsam:** Tum kaynak kod, yapilandirma dosyalari, Docker ayarlari

---

## OZET

Toplam **23 guvenlik acigi** tespit edildi. Asagida 1'den 23'e kadar numaralandirilmistir.

> **UYARI:** Bu proje self-host loglama sistemidir. Loglar kullanicinin mahrem verileridir.
> Asagidaki aciklar, yetkisiz kisilerin bu verilere erisimini mumkun kilabilir.

---

## MASTER CHECKLIST

| # | Acik | Seviye | Faz | Durum |
|---|---|---|---|---|
| 1 | SQL Injection - Tum Veritabani Katmani | KRITIK | 2 | [ ] |
| 2 | Varsayilan Kimlik Bilgileri (admin/admin) | KRITIK | 1 | [ ] |
| 3 | TOTP Test Modu Bypass (000000) | KRITIK | 1 | [ ] |
| 4 | API Key Loglarda Acik Metin | KRITIK | 1 | [ ] |
| 5 | Login Rate Limiting Yok | YUKSEK | 2 | [ ] |
| 6 | GET endpoint'lerini DashboardOnly yap (API key ile sadece POST) | YUKSEK | 2 | [ ] |
| 7 | Zayif Sifre Politikasi | YUKSEK | 2 | [ ] |
| 8 | HTTPS Destegi ve Rehberlik | YUKSEK | 3 | [ ] |
| 9 | AllowedHosts Wildcard | YUKSEK | 1 | [ ] |
| 10 | Guvenlik Header'lari Eksik | YUKSEK | 1 | [ ] |
| 11 | Setup Endpoint'leri Korumasiz | YUKSEK | 1 | [ ] |
| 12 | CSRF Korumasi Yok | ORTA | 3 | [ ] |
| 13 | Timing Saldirisi - Kullanici Enumerasyonu | ORTA | 2 | [ ] |
| 14 | RememberMe Cookie 30 Gun | ORTA | 3 | [ ] |
| 15 | Hata Mesajlarinda Bilgi Sizintisi | ORTA | 1 | [ ] |
| 16 | Pagination Siniri Yok (DoS) | ORTA | 3 | [ ] |
| 17 | TOTP Zaman Penceresi Genis | ORTA | 3 | [ ] |
| 18 | Silinen API Key 5dk Gecerli | ORTA | 3 | [ ] |
| 19 | Email Adresleri Loglarda Acik | ORTA | 3 | [ ] |
| 20 | Stack Trace Bilgi Sizintisi | ORTA | 3 | [ ] |
| 21 | API Key Format Tutarsizligi | DUSUK | 4 | [ ] |
| 22 | Input Validation Eksik | DUSUK | 4 | [ ] |
| 23 | User Creation Race Condition | DUSUK | 4 | [ ] |

---

## DETAYLI ACIKLAMALAR

---

### MADDE 1 - SQL Injection - Tum Veritabani Katmani
**Seviye:** KRITIK | **Faz:** 2

**Dosyalar:**
- `src/LogForDev/Services/UserService.cs:82-84, 149-154, 181-187, 225-230, 254`
- `src/LogForDev/Services/ProjectService.cs:52-54, 90-91, 120, 144`
- `src/LogForDev/Data/LogRepository.cs:178, 185-189, 199, 201, 265`
- `src/LogForDev/Data/ClickHouseStringHelper.cs:5-13`

**Sorun:**
Tum SQL sorgulari string interpolation ile olusturuluyor. Parameterized query kullanilmiyor.

```csharp
// UserService.cs:149 - GUVENLI DEGIL
var sql = $"SELECT ... FROM users WHERE email = '{email.Replace("'", "''")}'";

// LogRepository.cs:178 - GUVENLI DEGIL
WHERE timestamp > now() - INTERVAL {query.Hours} HOUR

// LogRepository.cs:185-189 - GUVENLI DEGIL (Levels escape edilmiyor)
var quotedLevels = string.Join(",", levelList.Select(l => $"'{l}'"));
sql += $" AND level IN ({quotedLevels})";
```

Ayrica `ClickHouseStringHelper.Escape()` yanlis escape yontemi kullaniyor (`\'` yerine `''` olmali).

**Yapilacak:**
- [ ] 1a. Tum SQL sorgularini parameterized query'ye cevir
- [ ] 1b. ClickHouse.Client'in `AddParameter()` metodunu kullan
- [ ] 1c. `ClickHouseStringHelper.Escape()` fonksiyonunu duzelt veya kaldir
- [ ] 1d. Integer parametreleri (Hours, Limit, MinCount) icin dogrulama ekle
- [ ] 1e. Levels parametresini whitelist'e gore dogrula

```csharp
// OLMASI GEREKEN:
var cmd = new ClickHouseCommand(connection);
cmd.CommandText = "SELECT ... FROM users WHERE email = {email:String}";
cmd.Parameters.Add(new ClickHouseParameter { ParameterName = "email", Value = email });
```

---

### MADDE 2 - Varsayilan Kimlik Bilgileri ile Tam Bypass
**Seviye:** KRITIK | **Faz:** 1

**Dosyalar:**
- `src/LogForDev/appsettings.json:19-20` (ClickHouse: admin/admin)
- `src/LogForDev/appsettings.json:24` (TestMode: true)
- `docker-compose.yml:16-17, 35-36`

**Sorun:**
Varsayilan ayarlarla: **email: admin, sifre: admin, TOTP: 000000** ile tam yetkili giris yapilabilir.

**Yapilacak:**
- [ ] 2a. ClickHouse sifrelerini environment variable'dan oku
- [ ] 2b. `docker-compose.yml`'da `.env` dosyasi kullan, `.gitignore`'a ekle
- [ ] 2c. Setup wizard'da guclu sifre zorunlulugu ekle
- [ ] 2d. README'de production icin sifre degistirme adimlarini belgele

---

### MADDE 3 - TOTP Test Modu Bypass
**Seviye:** KRITIK | **Faz:** 1

**Dosyalar:**
- `src/LogForDev/Services/UserService.cs:294-298`
- `src/LogForDev/Views/Auth/Login.cshtml:275-281`
- `src/LogForDev/appsettings.json:24`

**Sorun:**
TestMode aktifken `000000` koduyla giris mumkun. Login sayfasi bu kodu otomatik dolduruyor. 2FA tamamen etkisiz.

```csharp
// UserService.cs:294-298
if (_options.TestMode && code == "000000")
{
    return true; // 2FA bypass!
}
```

**Yapilacak:**
- [ ] 3a. `appsettings.json`'da `TestMode` varsayilani `false` yap
- [ ] 3b. TestMode'u `#if DEBUG` ile sarmalayarak production build'den cikar
- [ ] 3c. Login.cshtml'den auto-fill kodunu kaldir veya `#if DEBUG` ekle
- [ ] 3d. TestMode aktifse uygulama baslatildiginda konsola UYARI yaz

---

### MADDE 4 - API Key Loglarda Acik Metin
**Seviye:** KRITIK | **Faz:** 1

**Dosyalar:**
- `src/LogForDev/Authentication/ApiKeyAuthenticationHandler.cs:35-38`
- `src/LogForDev/Middleware/RequestLoggingMiddleware.cs:23, 68`

**Sorun:**
API key query string'den kabul ediliyor (`?apiKey=lfdev_xxx`). RequestLoggingMiddleware tam URL'yi logluyor. API key veritabanina acik metin olarak yaziliyor.

**Yapilacak:**
- [ ] 4a. Query string'den API key kabulunu tamamen kaldir
- [ ] 4b. Sadece `X-API-Key` header'i kabul et
- [ ] 4c. RequestLoggingMiddleware'da query string'i filtrele/maskele
- [ ] 4d. Mevcut loglardaki API key'leri temizle

---

### MADDE 5 - Login Rate Limiting Yok
**Seviye:** YUKSEK | **Faz:** 2

**Dosya:** `src/LogForDev/Controllers/AuthController.cs:41-98`

**Sorun:** Brute-force korumasiz. Account lockout 5 denemede 15dk - ama IP bazli sinir yok.

**Yapilacak:**
- [ ] 5a. ASP.NET Core built-in `AddRateLimiter` middleware ekle
- [ ] 5b. Login endpoint'ine IP bazli rate limit (5 deneme/15dk)
- [ ] 5c. Account lockout kademeli artir (15dk -> 30dk -> 1sa -> 24sa)
- [ ] 5d. Basarisiz giris denemelerini IP ile birlikte logla

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(15);
    });
});

// AuthController.cs
[EnableRateLimiting("login")]
[HttpPost("login")]
public async Task<IActionResult> Login(...)
```

---

### MADDE 6 - Projeler Arasi Log Erisimi (IDOR)
**Seviye:** YUKSEK | **Faz:** 2

**Dosya:** `src/LogForDev/Controllers/LogsController.cs:96-203`

**Sorun:**
API key ile auth sonrasi tum GET endpoint'lerine erisilebiliyor. Dis uygulama sadece log yazmali,
okumamalı. Simdi ise Proje A'nin key'i ile tum loglari (Proje B dahil) okuyabiliyorsun.

**Karar: Secenek A - GET endpoint'lerini DashboardOnly yap.**
Dis uygulamanin log OKUMASINA gerek yok. Log yazar, dashboard'dan okursun.

**Yapilacak:**
- [ ] 6a. `GetLogs` endpoint'ine `[Authorize(Policy = "DashboardOnly")]` ekle
- [ ] 6b. `GetStats` endpoint'ine `[Authorize(Policy = "DashboardOnly")]` ekle
- [ ] 6c. `GetApps` endpoint'ine `[Authorize(Policy = "DashboardOnly")]` ekle
- [ ] 6d. `GetEnvironments` endpoint'ine `[Authorize(Policy = "DashboardOnly")]` ekle
- [ ] 6e. `GetPatterns` endpoint'ine `[Authorize(Policy = "DashboardOnly")]` ekle
- [ ] 6f. `GetTraceTimeline` endpoint'ine `[Authorize(Policy = "DashboardOnly")]` ekle

Sonuc: API key ile SADECE log yazma (POST) acik kalir, geri kalan her sey dashboard login gerektirir.

```
API Key ile yapilabilecekler (degisiklik sonrasi):
  POST /api/logs         → Log yaz        ✓ (tek amaci bu)
  POST /api/logs/batch   → Toplu log yaz  ✓ (tek amaci bu)
  GET  /api/logs         → ENGELLENDI     ✗ (DashboardOnly)
  GET  /api/logs/stats   → ENGELLENDI     ✗ (DashboardOnly)
  GET  /api/logs/apps    → ENGELLENDI     ✗ (DashboardOnly)
  GET  /api/logs/patterns → ENGELLENDI    ✗ (DashboardOnly)
  ...diger GET'ler       → ENGELLENDI     ✗ (DashboardOnly)
```

```csharp
// LogsController.cs - ONCE (guvenli degil):
[HttpGet]
public async Task<ActionResult<PagedResult<LogEntry>>> GetLogs(...)

// LogsController.cs - SONRA (guvenli):
[HttpGet]
[Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
public async Task<ActionResult<PagedResult<LogEntry>>> GetLogs(...)
```

---

### MADDE 7 - Zayif Sifre Politikasi
**Seviye:** YUKSEK | **Faz:** 2

**Dosya:** `src/LogForDev/Controllers/AuthController.cs:115-116`

**Sorun:** Sadece 8 karakter minimum. Karmasiklik kontrolu yok.

**Yapilacak:**
- [ ] 7a. Minimum 12 karakter
- [ ] 7b. En az 1 buyuk, 1 kucuk, 1 rakam, 1 ozel karakter zorunlu
- [ ] 7c. Yaygin sifreleri reddet (top 10000 listesi)
- [ ] 7d. Setup wizard'da da ayni kurallar gecerli olsun

```csharp
private bool IsPasswordStrong(string password)
{
    if (password.Length < 12) return false;
    if (!password.Any(char.IsUpper)) return false;
    if (!password.Any(char.IsLower)) return false;
    if (!password.Any(char.IsDigit)) return false;
    if (!password.Any(c => !char.IsLetterOrDigit(c))) return false;
    return true;
}
```

---

### MADDE 8 - HTTPS Destegi ve Rehberlik
**Seviye:** YUKSEK | **Faz:** 3

**Dosyalar:**
- `src/LogForDev/Program.cs:112`
- `docker-compose.yml:11-12`

**Sorun:** HTTP varsayilan. Ancak self-hosted sistemde localhost kullanimda HTTPS zorlamak sorun yaratir.

**Gercekci Yaklasim:**

| Erisim Sekli | Guvenlik | Cozum |
|---|---|---|
| `localhost` | Trafik makineyi terk etmez | HTTP yeterli |
| LAN (`192.168.x.x`) | Sniff edilebilir | Istege bagli reverse proxy |
| Internet'e acik | TEHLIKELI | Zorunlu reverse proxy + Let's Encrypt |

**Yapilacak:**
- [ ] 8a. Cookie `Secure` flag'ini `SameAsRequest`'e cevir (localhost'ta HTTP'de calissin)
- [ ] 8b. `docker-compose.prod.yml` + `Caddyfile` ornegi sun (otomatik HTTPS)
- [ ] 8c. Uygulama baslatildiginda production + HTTP ise konsola UYARI ver
- [ ] 8d. HSTS header'i sadece HTTPS aktifken ekle

```yaml
# docker-compose.prod.yml
caddy:
  image: caddy:2
  ports:
    - "443:443"
    - "80:80"
  volumes:
    - ./Caddyfile:/etc/caddy/Caddyfile
```
```
# Caddyfile (tek satir - otomatik Let's Encrypt)
logs.senindomain.com {
    reverse_proxy logfordev:5000
}
```

---

### MADDE 9 - AllowedHosts Wildcard
**Seviye:** YUKSEK | **Faz:** 1

**Dosya:** `src/LogForDev/appsettings.json:14`

**Sorun:** `"AllowedHosts": "*"` - Host header injection'a acik.

**Yapilacak:**
- [ ] 9a. Production icin spesifik hostname tanimla
- [ ] 9b. Development icin `appsettings.Development.json`'da `*` kalabilir

---

### MADDE 10 - Guvenlik Header'lari Eksik
**Seviye:** YUKSEK | **Faz:** 1

**Dosya:** `src/LogForDev/Program.cs`

**Sorun:** CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy header'lari yok.

**Yapilacak:**
- [ ] 10a. Security headers middleware'i ekle

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'");
    await next();
});
```

---

### MADDE 11 - Setup Endpoint'leri Korumasiz
**Seviye:** YUKSEK | **Faz:** 1

**Dosya:** `src/LogForDev/Controllers/SetupController.cs`

**Sorun:** Setup sonrasi `/api/setup/*` hala erisilebilir. `[Authorize]` yok. Saldirgan admin sifresi degistirebilir.

**Yapilacak:**
- [ ] 11a. Tum setup endpoint'lerine setup tamamlanmissa 403 donduren kontrol ekle
- [ ] 11b. Setup tamamlandiktan sonra endpoint'leri tamamen devre disi birak

```csharp
[HttpPost("test-connection")]
public async Task<IActionResult> TestConnection(...)
{
    if (await IsSetupComplete())
        return Forbid("Setup zaten tamamlandi");
    // ...
}
```

---

### MADDE 12 - CSRF Korumasi Yok
**Seviye:** ORTA | **Faz:** 3

**Dosyalar:** Tum state-degistiren endpoint'ler (POST/PUT/DELETE)

**Sorun:** `[ValidateAntiForgeryToken]` hicbir endpoint'te yok.

**Yapilacak:**
- [ ] 12a. Tum POST/PUT/DELETE endpoint'lerine `[ValidateAntiForgeryToken]` ekle
- [ ] 12b. API endpoint'leri icin SameSite cookie + custom header kontrolu kullan
- [ ] 12c. Razor view'larda `@Html.AntiForgeryToken()` ekle

---

### MADDE 13 - Timing Saldirisi ile Kullanici Enumerasyonu
**Seviye:** ORTA | **Faz:** 2

**Dosya:** `src/LogForDev/Services/UserService.cs:107-127`

**Sorun:** Kullanici yoksa hemen donus (~0ms), varsa BCrypt (~100ms). Zaman farki kullanici varligini ele verir.

**Yapilacak:**
- [ ] 13a. Kullanici bulunamadiginda da dummy BCrypt dogrulamasi yap

```csharp
if (user == null)
{
    // Zamanlamayi esitle
    BCrypt.Net.BCrypt.Verify(password, "$2a$11$xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
    return null;
}
```

---

### MADDE 14 - RememberMe Cookie 30 Gun
**Seviye:** ORTA | **Faz:** 3

**Dosya:** `src/LogForDev/Controllers/AuthController.cs:78-87`

**Sorun:** Cookie calinirsa 30 gun boyunca gecerli. Server-side session dogrulamasi yok.

**Yapilacak:**
- [ ] 14a. RememberMe suresini 7 gune dusur
- [ ] 14b. Server-side session token ekle (veritabaninda sakla)
- [ ] 14c. Logout'ta session'i invalidate et
- [ ] 14d. Sifre degistiginde tum session'lari iptal et

---

### MADDE 15 - Hata Mesajlarinda Bilgi Sizintisi
**Seviye:** ORTA | **Faz:** 1

**Dosya:** `src/LogForDev/Controllers/SetupController.cs:70-73`

**Sorun:** Exception mesajlari dogrudan kullaniciya dondurulyor. Sistem bilgileri aciliga cikar.

**Yapilacak:**
- [ ] 15a. Exception mesajlarini genel mesajla degistir, detayi sadece logla

```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Setup connection test failed");
    return Ok(new { success = false, message = "Baglanti hatasi olustu." });
}
```

---

### MADDE 16 - Pagination Siniri Yok (DoS)
**Seviye:** ORTA | **Faz:** 3

**Dosya:** `src/LogForDev/Models/LogModels.cs:168-169`

**Sorun:** `pageSize=999999999` ile bellek tuketme mumkun.

**Yapilacak:**
- [ ] 16a. PageSize'i `Math.Clamp(value, 1, 500)` ile sinirla

```csharp
private int _pageSize = 50;
public int PageSize
{
    get => _pageSize;
    set => _pageSize = Math.Clamp(value, 1, 500);
}
```

---

### MADDE 17 - TOTP Zaman Penceresi Genis
**Seviye:** ORTA | **Faz:** 3

**Dosya:** `src/LogForDev/Services/UserService.cs:301-304`

**Sorun:** +-1 adim = 90 saniye pencere. Replay attack riski.

**Yapilacak:**
- [ ] 17a. `VerificationWindow(1, 0)` kullan (mevcut + bir onceki adim = 60sn)

---

### MADDE 18 - Silinen API Key 5dk Gecerli
**Seviye:** ORTA | **Faz:** 3

**Dosya:** `src/LogForDev/Services/ProjectService.cs:24, 42-46`

**Sorun:** In-memory cache 5dk TTL. Silinen key cache'te kalir.

**Yapilacak:**
- [ ] 18a. API key silindiginde cache'i invalidate et
- [ ] 18b. Cache TTL'i 1dk'ya dusur

---

### MADDE 19 - Email Adresleri Loglarda Acik
**Seviye:** ORTA | **Faz:** 3

**Dosya:** `src/LogForDev/Services/UserService.cs:109, 124, 132`

**Sorun:** Basarisiz giris denemelerinde email acik loglanyor.

**Yapilacak:**
- [ ] 19a. Email'i maskele: `m***@example.com`

---

### MADDE 20 - Stack Trace Bilgi Sizintisi
**Seviye:** ORTA | **Faz:** 3

**Dosya:** `src/LogForDev/Middleware/RequestLoggingMiddleware.cs:34-42`

**Sorun:** Exception stack trace'leri dosya yollarini ve ic yapiyi aciliga cikarir.

**Yapilacak:**
- [ ] 20a. Production'da stack trace'i sadece internal loglara yaz, API response'a ekleme

---

### MADDE 21 - API Key Format Tutarsizligi
**Seviye:** DUSUK | **Faz:** 4

**Dosyalar:**
- `LogsController.cs:231` -> `lfdev_` prefix
- `SetupController.cs:87` -> `lfd_` prefix

**Yapilacak:**
- [ ] 21a. Tek prefix belirle (`lfdev_`), tek yardimci metod olustur

---

### MADDE 22 - Input Validation Eksik
**Seviye:** DUSUK | **Faz:** 4

**Dosyalar:** AuthController, SetupController

**Sorun:** Email format kontrolu yetersiz, hostname/port dogrulamasi yok.

**Yapilacak:**
- [ ] 22a. Regex ile email dogrulama ekle
- [ ] 22b. Hostname/port format kontrolu ekle

---

### MADDE 23 - User Creation Race Condition
**Seviye:** DUSUK | **Faz:** 4

**Dosya:** `src/LogForDev/Services/UserService.cs:60-68`

**Sorun:** Ayni email ile esanli kayit olusturulabilir.

**Yapilacak:**
- [ ] 23a. Veritabaninda UNIQUE constraint ekle

---

## UYGULAMA FAZLARI

### FAZ 1 - ACIL (Bu Hafta)
> Minimum eforla maksimum guvenlik kazanimi. Cogu 1-15 satir degisiklik.

| Madde | Is | Dosya |
|---|---|---|
| 3a | TestMode varsayilani `false` yap | appsettings.json |
| 4a | Query string API key kabulunu kaldir | ApiKeyAuthenticationHandler.cs |
| 4b | Sadece X-API-Key header kabul et | ApiKeyAuthenticationHandler.cs |
| 11a | Setup endpoint'lerine yetki kontrolu ekle | SetupController.cs |
| 10a | Guvenlik header middleware ekle | Program.cs |
| 15a | Hata mesajlarini genellestir | SetupController.cs |
| 9a | AllowedHosts'u ayarla | appsettings.json |
| 2b | docker-compose .env kullan | docker-compose.yml |
| 3d | TestMode aktifse konsol uyarisi | Program.cs |

### FAZ 2 - KISA VADE (1-2 Hafta)
> SQL injection ve erisim kontrolu - en buyuk teknik is burada.

| Madde | Is | Dosya |
|---|---|---|
| 1a-1e | Tum SQL parameterized yap (~30 sorgu) | UserService, ProjectService, LogRepository |
| 5a-5d | Rate limiting ekle | Program.cs, AuthController.cs |
| 6a-6f | GET endpoint'lerine DashboardOnly ekle (6 endpoint) | LogsController.cs |
| 7a-7d | Sifre politikasi gucllendir | AuthController, SetupController |
| 13a | Timing attack onlemi | UserService.cs |

### FAZ 3 - ORTA VADE (2-4 Hafta)
> Derinlemesine guvenlik iyilestirmeleri.

| Madde | Is | Dosya |
|---|---|---|
| 12a-12c | CSRF korumasi | Tum controller + view'lar |
| 14a-14d | Server-side session yonetimi | AuthController, yeni SessionService |
| 8a-8d | HTTPS rehberlik + Caddy ornegi | Program.cs, docker-compose.prod.yml |
| 18a-18b | Cache invalidation | ProjectService.cs |
| 19a | Log maskeleme (email) | UserService.cs |
| 16a | Pagination limitleri | LogModels.cs |
| 17a | TOTP penceresi daralt | UserService.cs |
| 20a | Stack trace filtreleme | RequestLoggingMiddleware.cs |

### FAZ 4 - UZUN VADE (Sonraki Surumler)
> Yeni ozellik gerektiren iyilestirmeler.

| Madde | Is |
|---|---|
| 21a | API key format birlestir |
| 22a-22b | Input validation genislet |
| 23a | UNIQUE constraint ekle |
| - | Sifre sifirlama mekanizmasi |
| - | TOTP yedek kodlari |
| - | Guvenlik olay alertleri |
| - | API key rotasyonu |
| - | Audit log sistemi |

---

## EN ACIL 3 ADIM (BUGUN YAPILABILIR)

1. **Madde 3a** - `appsettings.json` -> `"TestMode": false` (1 satir, 2FA bypass kapanir)
2. **Madde 4a** - `ApiKeyAuthenticationHandler.cs` -> query string API key'i kaldir (3 satir)
3. **Madde 11a** - `SetupController.cs` -> setup sonrasi endpoint'leri kilitle (10 satir)
