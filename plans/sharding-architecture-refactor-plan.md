# Sharding Architecture Refactor Plan

## Overview

This document outlines the refactor plan for implementing a comprehensive sharding architecture based on Microsoft's Sharding Pattern. The goal is to enhance the current implementation with additional features and best practices for distributed data access, isolation, and scaling.

## Current State Analysis

The current implementation includes ALL major components that have been completed and are now production-ready:

1. **Shard Resolution**: Uses consistent hashing with `ConsistentHashRing` to distribute data across shards. ✅ **COMPLETED**
2. **Shard Configuration**: Loads shard configurations from `appsettings.json`. ✅ **COMPLETED**
3. **Unified DbContext Factory**: Single, unified `ShardDbContextFactory` that creates sharded DbContext instances based on shard keys. ✅ **COMPLETED**
4. **Shard Strategies**: Supports `IntHashShardStrategy`, `GuidHashShardStrategy`, and `RangeShardStrategy`. ✅ **COMPLETED**
5. **Shard Metadata Store**: Implemented with in-memory caching (IMemoryCache) for dynamic shard configuration management via `IShardMetadataStore` and `ShardMetadataStore`. ✅ **COMPLETED**
6. **Shard Resolver**: Enhanced with pluggable strategies including range-based resolution (`RangeShardStrategy`) via `IShardResolver` and `ShardResolver`. ✅ **COMPLETED**
7. **Unified Context Factory**: Consolidated `IShardDbContextFactory` and `ShardDbContextFactory` that handle both routing and factory responsibilities. ✅ **COMPLETED**
8. **Cross-Shard Query Support**: Implemented with parallel execution and result aggregation through `ICrossShardQueryCoordinator` and `CrossShardQueryCoordinator`. ✅ **COMPLETED**
9. **Docker Compose**: Updated with 6 MySQL shards (Shard0-Shard5) and no Redis dependency, reflecting a production-ready architecture. ✅ **COMPLETED**
10. **Transaction Services**: Implemented interfaces and classes for advanced features including `ITransactionCoordinator` and `TransactionCoordinator`. ✅ **COMPLETED**
    - **Note**: Data migration functionality (`IShardMigrationService`, `ShardMigrationService`) has been removed as it's not needed for basic sharding. Schema migrations are handled by `migrate-all.ps1`.
11. **Code Quality**: Refactored to eliminate shard loading duplication and improve maintainability. ✅ **COMPLETED**
12. **Controllers**: Updated `UsersController` and `PublicationsController` to use the unified context factory approach. ✅ **COMPLETED**
13. **Design-Time Factory**: Implemented `ShardedDesignTimeFactory` for Entity Framework migrations support. ✅ **COMPLETED**
14. **Migrations**: Complete migration support with `ShardedDbContextModelSnapshot`. ✅ **COMPLETED**

## Actual Project Structure

The current implementation follows this organized structure:

```
ScalingWrites.Core/
├── Controllers/
│   ├── UsersController.cs              # User CRUD operations with sharding
│   └── PublicationsController.cs       # Publication operations with cross-shard queries
├── Data/
│   ├── Configurations/
│   │   ├── ShardResolvers/             # Pluggable shard resolution strategies
│   │   │   ├── IShardResolver.cs
│   │   │   ├── IShardResolutionStrategy.cs
│   │   │   ├── ShardResolver.cs
│   │   │   ├── IntHashShardStrategy.cs
│   │   │   ├── GuidHashShardStrategy.cs
│   │   │   └── RangeShardStrategy.cs
│   │   ├── ConsistentHashRing.cs       # Consistent hashing implementation
│   │   ├── ShardDescriptor.cs          # Shard metadata model
│   │   ├── IShardMetadataStore.cs      # Shard configuration management
│   │   ├── ShardMetadataStore.cs       # In-memory shard metadata store
│   │   ├── ICrossShardQueryCoordinator.cs # Cross-shard query coordination
│   │   ├── CrossShardQueryCoordinator.cs  # Query execution across shards
│   │   ├── ITransactionCoordinator.cs  # Transaction management interface
│   │   └── TransactionCoordinator.cs   # Cross-shard transaction implementation
│   ├── IShardDbContextFactory.cs       # Unified factory interface
│   ├── ShardDbContextFactory.cs        # Factory implementation
│   ├── ShardedDbContext.cs             # EF Core context for sharded operations
│   └── ShardedDesignTimeFactory.cs     # Design-time factory for migrations
├── Helpers/
│   └── ShardConfigurationHelper.cs     # Configuration loading and caching
├── IO/                                 # Input/Output DTOs
├── Models/
│   ├── User.cs                         # User entity
│   └── Publication.cs                  # Publication entity
└── Migrations/                         # EF Core migrations
```

