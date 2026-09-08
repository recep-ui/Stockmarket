# BIST Quant Scanner - Database Maintenance & Optimization Runbook

## 1. VERİTABANI ŞEMASI VE İNDEKS STRATEJİSİ

Veritabanı MSSQL 2022 üzerinde optimize edilmiş Clean Architecture şeması ile çalışır. Yüksek frekanslı OHLCV ve tarama sorgularını hızlandırmak için şu indeksler yapılandırılmıştır:

### 1.1. PriceBars Tablosu
```sql
CREATE UNIQUE NONCLUSTERED INDEX IX_PriceBars_Symbol_Timeframe_Timestamp
ON PriceBars(SymbolId, Timeframe, Timestamp DESC)
INCLUDE (Open, High, Low, Close, Volume);
```
* **Amaç**: Teknik göstergelerin hesaplanması sırasında geriye dönük 200 barın en hızlı şekilde disk/önbellekten çekilmesini sağlar.

### 1.2. Signals Tablosu
```sql
CREATE NONCLUSTERED INDEX IX_Signals_Timeframe_CreatedAt
ON Signals(Timeframe, CreatedAt DESC)
INCLUDE (SymbolId, Score, SignalType, Price);
```
* **Amaç**: Market Scanner ve Dashboard üzerindeki "En Yüksek Skorlular" ve "Son Sinyaller" sorgularını anında yanıtlar.

---

## 2. VERİTABANI YEDEKLEME (BACKUP) PROSEDÜRLERİ

### 2.1. Tam Yedek (Full Backup)
Docker konteyneri içerisinden MSSQL veritabanı yedeğini almak için:
```bash
docker exec -it bistquant-mssql /opt/mssql-tools18/bin/sqlcmd \
   -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
   -Q "BACKUP DATABASE [BistQuantDb] TO DISK = N'/var/opt/mssql/backup/BistQuantDb_Full.bak' WITH NOFORMAT, NOINIT, SKIP, NOREWIND, NOUNLOAD, STATS = 10"
```

### 2.2. Geri Yükleme (Restore)
```bash
docker exec -it bistquant-mssql /opt/mssql-tools18/bin/sqlcmd \
   -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
   -Q "RESTORE DATABASE [BistQuantDb] FROM DISK = N'/var/opt/mssql/backup/BistQuantDb_Full.bak' WITH REPLACE"
```

---

## 3. VERİ ARŞİVLEME VE TEMİZLİK (PARTITIONING & PURGING)

15 dakikalık (`M15`) ve 1 saatlik (`H1`) periyotlardaki eski barlar belirli aralıklarla temizlenmelidir:
```sql
-- 90 günden eski 15 dakikalık barları temizle
DELETE FROM PriceBars 
WHERE Timeframe = 0 AND Timestamp < DATEADD(DAY, -90, GETUTCDATE());
```
