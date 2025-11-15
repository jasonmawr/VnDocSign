using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Api.Controllers;

[Authorize(Policy = "RequireAdmin")]
[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly AppDbContext _db;
    public RolesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> GetAll(CancellationToken ct)
        => Ok(await _db.Roles.AsNoTracking().Select(r => r.Name).ToListAsync(ct));
}
