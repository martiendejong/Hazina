using Hazina.API.Generic.Controllers;
using Hazina.Demo.GenericApi.Entities;
using Hazina.Tools.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;

namespace Hazina.Demo.GenericApi.Controllers;

/// <summary>
/// Notes API - demonstrates minimal generic controller usage.
/// Just 3 lines of code for full CRUD!
/// </summary>
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "Notes")]
public class NotesController : GenericEntityController<Note>
{
    public NotesController(IRepository<Note> repository) : base(repository)
    {
    }

    /// <summary>
    /// GET /api/notes/pinned
    /// Get all pinned notes
    /// </summary>
    [HttpGet("pinned")]
    public async Task<ActionResult<List<Note>>> GetPinned()
    {
        var notes = await Repository.FindAsync(n => n.IsPinned);
        return Ok(notes);
    }

    /// <summary>
    /// GET /api/notes/by-category/{category}
    /// Get notes by category
    /// </summary>
    [HttpGet("by-category/{category}")]
    public async Task<ActionResult<List<Note>>> GetByCategory(string category)
    {
        var notes = await Repository.FindAsync(n => n.Category == category);
        return Ok(notes);
    }

    /// <summary>
    /// POST /api/notes/{id}/pin
    /// Pin or unpin a note
    /// </summary>
    [HttpPost("{id}/pin")]
    public async Task<ActionResult<Note>> TogglePin(Guid id)
    {
        var note = await Repository.GetByIdAsync(id);
        if (note == null)
            return NotFound();

        note.IsPinned = !note.IsPinned;
        note.UpdatedAt = DateTime.UtcNow;
        await Repository.UpdateAsync(note);
        await Repository.SaveChangesAsync();

        return Ok(note);
    }

    /// <summary>
    /// Support filtering by title or content
    /// </summary>
    protected override Expression<Func<Note, bool>>? BuildFilterExpression(string filter)
    {
        var lowerFilter = filter.ToLowerInvariant();
        return n => n.Title.ToLower().Contains(lowerFilter) ||
                   (n.Content != null && n.Content.ToLower().Contains(lowerFilter)) ||
                   (n.Category != null && n.Category.ToLower().Contains(lowerFilter));
    }
}
