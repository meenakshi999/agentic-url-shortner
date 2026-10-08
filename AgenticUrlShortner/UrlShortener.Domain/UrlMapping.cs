namespace UrlShortener.Domain;

public class UrlMapping
{
    public Guid Id { get; set; }

    public string ShortCode { get; set; } = string.Empty;

    public string OriginalUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public long ClickCount { get; set; }

    public bool IsActive { get; set; } = true;
}