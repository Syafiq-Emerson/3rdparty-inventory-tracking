using IndustrialInventory.Api.Services.Shared;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialInventory.Api.Controllers.Shared;

/// <summary>Maps a <see cref="ServiceResult{T}"/> onto an action result with the standard { error } body.</summary>
public static class ControllerResults
{
    public static ActionResult<T> ToActionResult<T>(this ServiceResult<T> result) =>
        result.Succeeded
            ? new ObjectResult(result.Value) { StatusCode = result.StatusCode }
            : new ObjectResult(new { error = result.Error }) { StatusCode = result.StatusCode };

    public static ActionResult ToNoContent<T>(this ServiceResult<T> result) =>
        result.Succeeded
            ? new NoContentResult()
            : new ObjectResult(new { error = result.Error }) { StatusCode = result.StatusCode };
}
