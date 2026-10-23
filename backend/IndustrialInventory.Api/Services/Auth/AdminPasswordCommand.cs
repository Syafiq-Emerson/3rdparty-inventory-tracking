using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Services.Auth;

/// <summary>
/// One-time setup for the seeded admin account. The password is typed into the console and is
/// never passed as an argument, so it does not land in shell history or the process list.
/// </summary>
public static class AdminPasswordCommand
{
    public const string Verb = "set-admin-password";

    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await db.Database.MigrateAsync(ct);

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.UserName == "admin", ct);
        if (user is null)
        {
            Console.Error.WriteLine("No 'admin' account exists. Apply migrations first, then retry.");
            return 1;
        }

        Console.WriteLine($"Setting the password for '{user.UserName}'.");
        Console.WriteLine($"It must be at least {AuthService.MinimumPasswordLength} characters. Input is hidden.");

        var password = ReadHiddenLine("New password: ");
        var problem = AuthService.DescribePasswordProblem(password);
        if (problem is not null)
        {
            Console.Error.WriteLine(problem);
            return 1;
        }

        if (!string.Equals(password, ReadHiddenLine("Confirm password: "), StringComparison.Ordinal))
        {
            Console.Error.WriteLine("The two entries do not match. Nothing was changed.");
            return 1;
        }

        user.PasswordHash = AuthService.HashPasswordFor(user, password);
        user.MustChangePassword = false;
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        Console.WriteLine($"The password for '{user.UserName}' is set. You can now sign in.");
        return 0;
    }

    private static string ReadHiddenLine(string prompt)
    {
        Console.Write(prompt);

        // A redirected console has no key reader; fall back to a plain read.
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var entered = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;

            if (key.Key == ConsoleKey.Backspace)
            {
                if (entered.Length > 0) entered.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar)) entered.Append(key.KeyChar);
        }

        Console.WriteLine();
        return entered.ToString();
    }
}
