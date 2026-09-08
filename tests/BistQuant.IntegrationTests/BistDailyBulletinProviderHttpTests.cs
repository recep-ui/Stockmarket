using System.IO.Compression;
using System.Net;
using System.Text;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Interfaces;
using BistQuant.Application.Services.MarketData;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using BistQuant.Infrastructure.Persistence;
using BistQuant.Infrastructure.Providers.MarketData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BistQuant.IntegrationTests;

public class BistDailyBulletinProviderHttpTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, Task<HttpResponseMessage>>? Handler { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Handler == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            return await Handler(request);
        }
    }

    private static (BistQuantDbContext Context, BistDailyBulletinMarketDataProvider Provider, MockHttpMessageHandler HttpHandler)
        CreateProviderWithMockHttp(Dictionary<string, string?>? configOverrides = null)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<BistQuantDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new BistQuantDbContext(options);
        context.Database.EnsureCreated();
        context.Markets.Add(new Market { Id = 1, Code = "BIST", Name = "Borsa Istanbul", Country = "Turkey", Currency = "TRY", Timezone = "Europe/Istanbul" });
        context.SaveChanges();

        var configData = new Dictionary<string, string?>
        {
            ["BistBulletin:VerifiedDownloadEndpoint"] = "https://www.borsaistanbul.com/data/thb/{YYYY}/{MM}/thb{YYYY}{MM}{DD}1.zip",
            ["BistBulletin:AutomaticDownloadEnabled"] = "true",
            ["BistBulletin:StoragePath"] = Path.Combine(AppContext.BaseDirectory, "test_marketdata_" + Guid.NewGuid().ToString("N")),
            ["BistBulletin:FullDayPublicationWindow"] = "18:25:00",
            ["BistBulletin:HalfDayPublicationWindow"] = "13:25:00",
            ["BistBulletin:FullDayCutoff"] = "23:59:59",
            ["BistBulletin:HalfDayCutoff"] = "23:59:59"
        };

        if (configOverrides != null)
        {
            foreach (var kvp in configOverrides)
            {
                configData[kvp.Key] = kvp.Value;
            }
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var httpHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(httpHandler);
        var calendar = new BistMarketSessionCalendar(configuration);
        var dateResolver = new MarketSessionDateResolver();
        var parser = new BistDailyBulletinParser();
        var corpAction = new CorporateActionAdjustmentService(context, configuration, NullLogger<CorporateActionAdjustmentService>.Instance);

        var provider = new BistDailyBulletinMarketDataProvider(
            context,
            httpClient,
            configuration,
            NullLogger<BistDailyBulletinMarketDataProvider>.Instance,
            parser,
            corpAction,
            calendar,
            dateResolver
        );

        return (context, provider, httpHandler);
    }

    private static byte[] CreateZipWithCsv(string csvContent, string entryName = "thb202609071.csv")
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8);
            writer.Write(csvContent);
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task Download_Http200_ValidZip_ReturnsSuccess()
    {
        var (context, provider, httpHandler) = CreateProviderWithMockHttp();
        var date = new DateOnly(2026, 9, 7);

        var csv =
            "TARIH;ISLEM KODU;BULTEN ADI;PAZAR ALT SEGMENTI;PAZAR;PIYASA;ENSTRUMAN GRUBU;ENSTRUMAN TIPI;ENSTRUMAN SINIFI;ISLEM YONTEMI;PIYASA YAPICI;BIST 100;BIST 30;BRUT TAKAS;OZSERMAYE HALLERI;DURDURMA;ONCEKI KAPANIS FIYATI;ACILIS FIYATI;ACILIS SEANSI FIYATI;GUNORTASI FIYATI;EN DUSUK FIYAT;EN YUKSEK FIYAT;KAPANIS FIYATI;KAPANIS SEANSI FIYATI;FIYAT DEGISIMI (%);KALAN ALIS;KALAN SATIS;A.O.F;TOPLAM ISLEM HACMI;TOPLAM ISLEM ADEDI;SOZLESME SAYISI;REFERANS FIYAT\n" +
            "2026-09-07;ASELS.E;ASELSAN ELEKTRONIK;YILDIZ;YILDIZ;PAY;EQT;PAY;HISSE;SUREKLI;H;1;1;H;;0;390.0;390.25;390.25;;390.25;398.75;392.5;392.5;0.64;392.25;392.5;394.8;1000000;28000;450;390.0\n";
        var zipBytes = CreateZipWithCsv(csv, "thb202609071.csv");

        httpHandler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(zipBytes)
        });

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.True(result.IsSuccess);
        Assert.Equal(BulletinDownloadStatus.Success, result.Status);
        Assert.NotNull(result.Content);
        Assert.NotNull(result.ExtractedCsvBytes);
    }

    [Fact]
    public async Task Download_Http200_HtmlPage_ReturnsInvalidSourceContent()
    {
        var (context, provider, httpHandler) = CreateProviderWithMockHttp();
        var date = new DateOnly(2026, 9, 7);

        var html = "<!DOCTYPE html><html><body>Error 404 - Not Found</body></html>";
        httpHandler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        });

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.False(result.IsSuccess);
        Assert.Equal(BulletinDownloadStatus.InvalidSourceContent, result.Status);
        Assert.Contains("HTML/XML", result.ErrorMessage);
    }

    [Fact]
    public async Task Download_Http404_ReturnsNotPublishedYet()
    {
        var (context, provider, httpHandler) = CreateProviderWithMockHttp();
        var date = new DateOnly(2026, 9, 7);

        httpHandler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.False(result.IsSuccess);
        Assert.Equal(BulletinDownloadStatus.NotPublishedYet, result.Status);
        Assert.Equal(404, result.HttpStatusCode);
    }

    [Fact]
    public async Task Download_Http429_WithRetryAfterSeconds_PopulatesRetryAfter()
    {
        var (context, provider, httpHandler) = CreateProviderWithMockHttp();
        var date = new DateOnly(2026, 9, 7);

        httpHandler.Handler = _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return Task.FromResult(response);
        };

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.False(result.IsSuccess);
        Assert.Equal(BulletinDownloadStatus.RateLimited, result.Status);
        Assert.Equal(429, result.HttpStatusCode);
        Assert.NotNull(result.RetryAfter);
        Assert.Equal(120, result.RetryAfter.Value.TotalSeconds);
    }

    [Fact]
    public async Task Download_Http429_WithRetryAfterDate_PopulatesRetryAfter()
    {
        var (context, provider, httpHandler) = CreateProviderWithMockHttp();
        var date = new DateOnly(2026, 9, 7);

        var futureDate = DateTimeOffset.UtcNow.AddMinutes(5);
        httpHandler.Handler = _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(futureDate);
            return Task.FromResult(response);
        };

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.False(result.IsSuccess);
        Assert.Equal(BulletinDownloadStatus.RateLimited, result.Status);
        Assert.NotNull(result.RetryAfter);
        Assert.True(result.RetryAfter.Value.TotalSeconds > 60);
    }

    [Fact]
    public async Task Download_Http500_ReturnsProviderUnavailable()
    {
        var (context, provider, httpHandler) = CreateProviderWithMockHttp();
        var date = new DateOnly(2026, 9, 7);

        httpHandler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.False(result.IsSuccess);
        Assert.Equal(BulletinDownloadStatus.ProviderUnavailable, result.Status);
        Assert.Equal(500, result.HttpStatusCode);
    }

    [Fact]
    public async Task Download_TimeoutOrNetworkException_ReturnsProviderUnavailable()
    {
        var (context, provider, httpHandler) = CreateProviderWithMockHttp();
        var date = new DateOnly(2026, 9, 7);

        httpHandler.Handler = _ => throw new HttpRequestException("Connection timed out.");

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.False(result.IsSuccess);
        Assert.Equal(BulletinDownloadStatus.ProviderUnavailable, result.Status);
        Assert.Contains("Connection timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task Download_CorruptZip_ReturnsInvalidSourceContent()
    {
        var (context, provider, httpHandler) = CreateProviderWithMockHttp();
        var date = new DateOnly(2026, 9, 7);

        // Corrupt bytes with PK header but invalid zip stream
        var corruptBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00 };
        httpHandler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(corruptBytes)
        });

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.False(result.IsSuccess);
        Assert.Equal(BulletinDownloadStatus.InvalidSourceContent, result.Status);
    }

    [Fact]
    public async Task Download_WrongDateInZip_ReturnsDateMismatchOrFailed()
    {
        var (context, provider, httpHandler) = CreateProviderWithMockHttp();
        var date = new DateOnly(2026, 9, 7);

        // Entry name belongs to a different date: 20260908 instead of 20260907
        var zipBytes = CreateZipWithCsv("some content", "thb202609081.csv");
        httpHandler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(zipBytes)
        });

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not match requested session date", result.ErrorMessage);
    }

    [Fact]
    public async Task Download_UnconfiguredEndpoint_ReturnsAutomaticDownloadUnavailable()
    {
        // Unconfigured endpoint
        var (context, provider, httpHandler) = CreateProviderWithMockHttp(new Dictionary<string, string?>
        {
            ["BistBulletin:VerifiedDownloadEndpoint"] = ""
        });
        var date = new DateOnly(2026, 9, 7);

        var result = await provider.DownloadBulletinForDateAsync(date);

        Assert.False(result.IsSuccess);
        Assert.Equal(BulletinDownloadStatus.AutomaticDownloadUnavailable, result.Status);
    }
}
