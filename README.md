# BIST Quant Scanner Platform

[![CI Pipeline](https://github.com/recep-ui/Stockmarket/actions/workflows/ci.yml/badge.svg)](https://github.com/recep-ui/Stockmarket/actions/workflows/ci.yml)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Next.js](https://img.shields.io/badge/Next.js-16.3-black.svg)](https://nextjs.org/)
[![React](https://img.shields.io/badge/React-19.0-blue.svg)](https://react.dev/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind-v4-cyan.svg)](https://tailwindcss.com/)
[![Tests](https://img.shields.io/badge/Tests-147%20Passed-brightgreen.svg)](https://github.com/recep-ui/Stockmarket)

> **Borsa İstanbul (BIST) Pay Piyasası için geliştirilmiş kurumsal düzeyde sıfır maliyetli (zero-cost) algoritmik tarama, kantitatif puanlama, sinyal üretimi, geriye dönük test (backtesting) ve T+1 ileriye dönük simülasyon (forward-testing paper trading) platformu.**

---

## 1. Temel Özellikler

* **Resmi BIST Günlük Bülten Veri Sağlayıcısı (EOD):**
  * Ücretli veri dağıtıcılarına (Matriks, Foreks vb.) veya yetkisiz kazımaya (scraping) gerek duymadan Borsa İstanbul'un resmi açık kaynak günlük bülten CSV dosyalarını (`BUL_<YYYYMMDD>.csv`) ayrıştırır ve veritabanına işler.
  * **v1.14 Semicolon Ayrıştırıcı (`BistDailyBulletinParser`):** UTF-8 BOM desteği, InvariantCulture ondalık dönüşüm, katı hisse (`INSTRUMENT GROUP == EQT`) doğrulaması, `.E` temizleme ve OHLC tutarlılık kontrolleri.
  * **SHA256 Yinelenemezlik (Idempotency):** Aynı dosya tekrar yüklendiğinde mükerrer bar eklenmez; revizyon bültenleri otomatik tespit edilir ve denetim günlüğü (`MarketDataImport`) tutulur.
* **Resmi 2026 BIST Seans ve Tatil Takvimi (`BistMarketSessionCalendar`):**
  * 10 resmi tatil iş günü ve 3 yarım gün seansı (19 Mart, 26 Mayıs, 28 Ekim - Saat 13:00 kapanış).
  * Bülten yayım saati duyarlı zamanlayıcı (Tam günlerde 18:25, yarım günlerde 13:25).
* **Lookahead Bias Korumalı T+1 Paper Trading:**
  * Seans T kapanışında üretilen sinyaller aynı günün kapanışından işlem yapmaz.
  * Emirler `OrderStatus.PendingNextSessionOpen` durumunda kuyruğa alınır ve ertesi seansın (T+1) resmi açılış fiyatından (`OPENING PRICE`) icra edilir.
* **Kantitatif Puanlama ve Sinyal Motoru:**
  * 0–100 arası Trend (40), Momentum (30), Hacim (15) ve Yapı (15) puanlama bileşenleri.
  * Açıklanabilir sinyal gerekçeleri (`SignalReason`), ATR ve destek bazlı dinamik Stop-Loss / Take-Profit seviyeleri.
* **Teknik Analiz Motoru:**
  * EMA (20, 50, 100, 200), SMA (20, 50, 200), RSI (14), MACD (12/26/9), ATR (14), ADX (14), Stochastic (14/3/3), SuperTrend (10, 3.0), Bollinger Bantları (20, 2.0), Hacim Göstergeleri, Destek/Direnç ve Kırılım Tespiti.
* **Gelişmiş Strateji ve Tarama Motoru:**
  * Kullanıcıya özel (Private) stratejiler, sistem stratejileri, çoklu koşullu filtreleme.
* **Modern ve Zengin Kullanıcı Arayüzü:**
  * Next.js 16 (Turbopack), React 19, TypeScript ve Tailwind CSS ile koyu finansal terminal estetiği.
  * TradingView Lightweight Charts, etkileşimli tarayıcı, canlı bülten durum rozetleri.

---

## 2. Mimari ve Teknoloji Yığını

| Katman | Teknoloji | Açıklama |
| :--- | :--- | :--- |
| **Backend** | .NET 10 (C# 14), ASP.NET Core Web API | Clean Architecture, CQRS / Service Pattern |
| **Veritabanı** | Microsoft SQL Server 2022 / SQLite | EF Core 10 (Code-First Migrations, Dual Database) |
| **Önbellek & Kuyruk** | Redis 7 + MemoryCache | Dağıtık önbellekleme ve rate-limiting |
| **Arka Plan Servis** | .NET BackgroundService | MarketScanScheduler (EOD Seans Duyarlı) |
| **Frontend** | Next.js 16, React 19, TypeScript, Tailwind | Finansal terminal UI, Server Components |
| **Konteyner** | Docker, Docker Compose, Nginx Reverse Proxy | Üretim paketleme ve kalıcı veri hacimleri (`bist_market_data`) |
| **Güvenlik & CI** | JWT Authentication, Gitleaks, GitHub Actions | Rol bazlı erişim denetimi (RBAC), otomatik test boru hattı |

---

## 3. Kurulum ve Çalıştırma

### 3.1. Ön Koşullar
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [Node.js 22+](https://nodejs.org/)
* [Docker ve Docker Compose](https://www.docker.com/) (Opsiyonel / Üretim için)

### 3.2. Yerel Geliştirme (Local Development)

#### 1. Depoyu Klonlayın
```bash
git clone https://github.com/recep-ui/Stockmarket.git
cd Stockmarket
```

#### 2. Arka Uç (Backend) Servislerini Başlatın
```bash
# Bağımlılıkları geri yükleyin ve derleyin
dotnet build BistQuant.slnx

# SQLite veya SQL Server kullanarak API'yi başlatın
dotnet run --project src/BistQuant.API
```
* API Swagger Arayüzü: `http://localhost:5000/swagger`
* Sağlık Kontrolleri: `http://localhost:5000/health/ready`

#### 3. Ön Yüz (Frontend) Arayüzünü Başlatın
```bash
cd frontend
npm install
npm run dev
```
* Web Paneli: `http://localhost:3000`

---

### 3.3. Docker Compose ile Dağıtım (Production)

```bash
# Ortam değişkenlerini hazırlayın
cp .env.example .env

# Tüm servisleri ayağa kaldırın (Backend, Worker, Frontend, MSSQL, Redis, Nginx)
docker compose -f docker-compose.prod.yml up -d --build
```
Kalıcı bülten dosyaları `bist_market_data` volume'ü altında saklanır (`/app/data/marketdata`).

---

## 4. Yönetici (Admin) Bülten API Uç Noktaları

Yetkili yöneticiler (`Admin` rolü ve Bearer JWT token ile) piyasa verisi bültenlerini şu uç noktalarla yönetebilir:

* `GET /api/admin/market-data/status`: Sağlayıcı yetenekleri, aktif takvim ve son aktarım durumu.
* `GET /api/admin/market-data/imports`: Geçmiş aktarım logları ve denetim kayıtları.
* `POST /api/admin/market-data/import-session`: Belirli bir tarih için bülteni uzaktan indirip veritabanına işler:
  ```json
  { "date": "2026-09-08" }
  ```
* `POST /api/admin/market-data/upload`: Yerel `.csv` veya `.zip` bülten dosyasını güvenli yükleme (Zip-slip ve boyut korumalı).
* `POST /api/admin/market-data/backfill`: Tarih aralığında hafta sonlarını ve tatilleri atlayarak toplu geçmiş veri yükleme:
  ```json
  {
    "startDate": "2026-01-01",
    "endDate": "2026-09-08",
    "delayBetweenRequestsMs": 500
  }
  ```

---

## 5. Test ve Kalite Güvencesi

Proje bünyesinde 147 adet deterministik CI-uyumlu otomatik test ve 1 adet resmi duman testi (`OfficialSmokeTest`) bulunmaktadır:

```bash
# Deterministik CI test paketini çalıştırın
dotnet test BistQuant.slnx -c Release --filter "Category!=OfficialSmokeTest"

# İsteğe bağlı resmi bülten duman testini çalıştırın (resmi ZIP gerektirir)
dotnet test BistQuant.slnx -c Release --filter "Category=OfficialSmokeTest"
```

```text
Passed!  - Failed: 0, Passed:  3, Skipped: 0, Total:  3 - BistQuant.Domain.Tests.dll
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82 - BistQuant.Application.Tests.dll
Passed!  - Failed: 0, Passed: 62, Skipped: 0, Total: 62 - BistQuant.IntegrationTests.dll
Toplam Deterministik CI: 147/147 Test Başarılı (%100 Pass)
Resmi Duman Testi: PASS (Örnek bülten mevcut olduğunda) / SKIPPED (Mevcut olmadığında)
```

Frontend arayüz lint ve build kontrolleri:
```bash
cd frontend
npm run lint
npm run build
```
13 dinamik ve statik rotanın tümü Next.js Turbopack ile sıfır hata ile derlenir.

---

## 6. Lisans ve Katkı
Bu proje MIT lisansı altında dağıtılmaktadır. Borsa İstanbul açık bülten verileri Borsa İstanbul A.Ş. veri dağıtım kurallarına tabidir.
