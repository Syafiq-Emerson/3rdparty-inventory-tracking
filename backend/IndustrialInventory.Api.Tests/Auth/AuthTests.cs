using System.ComponentModel.DataAnnotations;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Services.Auth;
using Microsoft.AspNetCore.Identity;

namespace IndustrialInventory.Api.Tests.Auth;

public class AuthTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_password_is_rejected(string? password)
        => Assert.Equal("Enter a new password.", AuthService.DescribePasswordProblem(password));

    [Fact]
    public void Password_shorter_than_the_minimum_is_rejected()
        => Assert.Contains("at least 12 characters",
            AuthService.DescribePasswordProblem(new string('a', AuthService.MinimumPasswordLength - 1)));

    [Fact]
    public void Password_at_the_minimum_length_is_accepted()
        => Assert.Null(AuthService.DescribePasswordProblem(new string('a', AuthService.MinimumPasswordLength)));

    [Fact]
    public void Hash_verifies_the_same_password_only()
    {
        var user = new AppUser { Id = "usr-1", UserName = "operator" };
        var hash = AuthService.HashPasswordFor(user, "correct horse battery");
        var hasher = new PasswordHasher<AppUser>();

        Assert.DoesNotContain("correct horse battery", hash);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, hash, "correct horse battery"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, hash, "wrong password!!"));
    }

    [Fact]
    public void Empty_stored_hash_never_verifies()
        => Assert.Equal(PasswordVerificationResult.Failed,
            new PasswordHasher<AppUser>().VerifyHashedPassword(new AppUser(), string.Empty, "anything at all"));

    [Fact]
    public void Login_request_requires_user_name_and_password()
    {
        Assert.False(IsValid(new LoginRequest { UserName = "", Password = "x" }));
        Assert.False(IsValid(new LoginRequest { UserName = "admin", Password = "" }));
        Assert.False(IsValid(new LoginRequest { UserName = new string('u', 51), Password = "x" }));
        Assert.True(IsValid(new LoginRequest { UserName = "admin", Password = "x" }));
    }

    private static bool IsValid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), null, validateAllProperties: true);
}
