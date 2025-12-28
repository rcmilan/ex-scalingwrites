# Database Sharding Tutorial: Scaling Writes with .NET and MySQL

This tutorial guides you through implementing database sharding to scale write operations in a .NET application. You'll learn how to distribute data across multiple MySQL database shards using consistent hashing, enabling horizontal scaling for write-heavy workloads.

## 🎯 What You'll Learn

By the end of this tutorial, you'll understand:
- **Database Sharding Concepts**: How to distribute data across multiple databases
- **Consistent Hashing**: A technique for even data distribution and minimal rebalancing
- **EF Core Integration**: Implementing sharding with Entity Framework Core
- **Real-World Implementation**: Building a REST API with sharded data

**Key Benefits of Sharding:**
- **Horizontal Write Scaling**: Distribute write operations across multiple database instances
- **Improved Performance**: Reduce contention and increase throughput
- **Fault Isolation**: Database issues in one shard don't affect others
- **Scalability**: Add more shards as your data grows

## 📋 Prerequisites

Before starting, ensure you have:
- .NET 10.0 SDK (or later)
- Docker and Docker Compose
- Basic knowledge of C#, ASP.NET Core, and Entity Framework Core
- Understanding of relational databases and SQL

## 🏗️ Step 1: Understanding the Architecture

This project implements sharding using several key components. Let's examine each one:

### 1.1 Sharding Infrastructure

The sharding system consists of interfaces and implementations that determine which database shard handles each operation.

#### Core Interfaces

**[`IShardResolver`](ScalingWrites.Core/Data/Configurations/IShardResolver.cs)**: Defines the contract for shard resolution.
```csharp
namespace ScalingWrites.Core.Data.Configurations;

public interface IShardResolver
{
    ShardDescriptor Resolve(object shardKey);
}
```

**[`IShardResolutionStrategy`](ScalingWrites.Core/Data/Configurations/IShardResolutionStrategy.cs)**: Defines strategies for different key types.
```csharp
namespace ScalingWrites.Core.Data.Configurations;

public interface IShardResolutionStrategy
{
    bool CanResolve(object shardKey);
    ShardDescriptor Resolve(object shardKey, IReadOnlyList<ShardDescriptor> shards);
}
```

#### Implementations

**[`ShardResolver`](ScalingWrites.Core/Data/Configurations/ShardResolver.cs)**: The main resolver that delegates to appropriate strategies.
```csharp
namespace ScalingWrites.Core.Data.Configurations;

public sealed class ShardResolver(IReadOnlyList<ShardDescriptor> shards, IEnumerable<IShardResolutionStrategy> strategies) : IShardResolver
{
    public ShardDescriptor Resolve(object shardKey)
    {
        var strategy = strategies.FirstOrDefault(s => s.CanResolve(shardKey));
        return strategy is null
            ? throw new InvalidOperationException($"No shard strategy registered for key type {shardKey?.GetType().Name}")
            : strategy.Resolve(shardKey, shards);
    }
}
```

**[`IntHashShardStrategy`](ScalingWrites.Core/Data/Configurations/IntHashShardStrategy.cs)** and **[`GuidHashShardStrategy`](ScalingWrites.Core/Data/Configurations/GuidHashShardStrategy.cs)**: Strategies for integer and GUID keys using consistent hashing.

**[`ConsistentHashRing`](ScalingWrites.Core/Data/Configurations/ConsistentHashRing.cs)**: Implements consistent hashing with virtual nodes.
```csharp
using System.Security.Cryptography;
using System.Text;

namespace ScalingWrites.Core.Data.Configurations;

public sealed class ConsistentHashRing
{
    private readonly SortedDictionary<int, ShardDescriptor> _ring = [];

    public ConsistentHashRing(IEnumerable<ShardDescriptor> shards, int replicas = 100)
    {
        if (_ring.Count > 0) return;

        foreach (var shard in shards)
        {
            for (int i = 0; i < replicas; i++)
            {
                var key = Hash($"{shard.Name}:{i}");
                _ring[key] = shard;
            }
        }
    }

    public ShardDescriptor Resolve(string key)
    {
        var node = _ring.Keys.FirstOrDefault(k => k >= Hash(key));
        if (node == 0) node = _ring.Keys.First();
        return _ring[node];
    }

    private static int Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(bytes, 0);
    }
}
```

### 1.2 Database Context Layer

