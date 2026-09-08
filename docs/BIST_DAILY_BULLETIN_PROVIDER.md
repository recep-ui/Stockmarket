# Borsa İstanbul Resmi Günlük Bülten Veri Sağlayıcısı (EOD Provider)

Bu doküman, Borsa İstanbul Pay Piyasası Resmi Günlük Bülteni (`BUL_<YYYYMMDD>.csv`) tabanlı sıfır maliyetli (zero-cost) Günlük Kapanış (End-of-Day - EOD) piyasa verisi sağlayıcısının mimarisini, dosya yapısını, veri işleme kurallarını, T+1 ileriye dönük simülasyonunu ve yönetim arayüzlerini açıklar.

---

## 1. Genel Bakış ve Mimari

BIST Quant Scanner, sentetik test verisi (`MockMarketDataProvider`) yerine Borsa İstanbul'un resmi olarak yayımladığı günlük bülten CSV dosyalarını ayrıştıran ve veritabanına işleyen **`BistDailyBulletinMarketDataProvider`** sağlayıcısını varsayılan `IMarketDataProvider` olarak kullanır.

Sağlayıcı yetenekleri (`MarketDataProviderCapabilities`):
* **Canlı Yayın (Streaming):** `IsStreaming = false`
* **Zaman Dilimi Desteği:** `SupportsDaily = true`, `SupportsIntraday = false` (M15 ve H1 yapay mum üretilmez)
* **Veri Tipi:** `IsEodOnly = true`, `SupportsCorporateActions = true`
* **Kaynak:** Resmi Borsa İstanbul Pay Piyasası Bülteni

```
+-------------------------------------------------------------+
| Borsa İstanbul Resmi Bülten Portalı                         |
| (https://www.borsaistanbul.com/data/bulten / Arşiv / FTP)   |
+------------------------------+------------------------------+
                               | CSV / ZIP (BUL_YYYYMMDD.csv)
                               v
+-------------------------------------------------------------+
| BistDailyBulletinMarketDataProvider                         |
| - İndirme & Yerel Önbellekleme (/app/data/marketdata)       |
| - SHA256 Sağlama ve Idempotency Kontrolü                   |
| - v1.14 Semicolon Ayrıştırıcı (BistDailyBulletinParser)     |
| - Audit Kaydı (MarketDataImport)                            |
+------------------------------+------------------------------+
                               |
                               +----------------------------+
                               |                            |
                               v                            v
                      [PriceBars (Daily)]      [DailyInstrumentMarketStats]
                               |
                               v
             +------------------------------------+
             | Worker & MarketScanScheduler       |
             | 1. T+1 Bekleyen Emirleri İşle      |
             | 2. İndikatör & Tarama Motoru       |
             | 3. Sinyal & Uyarı Üretimi          |
             | 4. Bir Sonraki Seans Açılışı İçin  |
             |    PendingNextSessionOpen Emri Aç  |
             +------------------------------------+
```

---

## 2. Resmi CSV Formatı (v1.14 Spesifikasyonu)

Borsa İstanbul bülten dosyaları `thb<YYYYMMDD>1.zip` içerisinde `thb<YYYYMMDD>1.csv` adlandırmasıyla, noktalı virgül (`;`) ile ayrılmış ve UTF-8 biçiminde yayımlanır.

