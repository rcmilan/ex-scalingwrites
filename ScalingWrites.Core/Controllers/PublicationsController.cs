using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScalingWrites.Core.Data;
using ScalingWrites.Core.IO;
using ScalingWrites.Core.Models;
using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PublicationsController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PostPublicationOutput>> Post([FromServices] IShardedDbContextFactory contextFactory, [FromBody] PostPublicationInput input)
    {
        var publication = new Publication
        {
            Title = input.Title,
            CreatedAt = DateTime.UtcNow
        };

        if (input.UserId.HasValue)
        {
            await using var db = contextFactory.CreateDbContext(input.UserId.Value);
            await using var tx = await db.Database.BeginTransactionAsync();

            try
            {
                var user = await db.Users.FindAsync(input.UserId.Value);
                if (user == null)
                    return NotFound("User not found");

                user.Publications.Add(publication);
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new PostPublicationOutput(publication.Id));
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        else
        {
            await using var db = contextFactory.CreateDbContext(publication.Id);
            await using var tx = await db.Database.BeginTransactionAsync();

            try
            {
                db.Publications.Add(publication);
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new PostPublicationOutput(publication.Id));
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetPublicationOutput>> Get(
        [FromServices] IReadOnlyList<ShardDescriptor> shards,
        [FromServices] IShardedDbContextFactory contextFactory,
        [FromRoute] Guid id)
    {
        foreach (var shard in shards)
        {
            await using var db = contextFactory.GetShardedDbContext(shard);

            var publication = await db.Publications
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (publication != null)
                return Ok(new GetPublicationOutput(publication.Id, publication.Title, publication.CreatedAt));
        }

        return NotFound();
    }
}