**[`ShardedDbContext`](ScalingWrites.Core/Data/ShardedDbContext.cs)**: EF Core context with your entities.
```csharp
using Microsoft.EntityFrameworkCore;
using ScalingWrites.Core.Models;

namespace ScalingWrites.Core.Data;

public class ShardedDbContext : DbContext
{
    public ShardedDbContext(DbContextOptions<ShardedDbContext> options) : base(options)
    {

    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Publication> Publications => Set<Publication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Name).IsRequired();

            e.HasMany(u => u.Publications)
             .WithMany()
             .UsingEntity("UserPublications");
        });

        modelBuilder.Entity<Publication>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).IsRequired();
            e.Property(p => p.CreatedAt).IsRequired();
        });
    }
}
```

**[`ShardedDbContextFactory`](ScalingWrites.Core/Data/ShardedDbContextFactory.cs)**: Creates context instances for specific shards.
```csharp
using Microsoft.EntityFrameworkCore;
using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Data;

public sealed class ShardedDbContextFactory(IShardResolver resolver) : IShardedDbContextFactory, IDbContextFactory<ShardedDbContext>
{
    public ShardedDbContext CreateDbContext(object shardKey)
    {
        var shard = resolver.Resolve(shardKey);
        return GetShardedDbContext(shard);
    }

    public ShardedDbContext GetShardedDbContext(ShardDescriptor shard)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ShardedDbContext>();
        optionsBuilder.UseMySQL(shard.ConnectionString);

        return new ShardedDbContext(optionsBuilder.Options);
    }

    // fallback if EF tooling calls factory
    public ShardedDbContext CreateDbContext() => CreateDbContext(shardKey: 0);
}
```

### 1.3 Models

**[`User`](ScalingWrites.Core/Models/User.cs)**: Entity with auto-generated GUID ID.
```csharp
namespace ScalingWrites.Core.Models;

public class User
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; set; }
    public List<Publication> Publications { get; } = [];
}
```

**[`Publication`](ScalingWrites.Core/Models/Publication.cs)**: Entity with GUID ID (note: ID should be settable for EF Core).
```csharp
namespace ScalingWrites.Core.Models;

public class Publication
{
    public Guid Id { get; }
    public required DateTime CreatedAt { get; set; } = DateTime.Now;
    public required string Title { get; set; }
}
```

### 1.4 Configuration

**[`ShardDescriptor`](ScalingWrites.Core/Data/Configurations/ShardDescriptor.cs)**: Represents a shard.
```csharp
namespace ScalingWrites.Core.Data.Configurations;

public record ShardDescriptor(int Id, string Name, string ConnectionString);
```

**[`ShardConfigurationHelper`](ScalingWrites.Core/Helpers/ShardConfigurationHelper.cs)**: Loads shards from configuration.
```csharp
using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Helpers;

public static class ShardConfigurationHelper
{
    public static IReadOnlyList<ShardDescriptor> LoadShards(IConfiguration config)
    {
        var shards = config
            .GetSection("ConnectionStrings")
            .GetChildren()
            .Where(c => c.Key.StartsWith("Shard", StringComparison.OrdinalIgnoreCase))
            .Select((c, index) => new ShardDescriptor(
                index,
                c.Key,
                c.Value ?? throw new InvalidOperationException(
                    $"Missing connection string for {c.Key}")
            ))
            .ToList();

        if (shards.Count == 0)
            throw new InvalidOperationException("No shard connection strings were found.");

        return shards.AsReadOnly();
    }
}
```

## 🚀 Step 2: Setting Up the Environment

### 2.1 Start the Database Infrastructure

The project includes a Docker Compose setup for four MySQL shards across two instances:

```yaml
services:
  mysql_shard1:
    image: mysql:8.0
    container_name: mysql_shard1
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: rootpass1
      # no MYSQL_DATABASE so no initial DB
    ports:
      - "3307:3306"           # map host 3307 → container 3306
    volumes:
      - shard1_data:/var/lib/mysql
    networks:
      - shard-net
    command: --default-authentication-plugin=mysql_native_password

  mysql_shard2:
    image: mysql:8.0
    container_name: mysql_shard2
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: rootpass2
      # no initial DB
    ports:
      - "3308:3306"           # map host 3308 → container 3306
    volumes:
      - shard2_data:/var/lib/mysql
    networks:
      - shard-net
    command: --default-authentication-plugin=mysql_native_password

volumes:
  shard1_data:
  shard2_data:

networks:
  shard-net:
```