### Standart 0-Tabanlı Kolon Yapısı (v1.14 Düzeni)
0. **TARIH / DATE:** `YYYY-MM-DD` (örn. `2026-09-08`)
1. **ISLEM KODU / INSTRUMENT SERIES CODE:** Hisse serisi (örn. `THYAO.E`, `GARAN.E`, `ASELS.E`)
2. **BULTEN ADI / INSTRUMENT NAME:** Şirket unvanı
3. **PAZAR ALT SEGMENTI / MARKET SUB-SEGMENT**
4. **PAZAR / MARKET SEGMENT**
5. **PIYASA / MARKET**
6. **ENSTRUMAN GRUBU / INSTRUMENT GROUP:** `EQT` (Hisse), `WNT` (Varant), `ETF` (BYF) vb.
7. **ENSTRUMAN TIPI / INSTRUMENT TYPE**
8. **ENSTRUMAN SINIFI / INSTRUMENT CLASS**
9. **ISLEM YONTEMI / TRADING METHOD**
10. **PIYASA YAPICI / MARKET MAKER**
11. **BIST 100:** `1` veya `0`
12. **BIST 30:** `1` veya `0`
13. **BRUT TAKAS / GROSS SETTLEMENT**
14. **OZSERMAYE HALLERI / CORPORATE ACTION:** Bedelli/bedelsiz/temettü işlem kodu (örn. `BDL`, `BDS`, `TEM`)
15. **DURDURMA / SUSPENDED:** `1` (Askıda) veya `0`
16. **ONCEKI KAPANIS FIYATI / PREVIOUS LAST PRICE**
17. **ACILIS FIYATI / OPENING PRICE:** Günün ilk işlem fiyatı
18. **ACILIS SEANSI FIYATI / OPENING SESSION PRICE**
19. **GUNORTASI FIYATI / MIDDAY PRICE:** Gün ortası tek fiyat seansı fiyatı
20. **EN DUSUK FIYAT / LOWEST PRICE:** Gün içi en düşük fiyat
21. **EN YUKSEK FIYAT / HIGHEST PRICE:** Gün içi en yüksek fiyat
22. **KAPANIS FIYATI / CLOSING PRICE:** Resmi seans kapanış fiyatı
23. **KAPANIS SEANSI FIYATI / CLOSING SESSION PRICE**
24. **FIYAT DEGISIMI (%) / CHANGE PERCENT**
25. **KALAN ALIS / REMAINING BID**
26. **KALAN SATIS / REMAINING ASK**
27. **A.O.F / VWAP:** Ağırlıklı ortalama fiyat
28. **TOPLAM ISLEM HACMI / TOTAL TRADED VALUE:** Toplam işlem tutarı (TL)
29. **TOPLAM ISLEM ADEDI / TOTAL TRADED VOLUME:** Toplam işlem adedi/hacmi (Lot/Pay) - *Hacim için zorunlu alan*
30. **SOZLESME SAYISI / TOTAL NUMBER OF CONTRACTS**
31. **REFERANS FIYAT / REFERENCE PRICE**

### Ayrıştırma ve Doğrulama Kuralları (`BistDailyBulletinParser`)
* **Resmi Başlık Zorunluluğu (`RequireRecognizedHeader = true`):** Otomatik indirme modunda resmi BIST başlık satırı zorunludur. Başlıksız dosyalarda kolon sırası tahmin edilmez; `SchemaMismatch` hatası ile işlem reddedilir (Fail-Closed).
* **Hisse Doğrulaması:** Yalnızca `INSTRUMENT GROUP == "EQT"` olan enstrümanlar hisse senedi olarak kabul edilir. Varant, sertifika veya borçlanma araçları filtrelenir.
* **Sembol Temizleme:** `.E` uzantısı yalnızca `EQT` teyidinden sonra güvenle kaldırılır (`THYAO.E` -> `THYAO`).
* **Sayısal Dönüşüm:** InvariantCulture (nokta ondalık, virgül binlik) önceliklidir (`1,234.56` -> `1234.56`, `5,000,000` -> `5000000`). Tek virgüllü Türkçe ondalıklar (`100,50` -> `100.50`) `5,000` gibi 3 basamaklı binlik sayılarla karıştırılmayacak biçimde izole ayrıştırılır.
* **Hacim Zorunluluğu:** `TotalTradedVolume` (Lot adedi) kesinlikle zorunludur (`HasValue && Value >= 0`). `TotalTradedValue` (TL tutarı) hacim yerine kullanılamaz. Eksik veya ayrıştırılamayan hacim `0` olarak kabul edilmez; satır geçersiz kılınır (`HasValidOhlc = false`).
* **OHLC Tutarlılığı:** `High >= Low`, `High >= Open`, `High >= Close`, `Low <= Open`, `Low <= Close` kuralları doğrulanır.
* **İşlem Görmeyen / Askıdaki Hisseler:** Eğer hisse seans boyunca askıda kalmışsa (`Suspended == true`) veya geçerli açılış/kapanış fiyatı yoksa yapay OHLC üretilmez; `DailyInstrumentMarketStats` tablosuna askı durumu kaydedilir.

