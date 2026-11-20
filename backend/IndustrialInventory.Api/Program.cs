using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Services.Auth;
using IndustrialInventory.Api.Services.Deploy;
using IndustrialInventory.Api.Services.Security;
using IndustrialInventory.Api.Services.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog();
}

var connectionString = builder.Configuration.GetConnectionString("InventoryConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The database connection string is not set. Set the ConnectionStrings__InventoryConnection environment variable.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditContext>();
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IAppUserAccountLookup, EfAppUserAccountLookup>();
builder.Services.AddScoped<CookiePrincipalValidator>();
builder.Services.AddScoped<AppCookieAuthenticationEvents>();
builder.Services.AddScoped<AntiforgeryAuthorizationFilter>();
builder.Services.AddSingleton<IdempotencyGate>();
builder.Services.AddSingleton<DeployNoticeService>();
builder.Services.AddSingleton(new ExactOriginAllowlist(
    builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? []));

builder.Services.AddDbContext<InventoryDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
    {
        sql.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema);
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
    });
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "IndustrialInventory.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddControllers(options =>
        options.Filters.AddService<AntiforgeryAuthorizationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

const string LoginRateLimitPolicy = "login";
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(LoginRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many attempts. Wait a minute and try again." }, ct);
    };
});

var keysDirectory = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys"));
keysDirectory.Create();
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("IndustrialInventory")
    .PersistKeysToFileSystem(keysDirectory);
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "IndustrialInventory.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.EventsType = typeof(AppCookieAuthenticationEvents);
    });

builder.Services.AddAuthorization(AuthorizationPolicies.Configure);

// Vite proxies /api in development, so calls are same-origin and need no CORS policy.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

// One-time console setup: "dotnet run -- set-admin-password". Does not start the web host.
if (args.Length > 0 && args[0] == AdminPasswordCommand.Verb)
{
    return await AdminPasswordCommand.RunAsync(app.Services, CancellationToken.None);
}

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    try
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending migration(s): {Names}", pending.Count, string.Join(", ", pending));
        }
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed, so the server did not start.");
        throw;
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});

// HTTP-only internal site: no HSTS or HTTPS redirection (see README).
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExactOriginMiddleware>();

app.MapControllers();

app.MapGet("/api/{**path}", (string path) => Results.NotFound(new
{
    error = $"The endpoint /api/{path} does not exist. If you just updated the app, rebuild and restart the server."
})).AllowAnonymous();

app.MapFallbackToFile("/index.html").AllowAnonymous();

app.Run();
return 0;