```bash
# Clone or navigate to the project directory
cd ex-scalingwrites

# Start the databases
docker-compose up -d

# Verify containers are running
docker-compose ps
```

This creates:
- **MySQL Instance 1** (Port 3307): Hosts `shard0` and `shard1` databases
- **MySQL Instance 2** (Port 3308): Hosts `shard2` and `shard3` databases

### 2.2 Configure the Application

The **[`appsettings.json`](ScalingWrites.Core/appsettings.json)** file contains shard connection strings:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Shard0": "server=localhost;port=3307;user=root;password=rootpass1;database=shard0;",
    "Shard1": "server=localhost;port=3307;user=root;password=rootpass1;database=shard1;",
    "Shard2": "server=localhost;port=3308;user=root;password=rootpass2;database=shard2;",
    "Shard3": "server=localhost;port=3308;user=root;password=rootpass2;database=shard3;"
  }
}
```

Services are registered in **[`Program.cs`](ScalingWrites.Core/Program.cs)**:

```csharp
using Microsoft.EntityFrameworkCore;
using ScalingWrites.Core.Data;
using ScalingWrites.Core.Data.Configurations;
using ScalingWrites.Core.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    
    return ShardConfigurationHelper.LoadShards(config);
});

builder.Services.AddSingleton<IShardResolver, ShardResolver>();

builder.Services.AddSingleton<IShardedDbContextFactory, ShardedDbContextFactory>();
builder.Services.AddSingleton<IDbContextFactory<ShardedDbContext>, ShardedDbContextFactory>();

builder.Services.AddSingleton<IShardResolutionStrategy, IntHashShardStrategy>();
builder.Services.AddSingleton<IShardResolutionStrategy, GuidHashShardStrategy>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add Swagger UI services
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Add Swagger UI middleware
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ScalingWrites API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
```

## 🔄 Step 3: Running Database Migrations

### 3.1 Apply Migrations to All Shards

Use the provided PowerShell script:

```bash
# Apply migrations to all shards
./migrate-all.ps1
```

This script:
1. Sets the `SHARD_NAME` environment variable for each shard
2. Runs `dotnet ef database update` with the appropriate connection

### 3.2 Manual Migration (Optional)

For a specific shard:

```bash
# Migrate Shard0
$env:SHARD_NAME="Shard0"
dotnet ef database update
```

The **[`ShardedDesignTimeFactory`](ScalingWrites.Core/Data/ShardedDesignTimeFactory.cs)** handles design-time operations by reading the `--shard` argument or defaulting to `Shard0`.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ScalingWrites.Core.Data.Configurations;
using ScalingWrites.Core.Helpers;

namespace ScalingWrites.Core.Data;

public class ShardedDesignTimeFactory : IDesignTimeDbContextFactory<ShardedDbContext>
{
    public ShardedDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var shards = ShardConfigurationHelper.LoadShards(config);

        // default shard
        var shardName = "Shard0";

        // read arg:  Update-Database -- --shard=Shard2
        var shardArg = args?.FirstOrDefault(a => a.StartsWith("--shard=", StringComparison.OrdinalIgnoreCase));
        if (shardArg is not null)
            shardName = shardArg.Split("=", 2)[1];

        var shard = shards.Single(s => s.Name == shardName);

        var options = new DbContextOptionsBuilder<ShardedDbContext>()
            .UseMySQL(shard.ConnectionString)
            .Options;

        return new ShardedDbContext(options);
    }

    private sealed class StaticShardResolver(IEnumerable<ShardDescriptor> shards) : IShardResolver
    {
        private readonly Dictionary<object, ShardDescriptor> _byId = shards.ToDictionary(s => (object)s.Id);

        public ShardDescriptor Resolve(object shardKey)
        {
            return _byId.TryGetValue(shardKey, out var shard)
                ? shard
                : throw new InvalidOperationException($"Unknown shard key: {shardKey}");
        }
    }
}
```

## 🏃 Step 4: Running the Application

```bash
# Navigate to the project directory
cd ScalingWrites.Core

# Run the application
dotnet run
```

The API will be available at:
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`
- Swagger UI: `https://localhost:5001/swagger`

## 📝 Step 5: Exploring the API

### 5.1 Users Endpoints

The **[`UsersController`](ScalingWrites.Core/Controllers/UsersController.cs)** demonstrates sharding in action:

**Create User** (`POST /api/users`):
```bash
curl -X POST "https://localhost:5001/api/users" \
     -H "Content-Type: application/json" \
     -d '{"name": "John Doe"}'
```

**Get User** (`GET /api/users/{id}`):
```bash
curl "https://localhost:5001/api/users/{user-id}"
```

### 5.2 Publications Endpoints

The **[`PublicationsController`](ScalingWrites.Core/Controllers/PublicationsController.cs)** shows sharding with relationships and cross-shard queries:

**Create Publication** (`POST /api/publications`):
```bash
curl -X POST "https://localhost:5001/api/publications" \
     -H "Content-Type: application/json" \
     -d '{"title": "My First Post", "userId": "user-guid"}'
```

**Get Publication** (`GET /api/publications/{id}`):
```bash
curl "https://localhost:5001/api/publications/{publication-id}"
```

### 5.3 Implementation Details

The controllers use dependency injection:

UsersController:
```csharp
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

        var user = await db.Users.FindAsync(id);

        if (user == null)
            return NotFound();

        return Ok(new GetUserOutput(user.Id, user.Name));
    }
}
```

PublicationsController:
```csharp
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
```

## 🧪 Step 6: Testing Shard Distribution

### 6.1 Verify Data Distribution

1. Create multiple users with different GUIDs
2. Check which database stores each user
3. Observe how the consistent hashing distributes data evenly

### 6.2 Understanding the Flow

When you create a user:
1. A new GUID is generated for the User ID
2. `IShardedDbContextFactory.CreateDbContext(user.Id)` resolves the shard
3. `ShardResolver.Resolve(user.Id)` selects the appropriate strategy (`GuidHashShardStrategy`)
4. `ConsistentHashRing.Resolve(user.Id.ToString())` determines the target shard
5. A `ShardedDbContext` is created with the shard's connection string
6. The user is saved to the correct shard

For publications, if associated with a user, it uses the user's shard key; otherwise, its own ID.

For reading publications, since the shard is unknown, it queries all shards sequentially.

## 🔧 Step 7: Implementing Your Own Sharded Entities

### 7.1 Add a New Entity

1. Create the model class with a sharding key
2. Add it to `ShardedDbContext`
3. Create or reuse a sharding strategy
4. Add API endpoints

## 📈 Step 8: Scaling and Maintenance

### 8.1 Adding New Shards

1. Update `docker-compose.yaml` with new MySQL instances/databases
2. Add connection strings to `appsettings.json`
3. Run migrations on the new shard
4. Restart the application (consistent hashing handles redistribution automatically)

### 8.2 Monitoring

- Monitor write performance per shard
- Track data distribution balance
- Set up alerts for shard failures

### 8.3 Backup Strategy

- Implement per-shard backup procedures
- Ensure backup coordination across shards
- Plan for disaster recovery

## 🚀 Advanced Topics

### Cross-Shard Queries

The current implementation requires knowing the sharding key. For cross-shard queries, you would need:
- A query router to distribute queries across shards
- Result aggregation logic
- Handling of JOINs across shards

### Read Replicas

To add read scaling:
- Configure read replicas for each shard
- Route read operations to replicas
- Keep writes going to primary shards

### Distributed Transactions

For operations spanning multiple shards:
- Implement Saga pattern or 2PC
- Handle eventual consistency where appropriate
- Monitor distributed transaction states

## 🐛 Troubleshooting

### Common Issues

1. **Migration Failures**: Ensure all shards are accessible and credentials are correct
2. **Connection Errors**: Verify Docker containers are running and ports are available
3. **Data Not Found**: Confirm the sharding key resolves to the correct shard
4. **Performance Issues**: Monitor shard distribution and add more shards if needed

### Debugging Tips

- Use logging to track which shard operations hit
- Test with known GUIDs to verify distribution
- Monitor database connections and query performance

## 📚 Additional Resources

- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [MySQL Connector/NET Documentation](https://dev.mysql.com/doc/connector-net/en/)
- [Consistent Hashing Explained](https://en.wikipedia.org/wiki/Consistent_hashing)
- [Database Sharding Patterns](https://microservices.io/patterns/data/database-sharding.html)

## 🎓 Next Steps

Now that you understand the basics, consider:
- Implementing cross-shard queries
- Adding caching layers
- Setting up monitoring and metrics
- Exploring automatic rebalancing strategies

This tutorial provides a solid foundation for building scalable, sharded database systems with .NET and MySQL.