namespace UrlShortener.Application.Contracts;

public sealed record ShortUrlResponse(
    Guid Id,
    string ShortCode,
    string OriginalUrl,
    string ShortUrl,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    long ClickCount);