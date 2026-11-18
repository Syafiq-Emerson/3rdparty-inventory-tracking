using System.Reflection;
using System.Security.Claims;
using IndustrialInventory.Api.Controllers;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialInventory.Api.Tests.Auth;

public class AuthorizationPolicyTests
{
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Operator)]
    public async Task Lifecycle_policy_accepts_both_existing_roles(string role)
    {
        var options = new AuthorizationOptions();
        AuthorizationPolicies.Configure(options);
        var policy = Assert.IsType<AuthorizationPolicy>(options.GetPolicy(AuthorizationPolicies.InventoryLifecycle));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "test"));
        var context = new AuthorizationHandlerContext(policy.Requirements, principal, null);

        var requirement = Assert.IsType<RolesAuthorizationRequirement>(Assert.Single(policy.Requirements));
        await requirement.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Authenticated_principal_without_an_allowed_role_is_denied()
    {
        var options = new AuthorizationOptions();
        AuthorizationPolicies.Configure(options);
        var policy = Assert.IsType<AuthorizationPolicy>(options.GetPolicy(AuthorizationPolicies.InventoryLifecycle));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "tester")],
            authenticationType: "test"));
        var context = new AuthorizationHandlerContext(policy.Requirements, principal, null);

        var requirement = Assert.IsType<RolesAuthorizationRequirement>(Assert.Single(policy.Requirements));
        await requirement.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public void Lifecycle_policy_is_action_scoped_and_reads_remain_authenticated_only()
    {
        var controllerAuthorization = Assert.Single(typeof(InventoryController)
            .GetCustomAttributes<AuthorizeAttribute>());
        Assert.Null(controllerAuthorization.Policy);
        Assert.Null(controllerAuthorization.Roles);

        var readAction = typeof(InventoryController).GetMethod(nameof(InventoryController.GetInventory));
        Assert.NotNull(readAction);
        Assert.Empty(readAction.GetCustomAttributes<AuthorizeAttribute>());

        AssertPolicy(nameof(InventoryController.RegisterItem));
        AssertPolicy(nameof(InventoryController.IssueForPacking));
    }

    private static void AssertPolicy(string actionName)
    {
        var action = typeof(InventoryController).GetMethod(actionName);
        Assert.NotNull(action);
        var authorization = Assert.Single(action.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(AuthorizationPolicies.InventoryLifecycle, authorization.Policy);
    }
}
