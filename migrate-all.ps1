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
        Update-Database -Args "--shard=$shard" -Verbose
        Write-Host "Successfully migrated $shard" -ForegroundColor Green
    }
    catch {
        Write-Host "Failed to migrate $shard : $_" -ForegroundColor Red
        exit 1
    }
}

Write-Host "All shard migrations completed successfully!" -ForegroundColor Green
