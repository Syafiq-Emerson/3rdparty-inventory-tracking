using IndustrialInventory.Api.Controllers.Shared;
using IndustrialInventory.Api.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialInventory.Api.Tests.Shared;

public class ServiceResultTests
{
    [Fact]
    public void Factories_set_the_status_code()
    {
        Assert.Equal(StatusCodes.Status200OK, ServiceResult<int>.Ok(1).StatusCode);
        Assert.Equal(StatusCodes.Status201Created, ServiceResult<int>.Created(1).StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, ServiceResult<int>.Fail("bad").StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, ServiceResult<int>.Fail("no", StatusCodes.Status401Unauthorized).StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, ServiceResult<int>.NotFound("gone").StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, ServiceResult<int>.Conflict("clash").StatusCode);
    }

    [Fact]
    public void Success_is_the_absence_of_an_error()
    {
        Assert.True(ServiceResult<string>.Ok("v").Succeeded);
        Assert.False(ServiceResult<string>.Fail("e").Succeeded);
    }

    [Fact]
    public void Cast_carries_the_failure_to_another_type()
    {
        var cast = ServiceResult<int>.Conflict("clash").Cast<string>();

        Assert.Equal("clash", cast.Error);
        Assert.Equal(StatusCodes.Status409Conflict, cast.StatusCode);
        Assert.Null(cast.Value);
    }

    [Fact]
    public void Success_maps_to_the_value_with_its_status()
    {
        var result = Assert.IsType<ObjectResult>(ServiceResult<string>.Created("v").ToActionResult().Result);

        Assert.Equal("v", result.Value);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    [Fact]
    public void Failure_maps_to_the_error_envelope()
    {
        var result = Assert.IsType<ObjectResult>(ServiceResult<string>.NotFound("Not here.").ToActionResult().Result);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Not here.", result.Value!.GetType().GetProperty("error")!.GetValue(result.Value));
    }

    [Fact]
    public void No_content_mapping_keeps_failures()
    {
        Assert.IsType<NoContentResult>(ServiceResult<string>.Ok("v").ToNoContent());
        Assert.Equal(StatusCodes.Status400BadRequest,
            Assert.IsType<ObjectResult>(ServiceResult<string>.Fail("bad").ToNoContent()).StatusCode);
    }
}
