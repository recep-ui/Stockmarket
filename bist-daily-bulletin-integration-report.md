# BIST Quant Scanner – BIST Günlük Bülten Entegrasyonu ve Üretime Hazırlık Raporu
## (BIST Daily Bulletin Integration & Production Readiness Report)

**Tarih:** 08 Eylül 2026  
**Kapsam:** Borsa İstanbul Pay Piyasası Resmi Günlük Bülten (EOD) Entegrasyonu, T+1 İleriye Dönük Simülasyon, Takvim & Ayrıştırma Doğrulaması ve Nihai Sistem Onayı  
**Depo:** [https://github.com/recep-ui/Stockmarket.git](https://github.com/recep-ui/Stockmarket.git)  

---

## 1. YÖNETİCİ ÖZETİ VE NİHAİ KARAR

Bu sprint kapsamında, BIST Quant Scanner platformundaki sentetik `MockMarketDataProvider` sağlayıcısı, Borsa İstanbul Pay Piyasası'nın resmi olarak yayımladığı günlük bülten CSV dosyalarını (`BUL_<YYYYMMDD>.csv`) temel alan sıfır maliyetli (zero-cost) **`BistDailyBulletinMarketDataProvider`** sağlayıcısı ile değiştirilmiştir.

Platform; ücretli hiçbir veri dağıtıcısına (Matriks, Foreks, DataStore, Algolab vb.) veya yetkisiz HTML/web kazıma (scraping - Yahoo Finance, Investing.com, TradingView vb.) yöntemlerine başvurmadan, Borsa İstanbul'un resmi açık veri bültenlerini v1.14 spesifikasyonuna uygun biçimde ayrıştırmakta, denetim altına almakta ve T+1 ileriye dönük simülasyon (paper trading forward testing) ile çalıştırmaktadır.

### NİHAİ HAZIRLIK KARARI

```text
================================================================================
BIST DAILY BULLETIN EOD INTEGRATION: READY
READY FOR ZERO-COST DAILY FORWARD TESTING: YES
================================================================================
```

---

## 2. TEMEL MİMARİ BİLEŞENLER VE UYGULAMA DETAYLARI

### 2.1. Sağlayıcı Yetenek Modeli (`MarketDataProviderCapabilities`)
Sağlayıcı yetenekleri nesnel ve tip güvenli olarak tanımlanmıştır:
* `SupportsDaily = true` (Resmi BIST Pay Piyasası EOD barları)
* `SupportsIntraday = false` (M15 ve H1 zaman dilimleri yapay mum üretilmeden resmi olarak devre dışı bırakılmıştır)
* `IsStreaming = false` (WebSocket / canlı akış yoktur)
* `IsEodOnly = true` (Günlük kapanış sonrası resmi bülten temellidir)
* `SupportsCorporateActions = true` (Temettü ve sermaye artırımı düzeltmeleri desteklenir)

Bağımlılık Enjeksiyonunda (`DependencyInjection.cs`):
```csharp
services.AddHttpClient<IBistDailyBulletinMarketDataProvider, BistDailyBulletinMarketDataProvider>(...);
services.AddScoped<Application.Interfaces.IMarketDataProvider>(sp =>
    (Application.Interfaces.IMarketDataProvider)sp.GetRequiredService<IBistDailyBulletinMarketDataProvider>());
```

---

### 2.2. Resmi BIST 2026 Seans ve Tatil Takvimi
Borsa İstanbul Pay Piyasası'nın 2026 yılı işlem takvimi `IMarketSessionCalendar` ve `ConfigurableHolidayCalendar` üzerinde eksiksiz kodlanmıştır:

* **10 Tam Gün Tatili:**
  1. 01 Ocak 2026 (Yılbaşı)
  2. 20 Mart 2026 (Ramazan Bayramı 1. Gün)
  3. 23 Nisan 2026 (Ulusal Egemenlik ve Çocuk Bayramı)
  4. 01 Mayıs 2026 (Emek ve Dayanışma Günü)
  5. 19 Mayıs 2026 (Atatürk'ü Anma, Gençlik ve Spor Bayramı)
  6. 27 Mayıs 2026 (Kurban Bayramı 1. Gün)
  7. 28 Mayıs 2026 (Kurban Bayramı 2. Gün)
  8. 29 Mayıs 2026 (Kurban Bayramı 3. Gün)
  9. 15 Temmuz 2026 (Demokrasi ve Milli Birlik Günü)
  10. 29 Ekim 2026 (Cumhuriyet Bayramı)
* **3 Yarım Gün Seansı (Saat 13:00 Kapanış):**
  1. 19 Mart 2026 (Ramazan Bayramı Arefesi) -> Bülten Saati: **13:25**
  2. 26 Mayıs 2026 (Kurban Bayramı Arefesi) -> Bülten Saati: **13:25**
  3. 28 Ekim 2026 (Cumhuriyet Bayramı Arefesi) -> Bülten Saati: **13:25**
* **Normal Seans Bülten Saati:** **18:25** (Seans kapanışı 18:00 / 18:15 marjı sonrası).
* **Tatil Doğrulaması:** Hafta sonları ve tatil günlerinde worker taraması yapılmaz, gereksiz dış ağ trafiği engellenir.

---

### 2.3. Resmi v1.14 Semicolon CSV Ayrıştırıcı (`BistDailyBulletinParser`)
Borsa İstanbul'un resmi `BUL_<YYYYMMDD>.csv` formatı katı kurallarla ayrıştırılır:
1. **UTF-8 BOM ve Noktalı Virgül (`;`) Desteği:** Dosya başındaki olası BOM karakterleri temizlenir.
2. **Hisse Senedi Doğrulaması:** Yalnızca `INSTRUMENT GROUP == "EQT"` olan enstrümanlar kabul edilir. Varant (`WNT`), Fon (`ETF`) veya Sertifikalar hisse senedi taramasına dahil edilmez.
3. **Sembol Temizleme:** `.E` uzantısı yalnızca `EQT` teyidi alındıktan sonra kaldırılır (`THYAO.E` -> `THYAO`).
4. **Sayısal Format:** Türkçe ondalık virgüller (`326,50`) InvariantCulture (`326.50`) formatına çevrilir.
5. **OHLC Bütünlüğü:** `High >= Low`, `High >= Open`, `High >= Close`, `Low <= Open`, `Low <= Close`, `Volume >= 0` kuralları doğrulanır.
6. **Askıdaki ve İşlemsiz Hisseler:** Seans boyunca işlem görmeyen veya askıda olan hisseler için **asla yapay/sentetik OHLC mumları üretilmez**; ilgili hissenin işlem görmediği `DailyInstrumentMarketStats` tablosunda `TradedQuantity = 0`, `NumberOfTrades = 0` olarak tutulur.

---

### 2.4. Yinelenemezlik (Idempotency) ve Denetim Günlüğü (Audit Logging)
* Her içe aktarma işleminde bülten dosyasının SHA256 özeti (`Sha256Checksum`) hesaplanır.
* Aynı seans tarihi ve aynı dosya özeti için tekrar içe aktarma çağrıldığında sistem işlemi güvenle atlar (`MarketDataImportStatus.Skipped`) ve **0 mükerrer bar** eklenir.
* Borsa İstanbul tarafından yayımlanan revizyon bültenleri (aynı seans tarihi ancak farklı SHA256) tespit edilir (`IsRevision = true`), mevcut barlar güncellenir ve yeni audit kaydı oluşturulur.
* Denetim varlıkları: `MarketDataImport` ve `DailyInstrumentMarketStats` veri tabanında ilişkisel olarak saklanır.

---

### 2.5. Lookahead Bias Koruması ve T+1 Forward Testing
Günlük bülten tarayıcısında gelecek bilgisi sızıntısını (lookahead bias) önlemek amacıyla T+1 emir icra mimarisi devreye alınmıştır:
1. **Sinyal Üretimi (T Kapanışı):** T günü bülteni sisteme girildiğinde (18:25), T gününün kapanış fiyatlarına göre tarama çalışır ve sinyaller `SourceSessionDate = T` etiketiyle üretilir.
2. **Kuyruğa Alma (`PendingNextSessionOpen`):** T günü sinyalleri T kapanışından doldurulmaz. Bunun yerine T+1 seansının açılışında icra edilmek üzere `OrderStatus.PendingNextSessionOpen` durumunda kuyruğa alınır.
3. **Açılışta İcra (T+1 Açılışı):** Ertesi iş gününün (T+1) resmi bülteni sisteme ulaştığında, kuyruktaki emirler T+1 gününün resmi `OPENING PRICE` fiyatından doldurulur (`OrderStatus.Filled`).
4. **Pozisyon Kapanışı:** Stop-loss ve take-profit emirleri de benzer şekilde bir sonraki günün açılışında doldurulur.

---

### 2.6. Zaman Dilimine ve Sağlayıcı Yeteneğine Duyarlı Sağlık Kontrolleri
* `MarketDataFreshnessHealthCheck`: Sağlayıcının `SupportsIntraday == false` olduğunu algılar. Intraday (M15, H1) kontrolleri fail etmek yerine "Disabled per provider capabilities" olarak raporlanır; Daily barlar için ise takvim duyarlı tazelik politikası işletilir.
* `WorkerScanHealthCheck`: EOD sağlayıcı devredeyken gün içi tarama araması yapmaz; bülten yayım saatine göre EOD worker döngüsünü doğrular.

---

### 2.7. Admin API ve Güvenli Dosya Yükleme
Yetkili yöneticiler (`[Authorize(Roles = "Admin")]`) için geliştirilen `AdminMarketDataController`:
* `GET /api/admin/market-data/status`: Sağlayıcı yetenekleri, takvim ve son aktarım durumu.
* `GET /api/admin/market-data/imports`: Son aktarımların denetim listesi.
* `POST /api/admin/market-data/import-session`: Belirtilen tarih için bülteni otomatik indirme ve işleme.
* `POST /api/admin/market-data/upload`: Yerel CSV veya ZIP dosyasını yükleme (Zip Slip saldırılarına ve 50MB üstü dosyalara karşı korumalı).
* `POST /api/admin/market-data/backfill`: Tarih aralığında hafta sonlarını ve tatilleri atlayarak nazik hız limitiyle (rate-limiting) toplu arşiv indirme.

---

### 2.8. Docker ve Kalıcı Depolama
* `docker-compose.yml` ve `docker-compose.prod.yml` dosyalarında kalıcı `bist_market_data` volume'ü tanımlanmış ve `/app/data/marketdata` yoluna bağlanmıştır. Konteyner yeniden başladığında indirilen bültenler korunur.

---

### 2.9. Çift Veritabanı Migrasyonları (Dual Migrations)
* SQLite: `20260908150000_BistDailyBulletinIntegration.cs`
* SQL Server: `20260908150001_BistDailyBulletinIntegration.cs`
Her iki veritabanı sağlayıcısı da `MarketDataImports`, `DailyInstrumentMarketStats` tablolarını, `Signal.SourceSessionDate` ve `OrderStatus` enum genişletmelerini eksiksiz desteklemektedir.

---

## 3. DOĞRULAMA VE TEST SONUÇLARI

### 3.1. .NET Test Paketi Sonuçları
Tüm birim ve entegrasyon testleri Release konfigürasyonunda çalıştırılmıştır:
```text
Passed!  - Failed: 0, Passed:  3, Skipped: 0, Total:  3 - BistQuant.Domain.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 67, Skipped: 0, Total: 67 - BistQuant.Application.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48 - BistQuant.IntegrationTests.dll (net10.0)

TOPLAM: 118 / 118 TEST BAŞARILI (%100 PASS, 0 HATA)
```

Test Kapsamı:
* `BistDailyBulletinParserTests`: Semicolon parsing, `.E` uzantısı temizleme, non-equity filtreleme, askıdaki hisseler, geçersiz OHLC tespiti.
* `BistSessionCalendarTests`: 2026 tatilleri (10 kapalı gün, 3 yarım gün), bülten yayım saatleri, tatil atlama mantığı.
* `BistDailyBulletinIntegrationTests`: Bülten indirme & işleme, SHA256 idempotency, revizyon bülteni tespiti, T+1 forward testing emir icrası.

### 3.2. Frontend Next.js 16 Doğrulaması
* `npm run lint`: 0 hata.
* `npm run build`: Turbopack ile 13/13 sayfa başarıyla derlendi.
* Scanner ve Dashboard sayfalarına BIST Resmi Günlük Bülten (EOD) rozetleri ve bilgilendirmeleri eklendi.

### 3.3. CI/CD Boru Hattı
* `.github/workflows/ci.yml` ve `ci/ci.yml` senkronize edilmiş olup; backend derleme, birim/entegrasyon testleri, frontend lint/build, Gitleaks gizli anahtar denetimi ve bağımlılık güvenlik taramasını kapsar.

---

## 4. SONUÇ

BIST Quant Scanner, sentetik verilerden arındırılarak Borsa İstanbul'un resmi açık kaynak günlük bültenleri ile gerçek dünya verilerine tam olarak bağlanmıştır. Platform, sıfır veri maliyeti ile günlük periyotta algoritmik tarama, sinyal üretimi ve T+1 kağıt üzerinde alım-satım ileriye dönük testleri (forward testing) için tamamen hazırdır.
