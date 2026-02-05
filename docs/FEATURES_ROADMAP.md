# LogForDev - Yeni Özellikler Roadmap

Bu dokümanda eklenecek özelliklerin detaylı planı yer almaktadır.

---

## 1. Swagger/OpenAPI

**Durum:** ✅ Tamamlandı
**Zorluk:** Kolay
**Tahmini Dosyalar:** `Program.cs`, yeni `LogForDev.csproj` dependency

### Ne Yapılacak?
- `Swashbuckle.AspNetCore` NuGet paketi eklenecek
- Program.cs'e Swagger servisleri eklenecek
- API endpoint'lerine XML dokümantasyon yorumları eklenecek
- `/swagger` adresi aktif edilecek

### Sonuç
```
http://localhost:5000/swagger
```
Tarayıcıdan tüm API'ler test edilebilir, request/response modelleri görünür.

---

## 2. URL State (Filter Persistence)

**Durum:** ✅ Tamamlandı
**Zorluk:** Kolay
**Tahmini Dosyalar:** `Views/Home/Index.cshtml` (JavaScript kısmı)

### Ne Yapılacak?
- Filtre değiştiğinde URL query string güncellenir
- Sayfa yüklendiğinde URL'den filtreler okunur
- Browser back/forward butonları çalışır

### Örnek URL'ler
```
http://localhost:5000/?level=Error&app=auth-service&page=2
http://localhost:5000/?search=database&from=2024-01-01&to=2024-01-31
http://localhost:5000/?traceId=abc-123
```

### Sonuç
- Filtrelenmiş log görünümünü link olarak paylaşabilirsin
- Bookmark yapabilirsin
- Browser history ile geri gidebilirsin

---

## 3. Keyboard Shortcuts Modal

**Durum:** ✅ Tamamlandı
**Zorluk:** Kolay
**Tahmini Dosyalar:** `Views/Home/Index.cshtml`, `Views/Shared/_Layout.cshtml`

### Ne Yapılacak?
- `?` tuşuna basınca shortcuts modal açılır
- Mevcut shortcuts görünür hale gelir
- Yeni shortcuts eklenir

### Mevcut Shortcuts (Gizli)
| Tuş | Aksiyon |
|-----|---------|
| `←` | Önceki sayfa |
| `→` | Sonraki sayfa |
| `r` | Yenile |
| `Esc` | Modal kapat |

### Yeni Shortcuts
| Tuş | Aksiyon |
|-----|---------|
| `?` | Shortcuts modal aç |
| `/` | Search input'a focus |
| `l` | Live mode toggle |
| `c` | Filtreleri temizle |
| `1-5` | Log level filtresi (1=Trace, 5=Fatal) |

### Sonuç
Kullanıcılar keyboard ile hızlı navigasyon yapabilir.

---

## 4. API Docs İyileştirme

**Durum:** ✅ Tamamlandı
**Zorluk:** Orta
**Tahmini Dosyalar:** `Views/Home/Docs.cshtml`

### Ne Yapılacak?

#### 4.1 Error Response Örnekleri
Her endpoint için başarısız response örnekleri:
```json
// 401 Unauthorized
{ "success": false, "error": "Invalid API key" }

// 400 Bad Request
{ "success": false, "error": "Invalid log level. Valid: Trace, Debug, Info, Warning, Error, Fatal" }

// 500 Internal Server Error
{ "success": false, "error": "Internal server error" }
```

#### 4.2 Tam SDK Örnekleri
Her dil için production-ready kod:
- Error handling
- Retry logic
- Async/await kullanımı
- Batch gönderme best practices
- Connection pooling

#### 4.3 Rate Limiting Bilgisi
- Limit var mı? Kaç request/dakika?
- Rate limit aşılınca ne olur?

#### 4.4 Interactive API Playground
- Docs sayfasında "Try it" butonu
- Gerçek API çağrısı yapabilme
- Response görme

### Sonuç
Yeni kullanıcılar 5 dakikada entegrasyon yapabilir.

---

## 5. Log Aggregation (Pattern Grouping)

**Durum:** ✅ Tamamlandı
**Zorluk:** Orta
**Tahmini Dosyalar:** Yeni `LogAggregationService.cs`, `LogRepository.cs`, `Views/Home/Index.cshtml`

### Ne Yapılacak?

#### 5.1 Backend
- Benzer log mesajlarını gruplayan algoritma
- ClickHouse aggregation query'leri
- Yeni API endpoint: `GET /api/logs/patterns`

