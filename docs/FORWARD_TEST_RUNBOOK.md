# BIST Zero-Cost Daily Forward-Testing Runbook

## 1. Architecture & Zero-Cost Operational Model

This runbook defines the daily operational procedures, safety gates, backfill management, and incident response for **BistQuant Zero-Cost Daily Forward-Testing**.

### 1.1 Zero Paid Market Data Guarantees
* **Source of Truth**: Official Borsa İstanbul EOD Daily Equity Bulletins (`bulten_arsiv` / `gunluk_bulten`).
* **Cost**: ₺0.00 / $0.00 (Publicly accessible official closing bulletins).
* **Data Integrity**: Strict CSV v1.14 specification, `EQT` (Equity Market) classification filter, SHA-256 content fingerprinting, and atomic revision tracking (`Rev 0`, `Rev 1+`).
* **Execution Paradigm**: Exact **T+1 EOD Paper Execution**. Sinyal tespiti günün EOD kapanış bülteni ile yapılır; emirler ertesi işlem gününün (T+1) resmi EOD ağırlıklı ortalama/kapanış fiyatından icra edilir. Zaman damgaları `Europe/Istanbul` (UTC+3) takvimine göre yönetilir.

---

## 2. Daily Automated Timetable (UTC+3 / Istanbul)

| Zaman (UTC+3) | Aşama | Bileşen / Servis | Açıklama |
|---|---|---|---|
| **18:10** | Seans Kapanışı | Borsa İstanbul | Normal seans ve kapanış seansı tamamlanır. |
| **18:30 - 18:40** | Bülten Bekleme | `Worker.cs` | BIST EOD bülteni resmi sunucuda yayınlanana kadar periyodik kontrol yapılır. |
| **18:40 - 18:45** | Veri İndirme & Ayrıştırma | `BistDailyBulletinMarketDataProvider` | CSV indirilir, SHA-256 doğrulanır, EQT hisseleri ayrıştırılır ve veritabanına kaydedilir. |
| **18:45 - 18:47** | Evren & Gap Kontrolü | `IMarketDataGapDetector` | Evren kapsama oranı (≥ %95) ve hisse gap analizi doğrulanır. |
| **18:47 - 18:50** | Sinyal Tarama | `MarketScannerService` + `SignalEngine` | Tüm evren (≥220 bar geçmişi olanlar) teknik stratejiler ile taranır. |
| **18:50 - 18:52** | T+1 Emir & İşlem İcrası | `PaperTradingService` | Güvenlik kapılarından geçen sinyaller için T+1 alım/satım emirleri oluşturulur ve portföy güncellenir. |
| **18:52 - 18:55** | Raporlama & Bildirim | `IForwardTestPerformanceService` | Günlük performans raporu üretilir, Telegram özet bülteni iletilir. |

---

## 3. Safety Gates & Circuit Breakers (Koruma Kapıları)

Sistem, hatalı veya eksik veri durumunda **Fail-Closed** prensibiyle çalışır. Herhangi bir kapı ihlalinde trade üretimi durdurulur, ancak piyasa taraması ve teşhis logları açık kalır.

### 3.1 Universe Coverage Gate (Evren Kapsama Eşiği)
* **Kural**: Günlük taranan aktif hisselerin kapsama oranı **≥ %95.0** olmalıdır.
* **Tetiklenme**: Eğer kapsama < %95 ise (örneğin BIST veri sağlayıcısı yarım dosya yayınladıysa), `AutoTradeScanAsync` işlemi durdurur ve `DailyReport` hata kaydı oluşturur.

### 3.2 History Gate (Geçmiş Veri Eşiği)
* **Kural**: Bir hisse için teknik göstergelerin (özellikle EMA200 ve 200 günlük trend filtrelerinin) tam kararlı olabilmesi için en az **220 günlük resmi EOD barı** bulunmalıdır.
* **Tetiklenme**: 220 barın altındaki hisselerin sinyalleri `Watch` / `Candidate` durumuna çekilir, doğrudan `Buy` veya `StrongBuy` işlemine dönüşmez.

