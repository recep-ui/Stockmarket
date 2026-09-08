using BistQuant.Domain.Enums;

namespace BistQuant.Application.DTOs.MarketData;

public record BulletinDownloadResult(
    BulletinDownloadStatus Status,
    DateOnly SessionDate,
    int? HttpStatusCode,
    string SourceUrl,
    TimeSpan? RetryAfter,
    byte[]? Content,
    string? ErrorMessage,
    string? Sha256 = null,
    string? FileName = null,
    byte[]? ExtractedCsvBytes = null
)
{
    public bool IsSuccess => Status == BulletinDownloadStatus.Success && Content != null && Content.Length > 0;
    public byte[]? RawBytes => Content;
    public int? HttpStatus => HttpStatusCode;

    public static BulletinDownloadResult SuccessResult(
        DateOnly date,
        byte[] rawBytes,
        string sha256,
        string fileName,
        string sourceUrl,
        byte[]? extractedCsvBytes = null) =>
        new(BulletinDownloadStatus.Success, date, 200, sourceUrl, null, rawBytes, null, sha256, fileName, extractedCsvBytes);

    public static BulletinDownloadResult NotPublishedYet(
        DateOnly date,
        int? httpStatusCode,
        string error,
        TimeSpan? retryAfter = null,
        string sourceUrl = "") =>
        new(BulletinDownloadStatus.NotPublishedYet, date, httpStatusCode, sourceUrl, retryAfter, null, error);

    public static BulletinDownloadResult RateLimited(
        DateOnly date,
        int? httpStatusCode,
        string error,
        TimeSpan? retryAfter = null,
        string sourceUrl = "") =>
        new(BulletinDownloadStatus.RateLimited, date, httpStatusCode, sourceUrl, retryAfter, null, error);

    public static BulletinDownloadResult ProviderUnavailable(
        DateOnly date,
        int? httpStatusCode,
        string error,
        string sourceUrl = "") =>
        new(BulletinDownloadStatus.ProviderUnavailable, date, httpStatusCode, sourceUrl, null, null, error);

    public static BulletinDownloadResult InvalidSourceContent(
        DateOnly date,
        int? httpStatusCode,
        string error,
        string sourceUrl = "") =>
        new(BulletinDownloadStatus.InvalidSourceContent, date, httpStatusCode, sourceUrl, null, null, error);

    public static BulletinDownloadResult AutomaticDownloadUnavailable(
        DateOnly date,
        string error) =>
        new(BulletinDownloadStatus.AutomaticDownloadUnavailable, date, null, "", null, null, error);

    public static BulletinDownloadResult Failed(
        DateOnly date,
        int? httpStatusCode,
        string error,
        string sourceUrl = "") =>
        new(BulletinDownloadStatus.Failed, date, httpStatusCode, sourceUrl, null, null, error);
}
