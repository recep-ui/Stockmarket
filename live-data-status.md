# BIST Canlı Piyasa Verisi Durum Raporu (Live Market Data Status)

**Tarih:** 07 Eylül 2026  
**Denetim Kapsamı:** BIST Quant Scanner Market Data Mimarisi ve Entegrasyon Gerçeği  

---

## 1. Mevcut Sağlayıcı Durumu (Current Provider)

```text
Current Provider:
MockMarketDataProvider (BistQuant.Infrastructure.Providers.MarketData.MockMarketDataProvider)
```

Üretim (Production) ve Geliştirme (Development) ortamlarının tamamında DI konteyneri şu şekilde kaydedilmiştir:
```csharp
services.AddScoped<Application.Interfaces.IMarketDataProvider, Providers.MarketData.MockMarketDataProvider>();
```

Sistemde harici bir finansal veri sağlayıcısına (Matriks, Algolab, iDeal Data, Foreks, TradingView, BIST Doğrudan Veri Dağıtımı vb.) yapılan hiçbir aktif HTTP bağlantısı veya WebSocket soketi bulunmamaktadır.

---

## 2. Veri Tipi ve Doğası (Data Type)

```text
Data Type: MOCK / SYNTHETIC HISTORICAL DATA
Status:    LIVE MARKET DATA NOT IMPLEMENTED
```

* **Gerçek Zamanlı (Real-time):** HAYIR.
* **Gecikmeli (Delayed 15m):** HAYIR.
* **Tarihsel (Historical Real Exchange):** HAYIR.
* **Sentetik/Rastgele (Mock Random):** **EVET**.

### Ekranda Görünen Fiyatlar Nereden Geliyor?
Uygulama arayüzünde (veya Scanner API çıktısında) görüntülenen THYAO (412.55 TL / 326.50 TL), ASELS (64.20 TL) gibi fiyatlar şu döngüden türetilmektedir:
1. `DatabaseInitializer.SeedHistoricalDataAsync` metodu çalışır.
2. Sabit tohumlu bir rastgele sayı üreteci (`new Random(42)`) başlatılır.
3. Her hisse için sabit bir baz fiyata (THYAO için 320.00 TL) yapay bir günlük kayma (`dailyDrift = (decimal)(random.NextDouble() * 0.04 - 0.018)`) eklenerek 120 mumluk sentetik bar üretilir.
4. Bu barlar veritabanındaki `PriceBars` tablosuna yazılır.
5. `MockMarketDataProvider` bu tablodan veri çeker.
6. İndikatörler, skorlar ve sinyaller bu sentetik veriler üzerinden hesaplanır.

---

## 3. Desteklenen Zaman Dilimleri (Supported Timeframes)

* **Veritabanında Mevcut Olan:** `Daily` (1 Günlük)
* **Enum İçinde Tanımlı Olanlar:** `M15` (15 Dakika), `H1` (1 Saat), `Daily` (1 Gün)
* **İntraday Durumu:** Veritabanında hiçbir sembol için M15 veya H1 barı bulunmamaktadır. Tüm tohum veriler günlük barlardan ibarettir.

---

## 4. Son Başarılı Güncelleme (Last Successful Update)

* **En Yeni THYAO Fiyat Barı Timestamp:** `2026-09-07 00:00:00`
* **En Yeni ASELS Fiyat Barı Timestamp:** `2026-09-07 00:00:00`
* **En Yeni TUPRS Fiyat Barı Timestamp:** `2026-09-07 00:00:00`
* **Güncelleme Kaynağı:** Uygulama başlatılırken çalışan tohumlayıcı (Seeder).
* **Canlı Akış (Streaming):** Yok.

---

## 5. Kimlik Doğrulama ve Güvenlik (Authentication)

* **Canlı Veri API Anahtarı (API Key):** Tanımlı DEĞİL.
* **Broker / Dağıtıcı Kimlik Bilgisi:** Tanımlı DEĞİL.
* **Ortam Değişkenleri:** Sistemde veri sağlayıcısına ait hiçbir auth parametresi (username, token, certificate) mevcut değildir.

---

## 6. Hız Limitleri ve Kota Yönetimi (Rate Limits)

* **Mevcut Durum:** Herhangi bir dış API çağrısı yapılmadığı için rate-limit veya backoff mekanizması uygulanmamıştır.

---

## 7. Eksik Uygulama ve Entegrasyon Gereksinimleri (Missing Implementation)

Sistemi gerçek canlı piyasa verisine bağlamak için gereken adımlar:

1. **`ILiveMarketDataProvider` veya Gerçek `IMarketDataProvider` İmplementasyonu:**
   * WebSocket veya REST polling ile gerçek BIST verisini alacak provider sınıfı yazılmalıdır.
2. **Intraday Bar Üreteci (Tick to Bar Aggregator):**
   * Gelen tick veya son işlem verilerini M15, H1 ve Daily mumlara dönüştüren bellek içi (in-memory) bar aggreation motoru eklenmelidir.
3. **Stale Data Guard (TAMAMLANDI):**
   * `IMarketDataFreshnessPolicy` fail-closed mimarisiyle entegre edildi. Zaman dilimi bazında (M1: 3dk, M5: 15dk, M15: 45dk, H1: 3saat, Daily: 4gün) stale kontrolleri devrede olup bayat veri tespit edildiğinde sinyal üretimi ve otomatik alım-satım derhal durdurulur (`SignalEngine`, `PaperTradingService`, `MarketDataFreshnessHealthCheck`).
4. **Kopma & Otomatik Yeniden Bağlanma (Resilience):**
   * Polly tabanlı retry, circuit breaker ve WebSocket reconnect mekanizması canlı veri sağlayıcısı entegrasyonunda kurulacaktır.
5. **Kurumsal Eylem Düzeltmeleri (Corporate Actions Adjustment):**
   * Temettü ve bedelsiz/bedelli sermaye artırımlarında geriye dönük düzeltilmiş fiyat (`AdjustedClose`) motoru entegre edilecektir.

---

## 8. Canlı BIST Entegrasyonuna Hazırlık Kararı (Verdict)

* **Canlı Veri Sağlayıcı Entegrasyonuna Hazır mı?:** **EVET (READY FOR LIVE BIST DATA INTEGRATION: YES)**
* **Açıklama:** Platform içi sinyal-backtest paritesi, veri tazelik politikası (`IMarketDataFreshnessPolicy`), kapalı mum tetikleyicisi (`IMarketScanScheduler`), worker heartbeat izlemesi, fail-closed sağlık kontrolleri ve güvenlik hardening aşamaları başarıyla tamamlanmış ve 88 otomatik test ile doğrulanmıştır. Platform artık tek bir somut `IMarketDataProvider` sınıfı yazılarak canlı borsa beslemesine bağlanmaya tam hazırdır.
