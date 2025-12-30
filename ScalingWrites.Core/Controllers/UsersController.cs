using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScalingWrites.Core.Data;
using ScalingWrites.Core.IO;
using ScalingWrites.Core.Models;

namespace ScalingWrites.Core.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{

    [HttpPost]
    public async Task<ActionResult<PostUserOutput>> Post([FromServices] IShardDbContextFactory routingService, [FromBody] PostUserInput input)
    {
        var user = new User
        {
            Name = input.Name
        };

        await using var db = await routingService.CreateScopedDbContextAsync(user.Id);
        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            db.Users.Add(user);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new PostUserOutput(user.Id));
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetUserOutput>> Get([FromServices] IShardDbContextFactory routingService, [FromRoute] Guid id)
    {
        await using var db = await routingService.CreateScopedDbContextAsync(id);

        var user = await db.Users.FindAsync(id);

        if (user == null)
            return NotFound();

        return Ok(new GetUserOutput(user.Id, user.Name));
    }
}