### 3.3 Corporate Action Gate (Bedelli / Bedelsiz / Temettü)
* **Kural**: Bülten başlığında veya bülten notlarında sermaye artırımı, temettü veya hisse bölünmesi tespit edilen semboller için fiyat serisi düzeltilinceye kadar işlem açılmaz.
* **Tetiklenme**: Sembol `CorporateActionReview` statüsüne alınır ve alım emirleri engellenir.

### 3.4 Daily Maximum Drawdown Gate (Günlük Maksimum Zarar)
* **Kural**: Bir işlem gününde portföyün toplam günlük çekilmesi (drawdown) **%3.0**'ı aşarsa o gün için yeni alım emri üretimi derhal durdurulur.

### 3.5 Maximum Position & Allocation Limits
* **Maksimum Pozisyon**: Portföyde aynı anda en fazla **10** açık hisse senedi pozisyonu tutulabilir.
* **Sembol Başı Dağılım**: Tek bir hisseye portföyün en fazla **%10.0**'ı tahsis edilir.

### 3.6 Liquidity Floor (Likidite Filtresi)
* **Kural**: Günlük işlem hacmi < 100.000 adet veya işlem tutarı < 10.000.000 TL olan sığ hisselerde slippage riskini önlemek için işlem açılmaz.

---

## 4. Historical Backfill Operations (Geçmiş Veri İndirme)

Geçmiş verilerin Borsa İstanbul arşivinden güvenli ve sunucuyu yormadan çekilmesi için `IBistBulletinBackfillService` kullanılır.

### 4.1 Web Arayüzü Üzerinden Başlatma
* **URL**: `/admin/backfill`
* **Adımlar**:
  1. Başlangıç ve Bitiş tarihlerini seçin (örn: `2024-01-01` - `2026-09-08`).
  2. "Geriye Dönük İndirme İşini Başlat" butonuna tıklayın.
  3. İşlem arka planda 2000 ms (2 saniye) nezaket gecikmesiyle çalışır.
  4. Durum tablosundan canlı ilerleme yüzdesi (`ProgressPercent`), indirilen seanslar ve atlanan günler izlenebilir.
  5. İhtiyaç halinde **Duraklat (Pause)**, **Devam Et (Resume)** veya **İptal Et (Cancel)** butonları kullanılabilir.

### 4.2 REST API ile Kontrol
```bash
# Yeni backfill işi başlatma
curl -X POST http://localhost:5000/api/admin/market-data/backfill \
  -H "Content-Type: application/json" \
  -d '{"startDate":"2025-01-01","endDate":"2026-09-08","overwriteExisting":false,"rateLimitDelayMs":2000}'

# İşi duraklatma
curl -X POST http://localhost:5000/api/admin/market-data/backfill/1/pause

# İşi devam ettirme
curl -X POST http://localhost:5000/api/admin/market-data/backfill/1/resume

# Evren kapsama özetini sorgulama
curl http://localhost:5000/api/admin/market-data/coverage
```

### 4.3 Gap Tespiti ve Teşhisi
`IMarketDataGapDetector` servisi takvim günlerini analiz ederek boşlukları şu kategorilere ayırır:
* `NoTrade`: Resmi tatil veya yarım gün tatili (2026 tam tatil takvimi uygulanır).
* `Suspended`: BIST tarafından tahtası kapatılan veya işleme ara verilen hisse.
* `BulletinMissing`: Resmi arşivde ilgili günün bülteni bulunamadı.
* `ImportFailed`: Bülten ayrıştırılırken format hatası oluştu.

---

## 5. Forward-Testing Portfolio & Performance Audit

İleri test performansını izlemek için `/forward-testing` sayfası kullanılır.

