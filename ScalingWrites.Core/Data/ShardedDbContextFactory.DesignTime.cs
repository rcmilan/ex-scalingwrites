using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using ScalingWrites.Core.Data.Configurations;

namespace ScalingWrites.Core.Data;

/// <summary>
/// Design-time factory for creating ShardedDbContext instances for EF Core migrations.
/// This factory enables migrations to work with different shard databases by reading from appsettings.json.
/// For migrations, it uses the first available shard connection string.
/// </summary>
public class ShardedDbContextFactory : IDesignTimeDbContextFactory<ShardedDbContext>
{
    public ShardedDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ShardedDbContext>();

        // Get connection string - for migrations, we'll use the first shard found
        var connectionString = GetConnectionStringForMigration();

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "No shard connection strings found in configuration. Expected keys starting with 'Shard' in ConnectionStrings section.");
        }

        optionsBuilder.UseMySQL(connectionString);
        
        return new ShardedDbContext(optionsBuilder.Options);
    }

    private static string GetConnectionStringForMigration()
    {
        // For migrations, we'll use the first available shard connection string
        // This allows migrations to run without external parameters
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        var configuration = configBuilder.Build();
        
        // Get all shard connection strings
        var shardConfigs = configuration.GetSection("ConnectionStrings").GetChildren()
            .Where(c => c.Key.StartsWith("Shard", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Key)
            .ToList();
            
        if (shardConfigs.Count == 0)
        {
            throw new InvalidOperationException("No shard connection strings found. Expected keys starting with 'Shard' in ConnectionStrings section.");
        }

        // Return the first shard's connection string for migrations
        var firstShard = shardConfigs.First();
        return firstShard.Value ?? throw new InvalidOperationException($"Missing connection string for {firstShard.Key}");
    }

    private static string? GetConnectionStringFromArgs(string[] args)
    {
        // Check for --shard parameter first (our preferred method)
        var shardName = GetShardNameFromArgs(args);
        if (!string.IsNullOrEmpty(shardName))
        {
            return GetConnectionStringFromConfig(shardName);
        }

        // Check for --connection parameter in command-line args
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--connection", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("-c", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        // Check environment variable
        var envConnectionString = Environment.GetEnvironmentVariable("SHARD_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(envConnectionString))
        {
            return envConnectionString;
        }

        // Check environment variable for shard name
        var envShardName = Environment.GetEnvironmentVariable("SHARD_NAME");
        if (!string.IsNullOrEmpty(envShardName))
        {
            return GetConnectionStringFromConfig(envShardName);
        }

        return null;
    }

    private static string? GetShardNameFromArgs(string[] args)
    {
        // Check for --shard parameter
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--shard", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        // Check for shard parameter in different formats
        foreach (var arg in args)
        {
            if (arg.StartsWith("--shard=", StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring("--shard=".Length);
            }
        }

        return null;
    }

    private static string GetConnectionStringFromConfig(string shardName)
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        var configuration = configBuilder.Build();

        var connectionStringKey = $"ConnectionStrings:{shardName}";
        var connectionString = configuration.GetConnectionString(shardName);

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string not found for shard '{shardName}'. Expected configuration key: {connectionStringKey}");
        }

        return connectionString;
    }
}