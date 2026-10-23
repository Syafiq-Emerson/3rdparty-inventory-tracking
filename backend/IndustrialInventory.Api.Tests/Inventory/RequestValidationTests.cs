using System.ComponentModel.DataAnnotations;
using IndustrialInventory.Api.DTOs;

namespace IndustrialInventory.Api.Tests.Inventory;

public class RequestValidationTests
{
    [Fact]
    public void Register_request_needs_all_four_fields()
    {
        Assert.True(IsValid(Register()));
        Assert.False(IsValid(Register() with { SerialNumber = "" }));
        Assert.False(IsValid(Register() with { TagNumber = "   " }));
        Assert.False(IsValid(Register() with { StoreLocationId = "" }));
        Assert.False(IsValid(Register() with { ScanByPersonnelId = "" }));
    }

    [Fact]
    public void Identifiers_longer_than_the_column_are_rejected()
        => Assert.False(IsValid(Register() with { SerialNumber = new string('S', 101) }));

    [Fact]
    public void Location_code_and_name_follow_the_column_lengths()
    {
        Assert.True(IsValid(new CreateStoreLocationRequest { LocationCode = "WH-A-01", LocationName = "Warehouse A" }));
        Assert.False(IsValid(new CreateStoreLocationRequest { LocationCode = new string('C', 51), LocationName = "x" }));
        Assert.False(IsValid(new UpdateStoreLocationRequest { LocationCode = "WH", LocationName = new string('N', 151) }));
    }

    [Fact]
    public void Personnel_code_and_name_are_required()
    {
        Assert.True(IsValid(new CreatePersonnelRequest { PersonnelCode = "EMP-1", Name = "A. Person" }));
        Assert.False(IsValid(new CreatePersonnelRequest { PersonnelCode = "", Name = "A. Person" }));
        Assert.False(IsValid(new UpdatePersonnelRequest { PersonnelCode = "EMP-1", Name = "" }));
    }

    private static RegisterInventoryRequest Register() => new()
    {
        SerialNumber = "F005327261",
        TagNumber = "PV-16501",
        StoreLocationId = "loc-1",
        ScanByPersonnelId = "per-1"
    };

    private static bool IsValid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), null, validateAllProperties: true);
}
