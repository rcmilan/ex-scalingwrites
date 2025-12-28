using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using ScalingWrites.Core.Data;
using ScalingWrites.Core.IO;
using ScalingWrites.Core.Models;

namespace ScalingWrites.Core.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{

    [HttpPost]
    public async Task<ActionResult<PostUserOutput>> Post([FromServices] IShardedDbContextFactory contextFactory, [FromBody] PostUserInput input)
    {
        var user = new User
        {
            Name = input.Name
        };

        await using var db = contextFactory.CreateDbContext(user.Id);
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
    public async Task<ActionResult<GetUserOutput>> Get([FromServices] IShardedDbContextFactory contextFactory, [FromRoute] Guid id)
    {
        await using var db = contextFactory.CreateDbContext(id);

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        // MySQl still does not support .NET 10 :c
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Users WHERE Id = @id";
        cmd.Parameters.Add(new MySqlParameter("@id", id));

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return NotFound();

        return Ok(new GetUserOutput(reader.GetGuid(0), reader.GetString(1)));
    }
}
