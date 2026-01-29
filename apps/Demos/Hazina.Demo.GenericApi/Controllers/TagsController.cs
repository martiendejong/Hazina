using Hazina.API.Generic.Controllers;
using Hazina.Demo.GenericApi.Entities;
using Hazina.Tools.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Hazina.Demo.GenericApi.Controllers;

/// <summary>
/// Tags API - the simplest possible generic controller.
/// Literally just one line of actual code!
/// </summary>
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "Tags")]
public class TagsController : GenericEntityController<Tag>
{
    public TagsController(IRepository<Tag> repository) : base(repository)
    {
    }

    // That's it! You now have:
    // GET    /api/tags          - List all tags with pagination
    // GET    /api/tags/{id}     - Get tag by ID
    // POST   /api/tags          - Create tag
    // PUT    /api/tags/{id}     - Update tag
    // PATCH  /api/tags/{id}     - Partial update
    // DELETE /api/tags/{id}     - Delete tag
    // POST   /api/tags/bulk     - Bulk create
    // DELETE /api/tags/bulk     - Bulk delete
    // GET    /api/tags/count    - Count tags
    // GET    /api/tags/exists/{id} - Check if exists
}
