# LogForDev Roadmap

## Faz 1 - Olmazsa Olmaz (Production-Ready)

- [ ] **Alerting / Bildirimler** - Hata sayisi X'i gecince webhook/email atma. Her rakipte var.
- [ ] **Gercek arama** - Metadata icinde arama, regex, AND/OR operatorleri. Su an sadece ILIKE var.
- [ ] **Health endpoint** - `/health` endpoint'i (monitoring icin standart)
- [ ] **Dashboard login** - Web UI'ya basit authentication (en azindan sifre koruması)
- [ ] **Log export** - CSV/JSON olarak indirme
- [ ] **Environment filtresi** - Dashboard'da prod/staging/dev arasinda gecis

## Faz 2 - Ekosistem Entegrasyonu

- [ ] **OpenTelemetry (OTLP) desteği** - Endustri standardi. Tum OTEL SDK'lari ile calisir.
- [ ] **Serilog sink** - .NET icin sifir surtunme entegrasyon
- [ ] **Diger SDK'lar** - Python, Node.js, Go icin minimal SDK
- [ ] **Swagger/OpenAPI** - API dokumantasyonu otomatik
- [ ] **Syslog input** - Altyapi bilesenleri icin log toplama

## Faz 3 - Guc Ozellikleri

- [ ] **Kaydedilmis sorgular** - Sik kullanilan filtreleri kaydet
- [ ] **Trace correlation** - TraceId/SpanId ile iliskili loglari goster (veri modelde zaten var, kullanilmiyor)
- [ ] **Log pattern detection** - Benzer loglari grupla, gurultuyu azalt
- [ ] **Zaman serisi grafik** - Log hacmi zaman icinde gorsellestirilsin
- [ ] **Bildirim kanallari** - Slack, Discord, PagerDuty, email webhook
- [ ] **Rate limiting** - API'yi asiri yukten koru
- [ ] **Log context** - Bir logun oncesi/sonrasi (ayni trace icinde)

## Faz 4 - Enterprise

- [ ] **Multi-tenancy** - Takimlar/musteriler arasi veri izolasyonu
- [ ] **RBAC** - Rol bazli erisim kontrolu (admin, viewer, app-specific)
- [ ] **SSO / OIDC** - Kurumsal SSO desteği
- [ ] **HA / Clustering** - Yatay olceklendirme
- [ ] **S3/MinIO arsivleme** - Eski loglari ucuz depolamaya tasi
- [ ] **Log sampling** - Yuksek hacimli loglari akilli ornekleme
- [ ] **Pipeline processing** - Loglari yazma oncesi donusturme/zenginlestirme
- [ ] **Compliance / Audit** - Kim neye ne zaman eristiginin kaydi

## Stratejik Konum

LogForDev'in farki **basitlik**:
- Graylog = Elasticsearch + MongoDB
- Loki = Grafana + Alloy
- SigNoz = Kubernetes gerektirir
- **LogForDev = Tek Docker container, 5 dakika kurulum**

Hedef: Gelistiricilerin ihtiyacinin %80'ini karsilayan, kurulumu 5 dakika suren, self-hosted, ucretsiz log sistemi.

## Rakipler

| Ozellik | Loki | Graylog | Seq | SigNoz | LogForDev |
|---------|------|---------|-----|--------|-----------|
| Kurulum | Zor | Orta | Kolay | Zor | Cok kolay |
| Query dili | LogQL | Lucene | Seq filter | SQL | Basit filtre |
| Alerting | Var | Var | Var | Var | Yok |
| OTLP | Alloy ile | Plugin | Native | Native | Yok |
| SDK | Promtail | GELF | .NET, Node | OTel | Yok |
| Fiyat | Ucretsiz | Ucretsiz* | Ucretli | Ucretsiz | Ucretsiz |
| Backend | Kendi | ES+Mongo | Kendi | ClickHouse | ClickHouse |