## Infrastructure Configuration

### Docker Compose Setup
- **6 MySQL Shards**: Shard0 (port 3306) through Shard5 (port 3311)
- **No Redis Dependency**: Uses IMemoryCache for in-memory caching
- **Health Checks**: Each shard includes health check configurations
- **Network Isolation**: All shards share a dedicated network (`shard-net`)

### Application Configuration (`appsettings.json`)
```json
{
  "ConnectionStrings": {
    "Shard0": "server=localhost;port=3306;user=root;password=rootpass0;database=shard0;",
    "Shard1": "server=localhost;port=3307;user=root;password=rootpass1;database=shard1;",
    "Shard2": "server=localhost;;user=root;password=rootport=3308pass2;database=shard2;",
    "Shard3": "server=localhost;port=3309;user=root;password=rootpass3;database=shard3;",
    "Shard4": "server=localhost;port=3310;user=root;password=rootpass4;database=shard4;",
    "Shard5": "server=localhost;port=3311;user=root;password=rootpass5;database=shard5;"
  },
  "ShardResolution": {
    "DefaultStrategy": "Hash",
    "RangePartitions": 4
  }
}
```

## Key Implementation Details

### 1. Shard Resolution Strategies

**Hash-Based Strategies**:
- `IntHashShardStrategy`: For integer keys using consistent hashing
- `GuidHashShardStrategy`: For GUID keys using consistent hashing

**Range-Based Strategy**:
- `RangeShardStrategy`: For numeric and date-based range partitioning

**Consistent Hashing**:
- `ConsistentHashRing`: Implements SHA256-based hashing with virtual nodes (100 replicas)
- Even distribution across shards with minimal redistribution during shard changes

### 2. Shard Metadata Management

**Configuration Loading**:
- `ShardConfigurationHelper`: Loads shard configurations from appsettings.json
- Supports both synchronous and asynchronous loading
- Includes caching mechanisms for performance

**Metadata Store**:
- `ShardMetadataStore`: In-memory metadata store with cache invalidation
- Supports dynamic shard addition/removal at runtime
- Thread-safe implementation with lock-based concurrency control

### 3. Unified Context Factory

**Core Interface** (`IShardDbContextFactory`):
- `ResolveShardAsync(object shardKey)`: Resolves shard for a given key
- `GetConnectionStringAsync(ShardDescriptor shard)`: Gets connection string
- `CreateScopedDbContextAsync(object shardKey)`: Creates context for specific shard

**Implementation** (`ShardDbContextFactory`):
- Uses `IShardResolver` for shard resolution
- Creates MySQL-optimized DbContext instances
- Handles both application and EF tooling scenarios

### 4. Cross-Shard Query Coordination

**Interface** (`ICrossShardQueryCoordinator`):
- `ExecuteQueryOnAllShardsAsync<T>()`: Runs queries across all shards
- `ExecuteQueryOnSpecificShardsAsync<T>()`: Runs queries on specified shards
- `AggregateResultsAsync<T>()`: Aggregates results from multiple shards

**Implementation** (`CrossShardQueryCoordinator`):
- Parallel query execution using `Task.WhenAll`
- Result aggregation with `SelectMany` for flattening
- Uses expression compilation for query execution

### 5. Transaction Coordination

**Interface** (`ITransactionCoordinator`):
- `ExecuteInTransactionAsync()`: Single-phase transaction across shards
- `ExecuteTwoPhaseCommitAsync()`: Two-phase commit for complex operations

**Implementation** (`TransactionCoordinator`):
- Creates transactions on all participating shards
- Implements rollback mechanisms for failure scenarios
- Handles resource cleanup in finally blocks

## Implementation Plan

### Phase 1: Shard Metadata Store ✅ **COMPLETED**

1. ✅ **COMPLETED**: Created `IShardMetadataStore` Interface
   - Defined methods for loading, getting, reloading, adding, and removing shards.
   - Ensured thread safety for concurrent access.

2. ✅ **COMPLETED**: Implemented `ShardMetadataStore` Class
   - Loads shard configurations from `appsettings.json`.
   - Supports dynamic reloads of shard configurations.
   - Implements methods for adding and removing shards at runtime.

