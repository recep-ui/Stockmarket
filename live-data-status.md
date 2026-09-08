# BIST Piyasa Verisi Durum Raporu (Market Data Status)

**Tarih:** 08 Eylül 2026  
**Denetim Kapsamı:** BIST Quant Scanner Market Data Mimarisi ve Resmi Günlük Bülten Entegrasyonu  
**Durum:** **READY FOR ZERO-COST DAILY FORWARD TESTING: YES**

---

## 1. Mevcut Sağlayıcı Durumu (Current Provider)

```text
Current Registered Provider:
BistDailyBulletinMarketDataProvider (BistQuant.Infrastructure.Providers.MarketData.BistDailyBulletinMarketDataProvider)
```

Üretim (Production) ve Geliştirme (Development) DI konteynerinde `IMarketDataProvider` artık sentetik mock yerine Borsa İstanbul Pay Piyasası Resmi Günlük Bülteni sağlayıcısına bağlanmıştır:

```csharp
services.AddHttpClient<IBistDailyBulletinMarketDataProvider, BistDailyBulletinMarketDataProvider>(...);
services.AddScoped<Application.Interfaces.IMarketDataProvider>(sp =>
    (Application.Interfaces.IMarketDataProvider)sp.GetRequiredService<IBistDailyBulletinMarketDataProvider>());
```

*(Not: `MockMarketDataProvider` geriye dönük sentetik birim test senaryoları için kod tabanında saklanmış ancak birincil bağımlılık enjeksiyonundan çıkarılmıştır).*

---

## 2. Veri Tipi ve Doğası (Data Type)

```text
Data Type: OFFICIAL BIST END-OF-DAY (EOD) DAILY BULLETIN DATA
Status:    ZERO-COST OFFICIAL BULLETIN INTEGRATION ACTIVE
```

* **Gerçek Borsa Verisi (Real Exchange Data):** **EVET** (Borsa İstanbul Pay Piyasası Resmi Günlük Bülteni `BUL_<YYYYMMDD>.csv` v1.14 spesifikasyonu).
* **Maliyet:** **SIFIR MALİYET (ZERO-COST)**. Ücretli veri dağıtıcıları (Matriks, Foreks, Algolab, paid DataStore) veya yetkisiz kazıma (scraping) araçları kullanılmaz.
* **Yapay Mum Üretimi (Synthetic Fabrication):** **YOK**. Eksik barlar için yapay intraday mum üretimi tamamen engellenmiştir.
* **Sağlayıcı Yetenekleri (`MarketDataProviderCapabilities`):**
  * `SupportsDaily = true`
  * `SupportsIntraday = false`
  * `IsStreaming = false`
  * `IsEodOnly = true`
  * `SupportsCorporateActions = true`

---

## 3. Desteklenen Zaman Dilimleri (Supported Timeframes)

* **Aktif Zaman Dilimi:** `Daily` (1 Günlük Resmi Kapanış Barları)
* **İntraday Durumu (M15, H1):** Sağlayıcı yetenek modeli tarafından resmi olarak devre dışı bırakılmıştır. Sağlık kontrolleri (`MarketDataFreshnessHealthCheck`) ve tarayıcı (`MarketScanScheduler`) provider yeteneklerini algılar; günlük veriler için tazelik kontrolü yapılırken, intraday zaman dilimleri devre dışı ("disabled") olarak raporlanır ve sahte hata/alarm üretilmez.

---

## 4. Dosya Ayrıştırma ve Doğrulama Kuralları (`BistDailyBulletinParser`)

* **Enstrüman Filtresi:** Yalnızca `INSTRUMENT GROUP == "EQT"` olan pay senedi kayıtları işlenir. Varant (`WNT`), borsa yatırım fonu (`ETF`) ve sertifikalar filtrelenir.
* **Sembol Temizleme:** `.E` uzantısı sadece `EQT` teyidi yapıldıktan sonra kaldırılır (örn. `THYAO.E` -> `THYAO`).
* **Sayısal Format:** Noktalı virgül (`;`) ile ayrılmış satırlarda Türkçe ondalık virgüller (`25,40`) InvariantCulture (`25.40`) standardına dönüştürülür.
* **OHLC ve Hacim Bütünlüğü:** `High >= Low`, `High >= Open`, `High >= Close`, `Low <= Open`, `Low <= Close`, `Volume >= 0` koşulları sıkı denetimden geçirilir.
* **İşlem Görmeyen ve Askıdaki Hisseler:** Seans boyunca hiç işlem görmemiş veya askıda kalmış enstrümanlar için yapay OHLC barı üretilmez; resmi işlem hacmi ve statü bilgisi `DailyInstrumentMarketStats` tablosuna kaydedilir.

---

## 5. Yinelenemezlik ve Denetim Günlüğü (Idempotency & Audit)

* Her bülten dosyasının SHA256 özeti alınır.
* Aynı seans tarihi ve aynı dosya özeti için ikinci kez içe aktarma çalıştırıldığında işlem atlanır (`MarketDataImportStatus.Skipped`), **0 mükerrer bar** üretilir.
* BIST tarafından yayımlanan revizyon bültenleri otomatik tespit edilir (`IsRevision = true`) ve mevcut barlar güncellenir.
* Bütün aktarım hareketleri `MarketDataImport` audit tablosunda izlenir.

---

## 6. Lookahead Bias Koruması ve T+1 Forward Testing

1. Seans T bülteni sisteme girdiğinde (tam günlerde 18:25, yarım günlerde 13:25), T gününün resmi barları veritabanına işlenir.
2. T günü kapanışında algoritmik tarama çalışır ve sinyaller üretilir (`SourceSessionDate = T`).
3. Üretilen sinyaller aynı günün kapanışından işlem yapmaz. Emirler bir sonraki iş gününün açılışı için `OrderStatus.PendingNextSessionOpen` durumunda kuyruğa alınır.
4. Bir sonraki seans (T+1) bülteni sisteme yüklendiğinde, kuyruktaki emirler T+1 gününün resmi `OPENING PRICE` fiyatından doldurulur (`OrderStatus.Filled`).

---

## 7. BIST 2026 Resmi Tatil ve Seans Takvimi

* **10 Tam Gün Tatili:** 01 Ocak, 20 Mart, 23 Nisan, 01 Mayıs, 19 Mayıs, 27-28-29 Mayıs, 15 Temmuz, 29 Ekim.
* **3 Yarım Gün Seansı (13:00 Kapanış):** 19 Mart, 26 Mayıs, 28 Ekim (Bülten yayım saati: 13:25).
* **Takvim Sağlayıcı:** `BistMarketSessionCalendar`, tatil günlerinde worker taramasını ve gereksiz HTTP isteklerini atlar.

---

## 8. Yönetim ve Dağıtım

* **Admin API:** `/api/admin/market-data/status`, `imports`, `import-session`, `upload`, `backfill` uç noktaları.
* **Kalıcı Depolama:** `bist_market_data` Docker volume'ü `/app/data/marketdata` dizinine bağlanmıştır.
* **Test Durumu:** 118 otomatik test (Domain, Application, Integration) %100 başarıyla geçmiş ve doğrulanmıştır.

---

## 9. Sonuç ve Hazırlık Kararı (Verdict)

* **BIST DAILY BULLETIN EOD INTEGRATION:** **READY**
* **READY FOR ZERO-COST DAILY FORWARD TESTING:** **YES**
