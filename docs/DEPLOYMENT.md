# BIST Quant Scanner - Deployment & Operations Guide

Bu doküman, Borsa İstanbul Kantitatif Tarama, Teknik Analiz, Backtest ve Sinyal Platformu'nun üretim (production) ve geliştirme ortamlarında Docker ve Native yöntemlerle çalıştırılması için gerekli tüm adımları içermektedir.

---

## 1. MİMARİ BİLEŞENLERİ

| Bileşen | Teknoloji | Varsayılan Port | Açıklama |
| :--- | :--- | :--- | :--- |
| **bistquant-frontend** | Next.js 15, React, Tailwind CSS | `3000` | Bloomberg Terminal tarzı koyu tema finansal arayüz |
| **bistquant-api** | ASP.NET Core 10, Clean Architecture | `5000` (`8080`) | REST API, JWT Auth, Sinyal & Gösterge motoru |
| **bistquant-worker** | .NET 10 Background Service | - | 15D, 1S, Günlük periyotlarda otomatik tarama servisi |
| **mssql** | Microsoft SQL Server 2022 | `1433` | İlişkisel veritabanı, OHLCV verileri, sinyal kayıtları |
| **redis** | Redis 7 Alpine | `6379` | Dağıtık önbellek (Distributed Cache) |
| **nginx** | Nginx Alpine | `80` | Reverse proxy, SSL sonlandırma, gzip sıkıştırma |

---

## 2. HIZLI BAŞLANGIÇ (DOCKER COMPOSE)

### 2.1. Ortam Değişkenlerini Tanımlayın
```bash
cp .env.example .env
```
Gerekiyorsa `.env` dosyasındaki `MSSQL_SA_PASSWORD` ve `JWT_KEY` değerlerini güncelleyin.

### 2.2. Tüm Sistemi Başlatın
```bash
docker compose up -d --build
```

### 2.3. Erişim Noktaları
* **Web Arayüzü**: [http://localhost](http://localhost) (veya doğrudan frontend portu: [http://localhost:3000](http://localhost:3000))
* **Swagger API Dokümantasyonu**: [http://localhost/swagger](http://localhost/swagger) (veya [http://localhost:5000/swagger](http://localhost:5000/swagger))
* **Health Check**: [http://localhost/api/health](http://localhost/api/health)

---

## 3. YEREL GELİŞTİRME (LOCAL DEVELOPMENT)

### 3.1. .NET Backend'i Çalıştırma
Backend, `appsettings.json` içinde `DatabaseProvider: "Sqlite"` seçeneği sayesinde yerel geliştirme için harici bir MSSQL sunucusu gerektirmeden çalışabilir:

```bash
# REST API'yi başlat
dotnet run --project src/BistQuant.API

# veya Background Worker'ı başlat
dotnet run --project src/BistQuant.Worker
```

### 3.2. Testleri Çalıştırma
```bash
dotnet test BistQuant.slnx
```
Tüm 39 Domain, Application ve Integration testleri çalıştırılır.

### 3.3. Next.js Frontend'i Çalıştırma
```bash
cd frontend
npm run dev
```
Arayüze [http://localhost:3000](http://localhost:3000) adresinden erişilebilir.

---

## 4. GÜVENLİK VE ÜRETİM KONTROL LİSTESİ

1. **JWT Güvenliği**: Production ortamında `JWT_KEY` en az 256 bit uzunluğunda rastgele bir dize olmalıdır.
2. **MSSQL Güçlü Şifre**: `MSSQL_SA_PASSWORD` karmaşık bir şifre olarak belirlenmelidir.
3. **Rate Limiting**: ASP.NET Core RateLimiter dakikada 120 istekle sınırlandırılmıştır.
4. **Sıfır Look-Ahead Bias**: Backtest simülasyonları T barı kapanış sinyallerini T+1 açılışında çalıştırır.
