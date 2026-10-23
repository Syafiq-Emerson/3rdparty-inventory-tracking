using System.Security.Claims;

namespace IndustrialInventory.Api.Services.Shared;

/// <summary>Who and where the current request comes from, for audit rows.</summary>
public sealed class AuditContext(IHttpContextAccessor accessor)
{
    public const string SystemActor = "System";
    public const string AnonymousActor = "Anonymous";

    private const int UserNameMaxLength = 50;
    private const int UserAgentMaxLength = 400;
    private const int IpAddressMaxLength = 45;

    public Guid CorrelationId { get; } = Guid.NewGuid();

    public string UserName
    {
        get
        {
            var name = accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrWhiteSpace(name)) return AnonymousActor;
            return name.Length <= UserNameMaxLength ? name : name[..UserNameMaxLength];
        }
    }

    public string? UserAgent =>
        Truncate(accessor.HttpContext?.Request.Headers.UserAgent.ToString(), UserAgentMaxLength);

    public string? IpAddress =>
        Truncate(accessor.HttpContext?.Connection.RemoteIpAddress?.ToString(), IpAddressMaxLength);

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null
        : value.Length <= maxLength ? value
        : value[..maxLength];
}
