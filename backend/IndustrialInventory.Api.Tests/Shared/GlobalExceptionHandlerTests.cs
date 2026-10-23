using System.Text.Json;
using IndustrialInventory.Api.Services.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IndustrialInventory.Api.Tests.Shared;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Unhandled_exception_becomes_a_500_with_a_reference_code_and_no_details()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("secret detail"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("trace-123", body.RootElement.GetProperty("traceId").GetString());
        Assert.StartsWith("Something went wrong on the server.", body.RootElement.GetProperty("error").GetString());
        Assert.DoesNotContain("secret detail", body.RootElement.ToString());
    }
}