#### 5.2 Aggregation Mantığı
```
Input Logs:
- "Connection failed to db-server-1: timeout"
- "Connection failed to db-server-2: timeout"
- "Connection failed to db-server-1: refused"
- "User login successful: user@email.com"
- "User login successful: admin@company.com"

Output Patterns:
┌────────────────────────────────────┬───────┬────────────┐
│ Pattern                            │ Count │ Last Seen  │
├────────────────────────────────────┼───────┼────────────┤
│ Connection failed to *: *          │ 3     │ 2 min ago  │
│ User login successful: *           │ 2     │ 5 min ago  │
└────────────────────────────────────┴───────┴────────────┘
```

#### 5.3 UI
- "Patterns" tab veya toggle
- Pattern'e tıklayınca o gruptaki logları göster
- Occurrence count badge

### Sonuç
Aynı hatanın 1000 kez tekrarlandığını tek satırda görürsün.

---

## 6. Trace Correlation View

**Durum:** ✅ Tamamlandı
**Zorluk:** Orta
**Tahmini Dosyalar:** Yeni `Views/Home/TraceView.cshtml` veya modal, `LogRepository.cs`

### Ne Yapılacak?

#### 6.1 Trace Timeline View
Bir TraceId'ye ait tüm logları timeline'da göster:

```
TraceId: abc-123-def-456
Request Flow:

10:00:00.000 ─┬─ [api-gateway]     INFO    Request received: POST /api/orders
              │
10:00:00.050 ─┼─ [auth-service]    DEBUG   Validating token
              │
10:00:00.120 ─┼─ [auth-service]    INFO    Token valid, user: 123
              │
10:00:00.200 ─┼─ [order-service]   INFO    Creating order
              │
10:00:00.350 ─┼─ [order-service]   DEBUG   Checking inventory
              │
10:00:00.500 ─┼─ [payment-service] INFO    Processing payment
              │
10:00:00.800 ─┼─ [payment-service] ERROR   Payment failed: insufficient funds
              │
10:00:00.850 ─┴─ [api-gateway]     WARNING Request completed with error

Total Duration: 850ms
Services: 4
Errors: 1
```

#### 6.2 UI Elements
- Log satırında "View Trace" butonu (traceId varsa)
- Trace modal veya ayrı sayfa
- Service-to-service flow visualization
- Duration hesaplama
- Error highlighting

### Sonuç
Bir request'in tüm microservice'lerden geçişini görebilirsin.

---

## 7. UI Component Refactor

**Durum:** ✅ Tamamlandı (Temel yapı)
**Zorluk:** Zor
**Tahmini Dosyalar:** Tüm `Views/`, yeni `wwwroot/js/` dosyaları

### Ne Yapılacak?

#### 7.1 JavaScript Modülleri
Mevcut monolitik yapıyı ayır:

```
wwwroot/js/
├── components/
│   ├── LogTable.js        # Tablo render, row expand
│   ├── LogFilters.js      # Filter yönetimi
│   ├── Pagination.js      # Sayfalama
│   ├── LogModal.js        # Detail modal
│   ├── Toast.js           # Notification sistemi
│   └── BulkActions.js     # Seçim ve export
├── services/
│   ├── ApiService.js      # API çağrıları
│   └── UrlStateService.js # URL yönetimi
├── utils/
│   ├── formatters.js      # Tarih, level badge vb.
│   └── helpers.js         # Genel utility'ler
└── app.js                 # Ana orchestration
```

#### 7.2 CSS Organizasyonu
```
wwwroot/css/
├── components/
│   ├── table.css
│   ├── filters.css
│   ├── modal.css
│   └── pagination.css
└── main.css
```

#### 7.3 Partial Views
```
Views/Home/
├── Index.cshtml
├── _LogTable.cshtml
├── _Filters.cshtml
├── _Pagination.cshtml
└── _LogModal.cshtml
```

### Sonuç
- Daha kolay maintenance
- Kod tekrarı azalır
- Test edilebilir yapı
- Yeni özellik eklemek kolaylaşır

---

## Öncelik Sırası

```
Hafta 1: Swagger + URL State + Keyboard Shortcuts
Hafta 2: API Docs İyileştirme
Hafta 3: Log Aggregation
Hafta 4: Trace Correlation View
Hafta 5-6: UI Component Refactor
```

---

## Tamamlanan Özellikler

| Özellik | Tarih | Açıklama |
|---------|-------|----------|
| Swagger/OpenAPI | 2024 | `/swagger` endpoint, API dokümantasyonu |
| URL State | 2024 | Filtreler URL'de saklanır, paylaşılabilir |
| Keyboard Shortcuts | 2024 | `?` ile help modal, `/` ile arama focus |
| API Docs İyileştirme | 2024 | Error responses, SDK örnekleri, Try It |
| Log Aggregation | 2024 | Patterns tab, benzer logları grupla |
| Trace Correlation | 2024 | TraceId timeline view |
| UI Refactor | 2024 | Temel yapı: utils.js, dashboard.css |

---

*Son güncelleme: 2024*
