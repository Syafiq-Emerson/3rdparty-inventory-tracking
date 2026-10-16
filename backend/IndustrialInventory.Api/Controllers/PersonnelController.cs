using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IndustrialInventory.Api.Data;
using IndustrialInventory.Api.DTOs;
using IndustrialInventory.Api.Models;

namespace IndustrialInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonnelController : ControllerBase
{
    private readonly InventoryDbContext _context;

    public PersonnelController(InventoryDbContext context)
    {
        _context = context;
    }

    // GET: /api/personnel
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonnelResponse>>> GetPersonnel([FromQuery] bool? activeOnly = null)
    {
        var query = _context.Personnel.AsNoTracking();

        if (activeOnly.HasValue && activeOnly.Value)
        {
            query = query.Where(p => p.IsActive);
        }

        var list = await query
            .OrderBy(p => p.PersonnelCode)
            .Select(p => new PersonnelResponse
            {
                Id = p.Id,
                PersonnelCode = p.PersonnelCode,
                Name = p.Name,
                IsActive = p.IsActive,
                RegisteredItemsCount = p.RegisteredInventoryItems.Count,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    // POST: /api/personnel
    [HttpPost]
    public async Task<ActionResult<PersonnelResponse>> CreatePersonnel([FromBody] CreatePersonnelRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var codeClean = request.PersonnelCode.Trim();
        var exists = await _context.Personnel.AnyAsync(p => p.PersonnelCode == codeClean);
        if (exists)
        {
            return Conflict(new { message = $"Personnel code '{codeClean}' already exists." });
        }

        var newPersonnel = new Personnel
        {
            Id = Guid.NewGuid().ToString(),
            PersonnelCode = codeClean,
            Name = request.Name.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Personnel.Add(newPersonnel);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPersonnel), new { id = newPersonnel.Id }, new PersonnelResponse
        {
            Id = newPersonnel.Id,
            PersonnelCode = newPersonnel.PersonnelCode,
            Name = newPersonnel.Name,
            IsActive = newPersonnel.IsActive,
            RegisteredItemsCount = 0,
            CreatedAt = newPersonnel.CreatedAt,
            UpdatedAt = newPersonnel.UpdatedAt
        });
    }

    // PUT: /api/personnel/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<PersonnelResponse>> UpdatePersonnel(string id, [FromBody] UpdatePersonnelRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var person = await _context.Personnel.Include(p => p.RegisteredInventoryItems).FirstOrDefaultAsync(p => p.Id == id);
        if (person == null) return NotFound(new { message = $"Personnel with ID '{id}' was not found." });

        var codeClean = request.PersonnelCode.Trim();
        var codeInUse = await _context.Personnel.AnyAsync(p => p.PersonnelCode == codeClean && p.Id != id);
        if (codeInUse)
        {
            return Conflict(new { message = $"Personnel code '{codeClean}' is already in use by another person." });
        }

        person.PersonnelCode = codeClean;
        person.Name = request.Name.Trim();
        person.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new PersonnelResponse
        {
            Id = person.Id,
            PersonnelCode = person.PersonnelCode,
            Name = person.Name,
            IsActive = person.IsActive,
            RegisteredItemsCount = person.RegisteredInventoryItems.Count,
            CreatedAt = person.CreatedAt,
            UpdatedAt = person.UpdatedAt
        });
    }

    // PATCH: /api/personnel/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<PersonnelResponse>> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        var person = await _context.Personnel.Include(p => p.RegisteredInventoryItems).FirstOrDefaultAsync(p => p.Id == id);
        if (person == null) return NotFound(new { message = $"Personnel with ID '{id}' was not found." });

        person.IsActive = request.IsActive;
        person.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new PersonnelResponse
        {
            Id = person.Id,
            PersonnelCode = person.PersonnelCode,
            Name = person.Name,
            IsActive = person.IsActive,
            RegisteredItemsCount = person.RegisteredInventoryItems.Count,
            CreatedAt = person.CreatedAt,
            UpdatedAt = person.UpdatedAt
        });
    }
}
