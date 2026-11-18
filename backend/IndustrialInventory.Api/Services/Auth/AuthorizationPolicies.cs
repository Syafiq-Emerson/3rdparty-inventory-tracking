using IndustrialInventory.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace IndustrialInventory.Api.Services.Auth;

public static class AuthorizationPolicies
{
    public const string InventoryLifecycle = nameof(InventoryLifecycle);

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(InventoryLifecycle, policy =>
            policy.RequireRole(UserRole.Admin, UserRole.Operator));
    }
}
