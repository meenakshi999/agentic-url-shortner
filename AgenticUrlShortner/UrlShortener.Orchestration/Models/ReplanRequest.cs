
namespace UrlShortener.Orchestration.Models
{
    public sealed record ReplanRequest(string ChangedTaskId, string NewOutput);
}
