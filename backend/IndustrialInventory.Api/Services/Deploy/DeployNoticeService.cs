using System.Text.Json;
using IndustrialInventory.Api.DTOs;

namespace IndustrialInventory.Api.Services.Deploy;

/// <summary>
/// Reads App_Data/deploy-notice.json, which the DeployIIS publish profile writes before it takes the
/// site offline, so open pages can count down to the update.
/// </summary>
public sealed class DeployNoticeService(IWebHostEnvironment env, TimeProvider clock)
{
    public const string FileName = "deploy-notice.json";
    public const int MaxMessageLength = 200;
    public const string DefaultMessage =
        "Industrial Inventory is about to be updated. Save your work now; the app will be unavailable for a minute or two.";
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private readonly string path = Path.Combine(env.ContentRootPath, "App_Data", FileName);

    public DeployNoticeDto? Current()
    {
        string json;
        try
        {
            if (!File.Exists(path)) return null;
            json = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        return Evaluate(json, clock.GetUtcNow());
    }

    internal static DeployNoticeDto? Evaluate(string json, DateTimeOffset now)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("startsAt", out var startsAtValue) || startsAtValue.ValueKind != JsonValueKind.String
                || !startsAtValue.TryGetDateTimeOffset(out var startsAt)) return null;
            // A notice left behind by a publish that crashed must not block the page forever.
            if (now - startsAt > StaleAfter) return null;

            var message = root.TryGetProperty("message", out var messageValue) && messageValue.ValueKind == JsonValueKind.String
                ? messageValue.GetString()?.Trim()
                : null;
            if (string.IsNullOrEmpty(message)) message = DefaultMessage;
            else if (message.Length > MaxMessageLength) message = message[..MaxMessageLength];

            var seconds = (int)Math.Ceiling(Math.Max(0, (startsAt - now).TotalSeconds));
            return new DeployNoticeDto(seconds, message);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
