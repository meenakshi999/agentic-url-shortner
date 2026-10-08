using System.Security.Cryptography;
using UrlShortener.Application.Contracts;
using UrlShortener.Domain;
using Microsoft.EntityFrameworkCore;

namespace UrlShortener.Application.Services;

public sealed class UrlShortenerService : IUrlShortenerService
{
    private const int ShortCodeLength = 7;

    private readonly IUrlMappingRepository _repository;

    public UrlShortenerService(IUrlMappingRepository repository)
    {
        _repository = repository;
    }

    public async Task<ShortUrlResponse> CreateAsync(
    CreateShortUrlRequest request,
    CancellationToken cancellationToken = default)
    {
        ValidateUrl(request.OriginalUrl);
        ValidateExpiration(request.ExpiresAtUtc);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var shortCode = await GenerateUniqueShortCodeAsync(
                cancellationToken);

            var mapping = new UrlMapping
            {
                Id = Guid.NewGuid(),
                ShortCode = shortCode,
                OriginalUrl = request.OriginalUrl,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = request.ExpiresAtUtc,
                ClickCount = 0,
                IsActive = true
            };

            await _repository.AddAsync(mapping, cancellationToken);

            try
            {
                await _repository.SaveChangesAsync(cancellationToken);

                return ToResponse(mapping);
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                // A concurrent request may have created the same short code.
                // Retry with a newly generated code.
            }
        }

        throw new InvalidOperationException(
            "Unable to create a unique short URL after multiple attempts.");
    }

    public async Task<string?> ResolveAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _repository.GetByShortCodeAsync(
            shortCode,
            cancellationToken);

        if (mapping is null || !mapping.IsActive)
        {
            return null;
        }

        if (mapping.ExpiresAtUtc.HasValue &&
            mapping.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            mapping.IsActive = false;
            await _repository.SaveChangesAsync(cancellationToken);

            return null;
        }

        await _repository.IncrementClickCountAsync(shortCode,cancellationToken);

        return mapping.OriginalUrl;
    }

    public async Task<UrlAnalyticsResponse?> GetAnalyticsAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _repository.GetByShortCodeAsync(
            shortCode,
            cancellationToken);

        if (mapping is null)
        {
            return null;
        }

        var isActive = mapping.IsActive &&
                       (!mapping.ExpiresAtUtc.HasValue ||
                        mapping.ExpiresAtUtc.Value > DateTime.UtcNow);

        return new UrlAnalyticsResponse(
            mapping.ShortCode,
            mapping.OriginalUrl,
            mapping.ClickCount,
            mapping.CreatedAtUtc,
            mapping.ExpiresAtUtc,
            isActive);
    }

    public async Task<bool> DeleteAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _repository.GetByShortCodeAsync(
            shortCode,
            cancellationToken);

        if (mapping is null)
        {
            return false;
        }

        mapping.IsActive = false;

        await _repository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<string> GenerateUniqueShortCodeAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var shortCode = GenerateShortCode();

            var existing = await _repository.GetByShortCodeAsync(
                shortCode,
                cancellationToken);

            if (existing is null)
            {
                return shortCode;
            }
        }

        throw new InvalidOperationException(
            "Unable to generate a unique short code.");
    }

    private static string GenerateShortCode()
    {
        const string characters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        Span<char> buffer = stackalloc char[ShortCodeLength];

        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = characters[
                RandomNumberGenerator.GetInt32(characters.Length)];
        }

        return new string(buffer);
    }

    private static void ValidateUrl(string originalUrl)
    {
        if (!Uri.TryCreate(
                originalUrl,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "A valid HTTP or HTTPS URL is required.",
                nameof(originalUrl));
        }
    }

    private static void ValidateExpiration(DateTime? expiresAtUtc)
    {
        if (expiresAtUtc.HasValue &&
            expiresAtUtc.Value <= DateTime.UtcNow)
        {
            throw new ArgumentException(
                "Expiration time must be in the future.",
                nameof(expiresAtUtc));
        }
    }

    private static ShortUrlResponse ToResponse(
        UrlMapping mapping)
    {
        return new ShortUrlResponse(
            mapping.Id,
            mapping.ShortCode,
            mapping.OriginalUrl,
            $"/api/urls/{mapping.ShortCode}",
            mapping.CreatedAtUtc,
            mapping.ExpiresAtUtc,
            mapping.ClickCount);
    }
}