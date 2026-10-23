using System.Net;
using System.Security.Claims;
using IndustrialInventory.Api.Models.Audit;
using IndustrialInventory.Api.Services.Shared;
using IndustrialInventory.Api.Tests.Support;
using Microsoft.AspNetCore.Http;

namespace IndustrialInventory.Api.Tests.Audit;

public class AuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    private static (AuditWriter Writer, Func<AuditEvent> Single) Writer(string? userName)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.UserAgent = "Scanner/1.0";
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        if (userName is not null)
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, userName)], "Test"));

        var db = OfflineDb.Create();
        var writer = new AuditWriter(db, new AuditContext(new HttpContextAccessor { HttpContext = http }), new FakeClock(Now));
        return (writer, () => Assert.Single(db.AuditEvents.Local));
    }

    [Fact]
    public void Row_carries_the_actor_request_details_and_clock_time()
    {
        var (writer, single) = Writer("operator1");

        writer.Add(AuditAction.Create, AuditEntity.StoreLocation, "loc-1", referenceNo: "WH-A-01");

        var row = single();
        Assert.Equal("operator1", row.UserName);
        Assert.Equal("Scanner/1.0", row.UserAgent);
        Assert.Equal("10.0.0.5", row.IpAddress);
        Assert.Equal(Now.UtcDateTime, row.OccurredAtUtc);
        Assert.Equal(AuditAction.Create, row.ActionType);
        Assert.Equal("loc-1", row.EntityId);
        Assert.NotNull(row.CorrelationId);
    }

    [Fact]
    public void Signed_out_caller_is_recorded_as_anonymous_unless_overridden()
    {
        var (writer, single) = Writer(null);
        writer.Add(AuditAction.LoginFailed, AuditEntity.AppUser, null);
        Assert.Equal(AuditContext.AnonymousActor, single().UserName);

        var (overridden, row) = Writer(null);
        overridden.Add(AuditAction.LoginFailed, AuditEntity.AppUser, null, userNameOverride: "typed-name");
        Assert.Equal("typed-name", row().UserName);
    }

    [Fact]
    public void Values_are_json_and_never_contain_secrets()
    {
        var (writer, single) = Writer("admin");

        writer.Add(AuditAction.Update, AuditEntity.AppUser, "usr-1",
            previousValue: new { Name = "old", PasswordHash = "AQAAAA-old" },
            newValue: new { Name = "new", Password = "hunter2hunter2", Token = "t" });

        var row = single();
        Assert.Equal("""{"Name":"old"}""", row.PreviousValue);
        Assert.Equal("""{"Name":"new"}""", row.NewValue);
    }

    [Fact]
    public void Object_with_only_secrets_writes_no_value()
    {
        var (writer, single) = Writer("admin");
        writer.Add(AuditAction.PasswordChanged, AuditEntity.AppUser, "usr-1", newValue: new { Password = "x" });
        Assert.Null(single().NewValue);
    }

    [Fact]
    public void Long_remarks_and_user_agents_are_cut_to_the_column_length()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.UserAgent = new string('u', 600);
        var db = OfflineDb.Create();
        new AuditWriter(db, new AuditContext(new HttpContextAccessor { HttpContext = http }), new FakeClock(Now))
            .Add(AuditAction.Post, AuditEntity.InventoryItem, "item-1", remarks: new string('r', 700));

        var row = Assert.Single(db.AuditEvents.Local);
        Assert.Equal(500, row.Remarks!.Length);
        Assert.Equal(400, row.UserAgent!.Length);
    }
}
