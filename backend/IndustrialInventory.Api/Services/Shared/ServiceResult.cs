namespace IndustrialInventory.Api.Services.Shared;

/// <summary>
/// Expected outcomes of a service call. Unexpected failures stay as exceptions and are
/// turned into a 500 by GlobalExceptionHandler.
/// </summary>
public sealed record ServiceResult<T>(T? Value, string? Error, int StatusCode)
{
    public bool Succeeded => Error is null;

    public static ServiceResult<T> Ok(T value) => new(value, null, StatusCodes.Status200OK);

    public static ServiceResult<T> Created(T value) => new(value, null, StatusCodes.Status201Created);

    public static ServiceResult<T> Fail(string error, int status = StatusCodes.Status400BadRequest) =>
        new(default, error, status);

    public static ServiceResult<T> NotFound(string error) =>
        new(default, error, StatusCodes.Status404NotFound);

    public static ServiceResult<T> Conflict(string error) =>
        new(default, error, StatusCodes.Status409Conflict);

    /// <summary>Carries a failure across to a different result type.</summary>
    public ServiceResult<TOther> Cast<TOther>() => new(default, Error, StatusCode);
}
