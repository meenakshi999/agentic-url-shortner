using UrlShortener.Application.Contracts;

namespace UrlShortener.Application.Services;

public interface IUrlShortenerService
{
    Task<ShortUrlResponse> CreateAsync(
        CreateShortUrlRequest request,
        CancellationToken cancellationToken = default);

    Task<string?> ResolveAsync(
        string shortCode,
        CancellationToken cancellationToken = default);

    Task<UrlAnalyticsResponse?> GetAnalyticsAsync(
        string shortCode,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string shortCode,
        CancellationToken cancellationToken = default);
}