---

## 3. Resmi 2026 Tatil ve Seans Takvimi

BIST Pay Piyasası seans saatleri ve tatil takvimi `IMarketSessionCalendar` ve `BistMarketSessionCalendar` tarafından yönetilir:

### 2026 Tam Gün Tatilleri (10 İş Günü)
1. **01 Ocak 2026 (Perşembe):** Yılbaşı
2. **20 Mart 2026 (Cuma):** Ramazan Bayramı 1. Gün
3. **23 Nisan 2026 (Perşembe):** Ulusal Egemenlik ve Çocuk Bayramı
4. **01 Mayıs 2026 (Cuma):** Emek ve Dayanışma Günü
5. **19 Mayıs 2026 (Salı):** Atatürk'ü Anma, Gençlik ve Spor Bayramı
6. **27 Mayıs 2026 (Çarşamba):** Kurban Bayramı 1. Gün
7. **28 Mayıs 2026 (Perşembe):** Kurban Bayramı 2. Gün
8. **29 Mayıs 2026 (Cuma):** Kurban Bayramı 3. Gün
9. **15 Temmuz 2026 (Çarşamba):** Demokrasi ve Milli Birlik Günü
10. **29 Ekim 2026 (Perşembe):** Cumhuriyet Bayramı

### 2026 Yarım Gün Seansları (Saat 13:00 Kapanış)
1. **19 Mart 2026 (Perşembe):** Ramazan Bayramı Arefesi (Seans Kapanış: 13:00, Bülten: 13:25)
2. **26 Mayıs 2026 (Salı):** Kurban Bayramı Arefesi (Seans Kapanış: 13:00, Bülten: 13:25)
3. **28 Ekim 2026 (Çarşamba):** Cumhuriyet Bayramı Arefesi (Seans Kapanış: 13:00, Bülten: 13:25)

Normal seans günlerinde bülten yayım saati **18:25**, yarım günlerde **13:25** olarak tanımlanmıştır.

---

## 4. Idempotency (Yinelenemezlik) ve Denetim Günlüğü

* Her içe aktarma işlemi dosyanın SHA256 özetini çıkarır.
* Aynı seans tarihi ve aynı SHA256 özeti daha önce işlenmişse içe aktarma atlanır (`MarketDataImportStatus.Skipped`), 0 mükerrer bar eklenir.
* Borsa İstanbul aynı gün için düzeltilmiş revizyon bülteni yayımlarsa SHA256 değiştiği için revizyon tespit edilir (`IsRevision = true`), mevcut barlar güncellenir ve yeni denetim kaydı atılır.
* Bütün aktarımlar `MarketDataImport` tablosunda saklanır.

---

## 5. T+1 İleriye Dönük Kağıt Üzerinde İşlem (Paper Trading Forward Testing)

Günlük bülten verisiyle çalışan bir algoritmik tarayıcıda en kritik kural **Lookahead Bias (Gelecek Bilgisi Sızıntısı)** oluşmamasıdır:

