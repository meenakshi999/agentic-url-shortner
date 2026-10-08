namespace UrlShortener.Application.Contracts;

public sealed record CreateShortUrlRequest(
    string OriginalUrl,
    DateTime? ExpiresAtUtc);