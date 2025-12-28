$shards = "Shard0","Shard1","Shard2","Shard3"

foreach ($s in $shards) {
    Write-Host "Migrating $s..." -ForegroundColor Cyan
    Update-Database -Args "--shard=$s" -Verbose
}