### 5.1 İzlenen Metrikler
* **Portföy Varlığı (Equity)**: Nakit + açık pozisyonların piyasa değeri.
* **Kazanma Oranı (Win Rate %)**: Kârlı kapanan işlemler / toplam kapanan işlemler.
* **Kâr Faktörü (Profit Factor)**: Brüt kâr / brüt zarar.
* **Beklenti (Expectancy)**: Matematiksel işlem başı beklenen ortalama getiri (TL).
* **Maksimum Drawdown (% Max DD)**: Tepe değerden gerçekleşen en büyük sermaye kaybı.
* **Skor Dilimleri**: 90-100, 85-89, 80-84, 75-79, 70-74 aralıklarındaki sinyallerin ayrı ayrı kârlılık dağılımı.
* **Sinyal İzlenebilirliği**: Her `PaperTrade` kaydı, kendisini tetikleyen `SourceSignalId`'ye doğrudan bağlıdır ve denetlenebilir.

### 5.2 Günlük Telegram Yönetici Özeti
Her seans sonunda `Worker.cs` tarafından otomatik oluşturulan ve gönderilen mesaj formatı:
```text
📊 BIST Günlük Forward-Test Raporu
📅 Seans Tarihi: 2026-09-08 (Rev 0)
💼 Portföy Varlığı: ₺114,850.00
💵 Nakit Bakiye: ₺52,400.00
📈 Toplam Getiri: +14.85%
🎯 Kazanma Oranı: %68.8 (22K / 10Z)
🛡️ Maksimum Drawdown: -%2.15
📊 Seans K/Z: +₺1,450.00

🔍 Sinyal & Emir Özeti:
• Taranan Hisse: 512
• Üretilen Sinyal: 6 AL / 2 SAT
• T+1 Emirler: 4 İcra Edildi / 0 İptal
• Güvenlik Kapıları: TÜMÜ GEÇTİ (Coverage %99.2)
```

---

## 6. Incident Response & Troubleshooting

### 6.1 Borsa İstanbul Bülteni Geç Yayınlandığında
* **Belirti**: Saat 18:40'ta bülten indirme 404 döner.
* **İşlem**: `Worker.cs` arka plan servisi 5 dakikalık aralıklarla 21:00'a kadar otomatik sorgulamaya devam eder.
* **Manuel Müdahale**: Bülten yayınlandığında Swagger (`POST /api/market-data/eod-bulletin/daily`) veya `/forward-testing` arayüzündeki "Taramayı & Eşleştirmeyi Tetikle" butonuna basılabilir.

### 6.2 HTTP 429 (Too Many Requests) Alındığında
* **Belirti**: Arşiv indirmelerinde HTTP 429 yanıtı.
* **İşlem**: Sağlayıcı `Retry-After` başlığına tam saygılıdır. `BistBulletinBackfillService` yanıt başlığındaki süre kadar (varsayılan 5 saniye) otomatik bekler ve tekrar dener. Politeness gecikmesini artırmak için `rateLimitDelayMs: 3000` parametresi ile iş başlatılabilir.

### 6.3 Bülten Revizyonu (Düzeltme Bülteni) Yayınlandığında
* **Belirti**: BIST aynı gün için düzeltilmiş bir bülten yayınladığında (Rev 1, Rev 2).
* **İşlem**: `BistDailyBulletinMarketDataProvider` revizyon kontrolü yapar. Yeni revizyon tespit edildiğinde atomik bir veritabanı transaction'ı içerisinde eski bülten `Superseded` durumuna alınır, yeni bülten barları `Current` olarak yazılır.

### 6.4 Veritabanı ve Migrations Doğrulama
* SQLite (Local):
  ```bash
  dotnet ef database update --project src/BistQuant.Infrastructure --startup-project src/BistQuant.API
  ```
* SQL Server (Production):
  Environment değişkeni `DatabaseProvider=SqlServer` ayarlandığında `SqlServerBistDbContext` devreye girer.

---

## 7. Acil İletişim & Sistem Sorumluluğu
* Sistem: `BistQuant Forward-Testing Subsystem`
* Veri Kaynağı: `Borsa İstanbul EOD Bulletin Engine`
* Sürüm: `v1.0-PROD-VERIFIED`
