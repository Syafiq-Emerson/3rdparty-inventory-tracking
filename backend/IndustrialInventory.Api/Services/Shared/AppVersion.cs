using System.Globalization;
using System.Reflection;

namespace IndustrialInventory.Api.Services.Shared;

/// <summary>&lt;Version&gt; from the csproj (AssemblyInformationalVersion) and the BuildDate metadata it stamps.</summary>
public static class AppVersion
{
    private static readonly Assembly Assembly = typeof(AppVersion).Assembly;

    public static string Current { get; } =
        Clean(Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static DateTime? BuildDate { get; } = ParseBuildDate(
        Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "BuildDate")?.Value);

    // The SDK appends "+<commit sha>" to the informational version; people only care about the number.
    internal static string Clean(string? informational) =>
        string.IsNullOrWhiteSpace(informational) ? "0.0.0" : informational.Split('+')[0].Trim();

    internal static DateTime? ParseBuildDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;
}