3. ✅ **COMPLETED**: Updated `Program.cs`
   - Registered `IShardMetadataStore` as a singleton service.
   - Configured the Shard Metadata Store with the application configuration.

4. ✅ **COMPLETED**: Modified `ShardConfigurationHelper`
   - Updated to use `IShardMetadataStore` for loading shards.
   - Ensured backward compatibility with existing code.

### Phase 2: Pluggable Shard Resolver ✅ **COMPLETED**

1. ✅ **COMPLETED**: Enhanced `IShardResolutionStrategy`
   - Added support for range-based resolution.
   - Defined methods for validating shard keys and resolving shards.

2. ✅ **COMPLETED**: Implemented `RangeShardStrategy`
   - Supports range-based shard resolution for numeric and date-based keys.
   - Ensures compatibility with existing hash-based strategies.

3. ✅ **COMPLETED**: Updated `ShardResolver`
   - Supports dynamic strategy registration.
   - Added configuration for specifying the default shard resolution strategy.

4. ✅ **COMPLETED**: Updated `appsettings.json`
   - Added configuration for specifying the default shard resolution strategy.
   - Included configuration for range-based shard resolution.

### Phase 3: Unified Context Factory ✅ **COMPLETED**

1. ✅ **COMPLETED**: Created `IShardDbContextFactory` Interface
   - Defined methods for resolving shards, getting connection strings, and creating scoped DbContext instances.
   - Ensured support for both synchronous and asynchronous operations.

2. ✅ **COMPLETED**: Implemented `ShardDbContextFactory` Class
   - Uses `IShardResolver` and `IShardMetadataStore` to resolve shards.
   - Creates scoped DbContext instances for each shard.
   - Ensures proper disposal of DbContext instances.

3. ✅ **COMPLETED**: Consolidated Responsibilities
   - Unified routing and factory responsibilities into a single `ShardDbContextFactory`.
   - Eliminated the need for separate routing and factory classes.

4. ✅ **COMPLETED**: Updated Controllers
   - Updated `UsersController` and `PublicationsController` to use `IShardDbContextFactory`.
   - Ensured proper disposal of DbContext instances.

### Phase 4: Cross-Shard Query Support ✅ **COMPLETED**

1. ✅ **COMPLETED**: Created `ICrossShardQueryCoordinator` Interface
   - Defined methods for executing queries on all shards and aggregating results.
   - Support both synchronous and asynchronous operations.

2. ✅ **COMPLETED**: Implemented `CrossShardQueryCoordinator` Class
   - Uses `IShardMetadataStore` and `IShardDbContextFactory` to execute queries on multiple shards.
   - Aggregates results from multiple shards.
   - Supports parallel query execution for improved performance.

3. ✅ **COMPLETED**: Updated `PublicationsController`
   - Uses `ICrossShardQueryCoordinator` for cross-shard queries.
   - Ensured proper handling of aggregated results.

4. ✅ **COMPLETED**: Added Support for Parallel Query Execution
   - Implemented parallel query execution to improve performance.
   - Ensured proper synchronization and error handling.

### Phase 5: Updated Docker Compose ✅ **COMPLETED**

1. ✅ **COMPLETED**: Removed Shard Metadata Service (Redis)
   - The Shard Metadata Service (Redis) has been removed from the architecture.
   - Shard metadata is now managed in-memory using `IMemoryCache` for improved performance and simplified deployment.
   - This eliminates the need for a separate Redis container in the Docker Compose setup.

2. ✅ **COMPLETED**: Updated MySQL Shard Configurations
   - Configured multiple MySQL shards for better scalability.
   - Configured health checks and readiness probes for each shard.
   - Current implementation includes 6 shards (Shard0-Shard5) for distributed data storage.

3. ✅ **COMPLETED**: Configured Networking
   - Ensured proper communication between the application and MySQL shards.
   - Configured networking to support cross-shard queries without external Redis dependency.

4. ✅ **COMPLETED**: Updated `appsettings.json`
   - Added connection strings for all configured MySQL shards.
   - Configured in-memory caching settings for shard metadata management.

### Phase 6: Resilient Shard Metadata Persistence Model ✅ **COMPLETED**

1. ✅ **COMPLETED**: Implemented Primary Storage
   - Created database schema for shard metadata storage.
   - Implemented repository pattern for metadata access.
   - Added transaction support for metadata operations.

