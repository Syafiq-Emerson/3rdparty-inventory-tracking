using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Services.Deploy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/system")]
public sealed class DeployNoticeController(DeployNoticeService notices) : ControllerBase
{
    [HttpGet("deploy-notice")]
    public ActionResult<DeployNoticeDto> DeployNotice() => notices.Current() is { } notice ? notice : NoContent();
}
