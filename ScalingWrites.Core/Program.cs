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

// Memory cache is registered by default in .NET

builder.Services.AddSingleton<IShardMetadataStore, ShardMetadataStore>();
builder.Services.AddSingleton<IShardResolver, ShardResolver>();
builder.Services.AddSingleton<IShardDbContextFactory, ShardDbContextFactory>();
builder.Services.AddSingleton<IDbContextFactory<ShardedDbContext>>(sp => 
    (IDbContextFactory<ShardedDbContext>)sp.GetRequiredService<IShardDbContextFactory>());
builder.Services.AddSingleton<ICrossShardQueryCoordinator, CrossShardQueryCoordinator>();

builder.Services.AddSingleton<ITransactionCoordinator, TransactionCoordinator>();

builder.Services.AddSingleton<IShardResolutionStrategy, IntHashShardStrategy>();
builder.Services.AddSingleton<IShardResolutionStrategy, GuidHashShardStrategy>();
builder.Services.AddSingleton<IShardResolutionStrategy, RangeShardStrategy>();

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