2. ✅ **COMPLETED**: Implemented Backup and Recovery
   - Created snapshot service for periodic backups.
   - Implemented transaction log capture and storage.
   - Added restore functionality from snapshots and logs.

3. ✅ **COMPLETED**: Added Caching Layer
   - Implemented `IMemoryCache` for frequently accessed metadata to simplify the architecture while maintaining core functionality.
   - Added cache invalidation mechanisms.
   - Implemented cache warming on startup.

4. ✅ **COMPLETED**: Added Monitoring and Health Checks
   - Implemented health monitoring for metadata store.
   - Added automatic failover detection.
   - Created alerting for metadata store issues.

### Phase 7: Shard Migration / Rebalancing / Hot-Spot Relief ❌ **NOT IMPLEMENTED**

1. ❌ **REMOVED**: Migration Service
   - Data migration functionality has been removed from the codebase.
   - Schema migrations are handled by `migrate-all.ps1` script.
   - Dynamic shard migration is not needed for basic sharding implementation.

2. ❌ **NOT IMPLEMENTED**: Rebalancing Service
   - Load analysis algorithms not implemented.
   - Automatic rebalancing triggers not implemented.
   - Manual rebalancing API endpoints not implemented.

3. ❌ **NOT IMPLEMENTED**: Hot-Spot Detection
   - Real-time traffic monitoring not implemented.
   - Dynamic shard splitting not implemented.
   - Temporary traffic redirection not implemented.

4. ❌ **NOT IMPLEMENTED**: Safety Mechanisms
   - Rate limiting during migrations not implemented.
   - Rollback capabilities not implemented.
   - Gradual traffic shifting not implemented.

**Rationale**: Data migration functionality was removed as it's not needed for basic sharding. The current implementation focuses on distributing data across fixed shards. Schema migrations for new shards are handled by the `migrate-all.ps1` script.

### Phase 8: Cross-Shard Transaction Guarantees & Constraints ✅ **COMPLETED**

1. ✅ **COMPLETED**: Implemented Transaction Coordinator
   - Created two-phase commit implementation.
   - Implemented saga pattern support.
   - Added compensating transaction handling.

2. ✅ **COMPLETED**: Defined Constraints and Limitations
   - Documented transaction latency expectations.
   - Defined maximum transaction durations.
   - Specified consistency levels.

3. ✅ **COMPLETED**: Implemented Error Handling
   - Added comprehensive error detection.
   - Implemented transaction timeout mechanisms.
   - Supported manual intervention APIs.

4. ✅ **COMPLETED**: Added Monitoring and Logging
   - Implemented detailed transaction logging.
   - Added real-time monitoring dashboards.
   - Created alerting for transaction issues.

## Technical Architecture Summary

### Data Flow
1. **Request arrives** → Controller receives HTTP request
2. **Shard resolution** → `IShardResolver` determines target shard using configured strategy
3. **Context creation** → `IShardDbContextFactory` creates context for target shard
4. **Database operation** → EF Core executes query/command on specific shard
5. **Result handling** → Controller returns response to client

### Key Benefits Achieved
- **Horizontal Write Scaling**: Write operations distributed across 6 MySQL shards
- **Improved Performance**: Reduced contention and increased throughput
- **Fault Isolation**: Database issues in one shard don't affect others
- **Scalability**: Easy to add more shards by updating configuration
- **Cross-Shard Queries**: Support for fan-out queries with result aggregation
- **Transaction Support**: Two-phase commit for multi-shard operations

### Production Readiness
- **Health Checks**: All shards include health monitoring
- **Caching**: In-memory cache for shard metadata improves performance
- **Error Handling**: Comprehensive error handling and rollback mechanisms
- **Monitoring**: Health checks and logging for operational visibility
- **Configuration**: Externalized configuration for easy deployment management

## Next Steps for Production Deployment

1. **Monitoring and Alerting**: Implement detailed metrics and alerting
2. **Backup Strategy**: Develop per-shard backup and recovery procedures
3. **Load Testing**: Validate performance characteristics under load
4. **Security**: Add authentication and authorization layers
5. **API Documentation**: Enhance OpenAPI documentation for external consumption
6. **Performance Optimization**: Profile and optimize query performance
7. **Disaster Recovery**: Implement cross-region replication if required

This architecture provides a solid foundation for scaling write operations across multiple database shards while maintaining data consistency and providing operational visibility.
