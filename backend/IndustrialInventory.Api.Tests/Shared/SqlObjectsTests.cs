using System.Reflection;
using System.Text.RegularExpressions;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.Sql;

namespace IndustrialInventory.Api.Tests.Shared;

public class SqlObjectsTests
{
    private static readonly IReadOnlyList<string> CreateStatements = typeof(SqlObjects)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.Name.StartsWith("Create", StringComparison.Ordinal))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();

    private static readonly Regex ObjectName = new(
        @"CREATE OR ALTER (?<type>VIEW|PROCEDURE|TRIGGER) \[(?<schema>\w+)\]\.\[(?<name>\w+)\]", RegexOptions.Compiled);

    [Fact]
    public void Objects_use_the_app_schema_and_naming_prefixes()
    {
        Assert.NotEmpty(CreateStatements);
        Assert.All(CreateStatements, sql =>
        {
            var m = ObjectName.Match(sql);
            Assert.True(m.Success, "Every Create constant must start with CREATE OR ALTER [schema].[name].");
            Assert.Equal(InventoryDbContext.Schema, m.Groups["schema"].Value);
            var prefix = m.Groups["type"].Value switch { "VIEW" => "V_", "PROCEDURE" => "SP_", _ => "TR_" };
            Assert.StartsWith(prefix, m.Groups["name"].Value);
        });
    }

    [Fact]
    public void AsBatch_doubles_quotes()
        => Assert.Equal("EXEC(N'SELECT N''x''')", SqlObjects.AsBatch("SELECT N'x'"));
}
