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

Borsa İstanbul bülten dosyaları `BUL_<YYYYMMDD>.csv` adlandırmasıyla, noktalı virgül (`;`) ile ayrılmış ve UTF-8 (BOM içerebilen) biçiminde yayımlanır:

### Başlıca Kolonlar
1. **BÜLTEN TARİHİ (BULLETIN DATE):** `DD/MM/YYYY` (örn. `08/09/2026`)
2. **ENSTRÜMAN KODU (INSTRUMENT CODE):** Hisse ve pazar eki içeren kod (örn. `THYAO.E`, `GARAN.E`, `AKBNK.E`)
3. **ENSTRÜMAN GRUBU (INSTRUMENT GROUP):** `EQT` (Pay / Hisse Senedi), `WNT` (Varant), `ETF` (Borsa Yatırım Fonu) vb.
4. **İŞLEM GÖRDÜĞÜ PAZAR / PAZAR KODU (MARKET CODE):** `ZP` (Yıldız Pazar), `AP` (Ana Pazar), `ALT` (Alt Pazar) vb.
5. **ÖNCEKİ SEANS KAPANIS FİYATI (PREVIOUS CLOSE):** Sayısal değer (Türkçe virgül veya nokta biçimi)
6. **AÇILIŞ FİYATI (OPENING PRICE):** Günün ilk işlem fiyatı
7. **EN DÜŞÜK FİYAT (LOW PRICE):** Gün içinde görülen en düşük fiyat
8. **EN YÜKSEK FİYAT (HIGH PRICE):** Gün içinde görülen en yüksek fiyat
9. **KAPANIŞ FİYATI (CLOSING PRICE):** Resmi seans kapanış fiyatı
10. **AĞIRLIKLI ORTALAMA FİYAT (WEIGHTED AVERAGE PRICE):** Hacim ağırlıklı ortalama fiyat (AOF)
11. **İŞLEM HACMİ (TOTAL VOLUME):** Gerçekleşen toplam lot adedi
12. **İŞLEM TUTARI (TOTAL VALUE):** Gerçekleşen toplam TL işlem büyüklüğü
13. **İŞLEM ADEDİ (NUMBER OF TRADES):** Gerçekleşen işlem/sözleşme sayısı

### Ayrıştırma Kuralları (`BistDailyBulletinParser`)
* **Hisse Doğrulaması:** Yalnızca `INSTRUMENT GROUP == "EQT"` olan enstrümanlar hisse senedi olarak kabul edilir. Varant, sertifika veya fonlar filtrelenir.
* **Sembol Temizleme:** `.E` uzantısı yalnızca `EQT` teyidinden sonra güvenle kaldırılır (`THYAO.E` -> `THYAO`).
* **Sayısal Dönüşüm:** Türkçe ondalık virgül (`,`) InvariantCulture noktaya (`.`) dönüştürülür.
* **OHLC Tutarlılığı:** `High >= Low`, `High >= Open`, `High >= Close`, `Low <= Open`, `Low <= Close` ve `Volume >= 0` kuralları doğrulanır.
* **İşlem Görmeyen / Askıdaki Hisseler:** Eğer hisse seans boyunca askıda kalmışsa veya hiç işlem görmemişse yapay OHLC üretilmez; sadece `DailyInstrumentMarketStats` tablosunda askı/işlemsiz durumu olarak kaydedilir.

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

1. **Sinyal Üretimi (T Günü Kapanışı):** Bülten saat 18:25'te sisteme girildiğinde T gününün OHLC barları veritabanına yazılır ve tarama çalışır. Üretilen sinyaller T seansı tarihine (`SourceSessionDate = T`) etiketlenir.
2. **Emir Kuyruğu (`PendingNextSessionOpen`):** T günü kapanışında üretilen alım/satım sinyalleri derhal T gününün kapanış fiyatından işlem YAPMAZ. Bunun yerine bir sonraki iş günü olan T+1 seansının açılışında gerçekleşmek üzere `OrderStatus.PendingNextSessionOpen` durumunda kuyruğa alınır.
3. **Emir İcrası (T+1 Günü Açılışı):** Bir sonraki seans (T+1) bülteni sisteme geldiğinde, bültenin resmi `OPENING PRICE` fiyatı okunur ve bekleyen emirler bu fiyattan doldurulur (`OrderStatus.Filled`).
4. **Pozisyon Kapanışı:** Stop-loss veya take-profit sinyalleri de benzer şekilde bir sonraki günün açılışında icra edilir.

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
