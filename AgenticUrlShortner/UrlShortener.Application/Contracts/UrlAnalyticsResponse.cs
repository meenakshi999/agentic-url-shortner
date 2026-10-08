namespace UrlShortener.Application.Contracts;

public sealed record UrlAnalyticsResponse(
    string ShortCode,
    string OriginalUrl,
    long ClickCount,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    bool IsActive);