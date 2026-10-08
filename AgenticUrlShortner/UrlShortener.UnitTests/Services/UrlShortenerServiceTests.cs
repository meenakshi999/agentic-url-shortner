using Moq;
using UrlShortener.Application.Contracts;
using UrlShortener.Application.Services;

namespace UrlShortener.UnitTests.Services;

public class UrlShortenerServiceTests
{
    [Fact]
    public async Task CreateAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        var repository = new Mock<IUrlMappingRepository>();

        var service = new UrlShortenerService(
            repository.Object);

        var request = new CreateShortUrlRequest(
            "not-a-valid-url",
            null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_WithPastExpiration_ThrowsArgumentException()
    {
        var repository = new Mock<IUrlMappingRepository>();

        var service = new UrlShortenerService(
            repository.Object);

        var request = new CreateShortUrlRequest(
            "https://example.com",
            DateTime.UtcNow.AddMinutes(-5));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(request));
    }
    [Fact]
    public async Task CreateAsync_WithValidUrl_CreatesShortUrl()
    {
        var repository = new Mock<IUrlMappingRepository>();

        UrlShortener.Domain.UrlMapping? savedMapping = null;

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<UrlShortener.Domain.UrlMapping>(),
                It.IsAny<CancellationToken>()))
            .Callback<UrlShortener.Domain.UrlMapping, CancellationToken>(
                (mapping, _) => savedMapping = mapping)
            .Returns(Task.CompletedTask);

        repository
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new UrlShortenerService(
            repository.Object);

        var request = new CreateShortUrlRequest(
            "https://example.com/products",
            null);

        var result = await service.CreateAsync(request);

        Assert.NotNull(result);
        Assert.NotEmpty(result.ShortCode);
        Assert.Equal(
            "https://example.com/products",
            result.OriginalUrl);

        Assert.NotNull(savedMapping);
        Assert.Equal(
            result.ShortCode,
            savedMapping.ShortCode);
    }
    [Fact]
    public async Task ResolveAsync_WithExpiredUrl_ReturnsNull()
    {
        var repository = new Mock<IUrlMappingRepository>();

        var mapping = new UrlShortener.Domain.UrlMapping
        {
            Id = Guid.NewGuid(),
            ShortCode = "abc123",
            OriginalUrl = "https://example.com",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ClickCount = 0,
            IsActive = true
        };

        repository
            .Setup(x => x.GetByShortCodeAsync(
                "abc123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mapping);

        repository
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new UrlShortenerService(
            repository.Object);

        var result = await service.ResolveAsync("abc123");

        Assert.Null(result);
        Assert.False(mapping.IsActive);

        repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task ResolveAsync_WithActiveUrl_IncrementsClickCount()
    {
        var repository = new Mock<IUrlMappingRepository>();

        var mapping = new UrlShortener.Domain.UrlMapping
        {
            Id = Guid.NewGuid(),
            ShortCode = "abc123",
            OriginalUrl = "https://example.com",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            ExpiresAtUtc = null,
            ClickCount = 5,
            IsActive = true
        };

        repository
            .Setup(x => x.GetByShortCodeAsync(
                "abc123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mapping);

        repository
            .Setup(x => x.IncrementClickCountAsync(
                "abc123",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new UrlShortenerService(
            repository.Object);

        var result = await service.ResolveAsync("abc123");

        Assert.Equal(
            "https://example.com",
            result);

        repository.Verify(
            x => x.IncrementClickCountAsync(
                "abc123",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task ResolveAsync_WithUnknownShortCode_ReturnsNull()
    {
        var repository = new Mock<IUrlMappingRepository>();

        repository
            .Setup(x => x.GetByShortCodeAsync(
                "missing",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UrlShortener.Domain.UrlMapping?)null);

        var service = new UrlShortenerService(
            repository.Object);

        var result = await service.ResolveAsync("missing");

        Assert.Null(result);

        repository.Verify(
            x => x.IncrementClickCountAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }


    [Fact]
    public async Task ResolveAsync_WithInactiveUrl_ReturnsNull()
    {
        var repository = new Mock<IUrlMappingRepository>();

        var mapping = new UrlShortener.Domain.UrlMapping
        {
            Id = Guid.NewGuid(),
            ShortCode = "inactive",
            OriginalUrl = "https://example.com",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            ExpiresAtUtc = null,
            ClickCount = 2,
            IsActive = false
        };

        repository
            .Setup(x => x.GetByShortCodeAsync(
                "inactive",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mapping);

        var service = new UrlShortenerService(
            repository.Object);

        var result = await service.ResolveAsync("inactive");

        Assert.Null(result);

        repository.Verify(
            x => x.IncrementClickCountAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }


    [Fact]
    public async Task GetAnalyticsAsync_WithExistingUrl_ReturnsAnalytics()
    {
        var repository = new Mock<IUrlMappingRepository>();

        var mapping = new UrlShortener.Domain.UrlMapping
        {
            Id = Guid.NewGuid(),
            ShortCode = "abc123",
            OriginalUrl = "https://example.com",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            ClickCount = 7,
            IsActive = true
        };

        repository
            .Setup(x => x.GetByShortCodeAsync(
                "abc123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mapping);

        var service = new UrlShortenerService(
            repository.Object);

        var result = await service.GetAnalyticsAsync("abc123");

        Assert.NotNull(result);
        Assert.Equal("abc123", result.ShortCode);
        Assert.Equal("https://example.com", result.OriginalUrl);
        Assert.Equal(7, result.ClickCount);
        Assert.True(result.IsActive);
    }


    [Fact]
    public async Task DeleteAsync_WithExistingUrl_DeactivatesUrl()
    {
        var repository = new Mock<IUrlMappingRepository>();

        var mapping = new UrlShortener.Domain.UrlMapping
        {
            Id = Guid.NewGuid(),
            ShortCode = "delete1",
            OriginalUrl = "https://example.com",
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            ClickCount = 3,
            IsActive = true
        };

        repository
            .Setup(x => x.GetByShortCodeAsync(
                "delete1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mapping);

        repository
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new UrlShortenerService(
            repository.Object);

        var result = await service.DeleteAsync("delete1");

        Assert.True(result);
        Assert.False(mapping.IsActive);

        repository.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}