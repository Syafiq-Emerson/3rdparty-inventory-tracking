namespace IndustrialInventory.Api.DTOs;

public sealed record VersionDto(string Version, DateTime? BuildDate);

public sealed record DeployNoticeDto(int SecondsRemaining, string Message);
