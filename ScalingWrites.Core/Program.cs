using Microsoft.EntityFrameworkCore;
using ScalingWrites.Core.Data;
using ScalingWrites.Core.Data.Configurations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var shards = config
        .GetSection("ConnectionStrings")
        .GetChildren()
        .Where(c => c.Key.StartsWith("Shard", StringComparison.OrdinalIgnoreCase))
        .Select((c, index) => new ShardDescriptor(
            index,
            c.Key,
            c.Value ?? throw new InvalidOperationException($"Missing connection string for {c.Key}")
        ))
        .ToList();

    if (shards.Count == 0)
        throw new InvalidOperationException("No shard connection strings were found.");

    return shards.AsReadOnly();
});

builder.Services.AddSingleton<IShardResolutionStrategy, IntHashShardStrategy>();
builder.Services.AddSingleton<IShardResolutionStrategy, GuidHashShardStrategy>();

builder.Services.AddSingleton<IShardResolver, ShardResolver>();

builder.Services.AddScoped<IDbContextFactory<ShardedDbContext>, ShardedDbContextFactory>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
