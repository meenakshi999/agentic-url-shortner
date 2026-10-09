namespace UrlShortener.Orchestration.Policies;

public enum PolicyAction { Allow, RequireApproval, Deny }

public sealed record PolicyResult(
    PolicyAction Action,
    string PolicyName,
    string Reason)
{
    public bool IsAllowed => Action == PolicyAction.Allow;
}
