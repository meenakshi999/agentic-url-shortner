using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Contracts;
using UrlShortener.Domain;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure.Repositories;

public sealed class UrlMappingRepository : IUrlMappingRepository
{
    private readonly UrlShortenerDbContext _dbContext;

    public UrlMappingRepository(UrlShortenerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UrlMapping?> GetByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UrlMappings
            .SingleOrDefaultAsync(
                x => x.ShortCode == shortCode,
                cancellationToken);
    }

    public async Task AddAsync(
        UrlMapping mapping,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.UrlMappings.AddAsync(
            mapping,
            cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Remove(UrlMapping mapping)
    {
        _dbContext.UrlMappings.Remove(mapping);
    }
    public async Task IncrementClickCountAsync(
    string shortCode,
    CancellationToken cancellationToken = default)
    {
        await _dbContext.UrlMappings
            .Where(x => x.ShortCode == shortCode)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.ClickCount,
                        x => x.ClickCount + 1),
                cancellationToken);
    }
}