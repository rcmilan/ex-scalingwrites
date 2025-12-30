# Load the configuration to get shard names dynamically
$configPath = "ScalingWrites.Core\appsettings.json"

if (-not (Test-Path $configPath)) {
    Write-Host "Error: Configuration file not found at $configPath" -ForegroundColor Red
    exit 1
}

# Read and parse the JSON configuration
$configContent = Get-Content $configPath -Raw
$config = $configContent | ConvertFrom-Json

# Extract shard names from connection strings (keys that start with "Shard")
$shards = $config.ConnectionStrings.PSObject.Properties.Name | Where-Object { $_ -like "Shard*" } | Sort-Object

if ($shards.Count -eq 0) {
    Write-Host "Error: No shard connection strings found in configuration" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($shards.Count) shards: $($shards -join ', ')" -ForegroundColor Green
Write-Host "Starting migration process..." -ForegroundColor Yellow

# Apply migrations to each shard
foreach ($shard in $shards) {
    Write-Host "Migrating $shard..." -ForegroundColor Cyan
    try {
        # Get the connection string for this shard
        $connectionString = $config.ConnectionStrings.$shard
        if ([string]::IsNullOrEmpty($connectionString)) {
            throw "Connection string not found for shard $shard"
        }
        
        # Run EF Core migration using dotnet CLI with connection string
        $result = dotnet ef database update --connection "$connectionString" --context ShardedDbContext --project ScalingWrites.Core
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully migrated $shard" -ForegroundColor Green
        } else {
            throw "EF Core command failed with exit code $LASTEXITCODE"
        }
    }
    catch {
        Write-Host "Failed to migrate $shard : $_" -ForegroundColor Red
        exit 1
    }
}

Write-Host "All shard migrations completed successfully!" -ForegroundColor Green
