using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;
using IndustrialInventory.Api.Models.Audit;
using IndustrialInventory.Api.Services.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PersonnelController(
    InventoryDbContext db,
    AuditWriter audit,
    TimeProvider timeProvider) : ControllerBase
{
    // GET: /api/personnel
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonnelResponse>>> GetPersonnel(
        CancellationToken ct,
        [FromQuery] bool? activeOnly = null)
    {
        var query = db.Personnel.AsNoTracking();
        if (activeOnly is true) query = query.Where(p => p.IsActive);

        var list = await query
            .OrderBy(p => p.PersonnelCode)
            .Select(Projection)
            .ToListAsync(ct);

        return Ok(list);
    }

    // POST: /api/personnel
    [HttpPost]
    public async Task<ActionResult<PersonnelResponse>> CreatePersonnel(
        [FromBody] CreatePersonnelRequest request,
        CancellationToken ct)
    {
        var codeClean = request.PersonnelCode.Trim();
        if (await db.Personnel.AnyAsync(p => p.PersonnelCode == codeClean, ct))
        {
            return Conflict(new { error = $"Personnel code '{codeClean}' already exists." });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var newPersonnel = new Personnel
        {
            Id = Guid.NewGuid().ToString(),
            PersonnelCode = codeClean,
            Name = request.Name.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Personnel.Add(newPersonnel);
        audit.Add(AuditAction.Create, AuditEntity.Personnel, newPersonnel.Id,
            newValue: new { newPersonnel.PersonnelCode, newPersonnel.Name },
            referenceNo: newPersonnel.PersonnelCode);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetPersonnel), new { id = newPersonnel.Id }, new PersonnelResponse
        {
            Id = newPersonnel.Id,
            PersonnelCode = newPersonnel.PersonnelCode,
            Name = newPersonnel.Name,
            IsActive = newPersonnel.IsActive,
            RegisteredItemsCount = 0,
            CreatedAt = newPersonnel.CreatedAtUtc,
            UpdatedAt = newPersonnel.UpdatedAtUtc
        });
    }

    // PUT: /api/personnel/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<PersonnelResponse>> UpdatePersonnel(
        string id,
        [FromBody] UpdatePersonnelRequest request,
        CancellationToken ct)
    {
        var person = await db.Personnel.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (person is null) return NotFound(new { error = $"Personnel with ID '{id}' was not found." });

        var codeClean = request.PersonnelCode.Trim();
        if (await db.Personnel.AnyAsync(p => p.PersonnelCode == codeClean && p.Id != id, ct))
        {
            return Conflict(new { error = $"Personnel code '{codeClean}' is already in use by another person." });
        }

        var before = new { person.PersonnelCode, person.Name };

        person.PersonnelCode = codeClean;
        person.Name = request.Name.Trim();
        person.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        audit.Add(AuditAction.Update, AuditEntity.Personnel, person.Id,
            previousValue: before,
            newValue: new { person.PersonnelCode, person.Name },
            referenceNo: person.PersonnelCode);
        await db.SaveChangesAsync(ct);

        return Ok(await ReadBackAsync(id, ct));
    }

    // PATCH: /api/personnel/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<PersonnelResponse>> UpdateStatus(
        string id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken ct)
    {
        var person = await db.Personnel.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (person is null) return NotFound(new { error = $"Personnel with ID '{id}' was not found." });

        person.IsActive = request.IsActive;
        person.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        audit.Add(request.IsActive ? AuditAction.Restore : AuditAction.Deactivate,
            AuditEntity.Personnel, person.Id,
            previousValue: new { IsActive = !request.IsActive },
            newValue: new { request.IsActive },
            referenceNo: person.PersonnelCode);
        await db.SaveChangesAsync(ct);

        return Ok(await ReadBackAsync(id, ct));
    }

    private async Task<PersonnelResponse> ReadBackAsync(string id, CancellationToken ct) =>
        await db.Personnel.AsNoTracking().Where(p => p.Id == id).Select(Projection).FirstAsync(ct);

    internal static readonly System.Linq.Expressions.Expression<Func<Personnel, PersonnelResponse>> Projection =
        p => new PersonnelResponse
        {
            Id = p.Id,
            PersonnelCode = p.PersonnelCode,
            Name = p.Name,
            IsActive = p.IsActive,
            RegisteredItemsCount = p.RegisteredInventoryItems.Count,
            CreatedAt = p.CreatedAtUtc,
            UpdatedAt = p.UpdatedAtUtc
        };
}