1. **Sinyal Üretimi (T Günü Kapanışı):** Bülten yayım penceresinde (tam günlerde 18:25 TRT, yarım günlerde 13:25 TRT) sisteme girildiğinde T gününün OHLC barları veritabanına yazılır ve tarama çalışır. Üretilen sinyaller T seansı tarihine (`SourceSessionDate = T`) etiketlenir.
2. **Emir Kuyruğu (`PendingNextSessionOpen`):** T günü kapanışında üretilen alım/satım sinyalleri derhal T gününün kapanış fiyatından işlem YAPMAZ. Bunun yerine bir sonraki iş günü olan T+1 seansının açılışı için `OrderStatus.PendingNextSessionOpen` durumunda kuyruğa alınır.
3. **Emir İcrası ve UTC Zaman Damgası Dönüşümü:** Bir sonraki seans (T+1) bülteni sisteme geldiğinde, bültenin resmi `OPENING PRICE` fiyatı okunur ve bekleyen emirler bu fiyattan doldurulur (`OrderStatus.Filled`).
   * **Zaman Damgası Semantiği:** `PriceBar.Timestamp` normalleştirilmiş seans-tarih anahtarıdır (`SessionDate 00:00 UTC`).
   * **Fiziksel İcra Zamanı:** `PaperOrder.FilledAt` ve `PaperTrade.ExecutedAt` alanları yerel BIST saati olan 10:00 Europe/Istanbul vaktini `IMarketSessionCalendar.GetSessionOpenUtc(sessionDate)` üzerinden tam UTC karşılığı olan **07:00 UTC** değerine dönüştürerek kaydeder.
4. **Pozisyon Kapanışı ve İptal/Zaman Aşımı:** Askıda olan veya açılış fiyatı bulunmayan enstrümanlar için emirler T+2 gününe taşınmaz; `OrderStatus.Expired` durumuna çekilerek iptal gerekçesi kaydedilir.

---

## 5.1. Yayın Kesilme Zamanı (Publication Cutoff) ve Retry-After Yönetimi

* **Yayın Kesilme Zamanı:** Tam günlerde **21:00 TRT (18:00 UTC)**, yarım günlerde **16:00 TRT (13:00 UTC)** sonrasında otomatik HTTP denemeleri durdurulur (`NextAttemptAt = null`). Gece boyu gereksiz BIST sorgulaması yapılmaz.
* **HTTP 429 & Retry-After:** Borsa İstanbul sunucusundan 429 yanıtı alındığında yanıttaki `Retry-After` başlığı (saniye veya HTTP tarihi cinsinden) ayrıştırılır ve bir sonraki deneme zamanı buna göre planlanır. Başlık bulunmuyorsa 15 dakikalık varsayılan bekleme süresi uygulanır.
* **Duruma Duyarlı Geri Çekilme (Status-Aware Backoff):**
  * 404 (Bülten henüz hazır değil): 10 dakika
  * 429 (Hız sınırı): Retry-After veya 15 dakika
  * 5xx / Ağ hatası: Üstel bekleme (1d -> 2d -> 5d -> 10d -> 20d -> 30d tavan)
  * SchemaMismatch / DateMismatch: Otomatik deneme yapılmaz (admin incelemesi gerekir)

---

## 6. Admin API ve Yönetim Uç Noktaları

Yetkili yöneticiler (`AdminMarketDataController`, `[Authorize(Roles = "Admin")]`) bülten işlemlerini yönetebilir:

* **`GET /api/admin/market-data/status`**: Sağlayıcı adı, aktif takvim bilgisi, desteklenen zaman dilimleri ve son içe aktarım durumunu döner.
* **`GET /api/admin/market-data/imports?limit=20`**: Geçmiş bülten aktarım loglarını listeler.
* **`POST /api/admin/market-data/import-session`**: Belirli bir tarih için bülteni uzaktan indirip veritabanına işler (`{ "date": "2026-09-08" }`).
* **`POST /api/admin/market-data/upload`**: Manuel olarak indirilen `.csv` veya `.zip` bülten dosyasını güvenli şekilde yükler (Zip slip ve dosya boyutu korumalı).
* **`POST /api/admin/market-data/backfill`**: Tarih aralığı belirterek bültenleri toplu geriye dönük indirir (`startDate`, `endDate`, `delayBetweenRequestsMs`).

---

## 7. Docker Dağıtımı ve Kalıcı Depolama

Bülten dosyaları Docker konteyneri yeniden başlatıldığında kaybolmaması için kalıcı bir volume üzerinde saklanır:

```yaml
volumes:
  bist_market_data:
    driver: local

services:
  backend:
    environment:
      BistBulletin__StoragePath: "/app/data/marketdata"
    volumes:
      - bist_market_data:/app/data/marketdata
```
