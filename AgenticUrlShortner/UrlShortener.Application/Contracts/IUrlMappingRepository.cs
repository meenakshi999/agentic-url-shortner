using UrlShortener.Domain;

namespace UrlShortener.Application.Contracts;

public interface IUrlMappingRepository
{
    Task<UrlMapping?> GetByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        UrlMapping mapping,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    void Remove(UrlMapping mapping);

    Task IncrementClickCountAsync(
    string shortCode,
    CancellationToken cancellationToken = default);
}