# Database Sharding Example - Scaling Writes

This project demonstrates a practical implementation of database sharding to scale write operations in a .NET application. The architecture uses consistent hashing to distribute data across multiple MySQL database shards, enabling horizontal scaling for write-heavy workloads.

## 🎯 What This Project Demonstrates

This example showcases how to implement database sharding to solve the problem of write scalability in high-traffic applications. Instead of having all data in a single database that becomes a bottleneck, this system distributes data across multiple database instances (shards) based on a sharding key.

**Key Benefits:**
- **Horizontal Write Scaling**: Distribute write operations across multiple database instances
- **Improved Performance**: Reduce contention and increase throughput
- **Fault Isolation**: Database issues in one shard don't affect others
- **Scalability**: Add more shards as your data grows

## 🏗️ Architecture Overview

### Core Components

#### 1. Sharding Infrastructure

The sharding infrastructure is the core of this system, responsible for determining which database shard should handle each operation. It consists of several key components that work together to provide a robust and scalable sharding solution.

**Key Components and Their Responsibilities:**

- **`IShardResolver`**: This interface defines the contract for resolving which shard to use for a given sharding key. It provides the primary method `ResolveShard<T>(T key)` that takes a sharding key and returns the appropriate `ShardDescriptor`. This abstraction allows for different resolution implementations while maintaining a consistent API.

- **`ShardResolver`**: The main implementation of `IShardResolver` that acts as a coordinator between different sharding strategies. It maintains a collection of `IShardResolutionStrategy` instances and determines which strategy to use based on the type of the sharding key. The resolver follows a delegation pattern, passing the resolution request to the most appropriate strategy.

- **`IShardResolutionStrategy`**: This interface defines the contract for different sharding algorithms. Each strategy implementation is responsible for mapping sharding keys to specific shards using its own algorithm. The interface includes methods for resolving shards and retrieving all available shards for operations that need to span multiple shards.

- **`IntHashShardStrategy`**: A concrete implementation of `IShardResolutionStrategy` designed for integer-based sharding keys (like User IDs). It uses consistent hashing to distribute integer keys evenly across available shards. This strategy is particularly useful for auto-incrementing primary keys and sequential data.

- **`GuidHashShardStrategy`**: Another concrete implementation of `IShardResolutionStrategy` optimized for GUID-based sharding keys (like Publication IDs). It handles the conversion of GUIDs to hash values and applies consistent hashing to determine shard placement. This strategy is ideal for distributed systems where GUIDs are generated across multiple services.

- **`ConsistentHashRing`**: The heart of the consistent hashing implementation. This component manages a virtual ring structure where each physical shard is represented by multiple virtual nodes (100 by default). The hash ring provides even distribution of keys across shards and minimizes data movement when shards are added or removed. It uses SHA256 hashing for consistent and reliable hash generation.

**Component Interactions:**

The components interact in a well-defined flow:

1. **Request Entry**: When an operation requires database access, the application calls `ShardResolver.ResolveShard<T>(key)`
2. **Strategy Selection**: `ShardResolver` examines the type of the key and selects the appropriate strategy (`IntHashShardStrategy` for integers, `GuidHashShardStrategy` for GUIDs)
3. **Shard Resolution**: The selected strategy uses `ConsistentHashRing` to map the key to a specific shard
4. **Result Return**: The strategy returns the `ShardDescriptor` containing connection information
5. **Context Creation**: The database context factory uses this descriptor to create a connection to the appropriate shard

**Data Flow Through the Sharding System:**

1. **Key Generation**: Application generates or receives a sharding key (User ID, Publication ID, etc.)
2. **Shard Resolution**: The sharding infrastructure determines which shard should handle this key
3. **Connection Establishment**: A database context is created with the shard-specific connection string
4. **Operation Execution**: All CRUD operations for that entity are performed on the designated shard
5. **Result Return**: Data is retrieved from or stored to the appropriate shard

**Implementation Details and Design Patterns:**

- **Strategy Pattern**: Different sharding algorithms are implemented as separate strategy classes that can be easily extended or replaced
- **Factory Pattern**: Database contexts are created through factories that encapsulate the complexity of shard selection
- **Dependency Injection**: All components are registered with the DI container for loose coupling and testability
- **Interface Segregation**: Each interface has a focused responsibility, making the system more maintainable
- **Consistent Hashing**: Uses virtual nodes to ensure even distribution and minimize rebalancing when shards change

**Architectural Decisions and Rationale:**

1. **Consistent Hashing over Simple Modulo**: Chosen for its ability to minimize data movement when adding/removing shards, which is crucial for production systems
2. **Virtual Nodes**: Implemented to improve distribution evenness and reduce the impact of adding new shards
3. **Type-Based Strategy Selection**: Allows the system to handle different key types optimally without requiring explicit strategy specification
4. **Interface-Based Design**: Enables easy testing, mocking, and future extensibility
5. **SHA256 Hashing**: Selected for its cryptographic properties and even distribution characteristics
6. **100 Virtual Nodes per Shard**: This ratio provides good distribution while maintaining reasonable performance for hash ring operations

#### 2. Database Context
- **`ShardedDbContext`**: Entity Framework Core context configured for sharding
- **`ShardedDbContextFactory`**: Factory for creating context instances with the correct shard connection
- **`ShardedDesignTimeFactory`**: Factory for EF Core tooling (migrations, scaffolding)

#### 3. Models
- **`User`**: Entity with integer ID (sharded by ID)
- **`Publication`**: Entity with GUID ID (sharded by ID)
- **Many-to-Many Relationship**: Users can have multiple publications

#### 4. Configuration
- **`ShardDescriptor`**: Represents a single database shard with ID, name, and connection string
- **`ShardConfigurationHelper`**: Loads shard configuration from app settings

## 📊 Sharding Strategy

This implementation uses **consistent hashing** to distribute data evenly across shards:

1. **Hash Function**: SHA256 is used to generate consistent hash values
2. **Virtual Nodes**: Each physical shard is represented by 100 virtual nodes in the hash ring
3. **Even Distribution**: This approach minimizes data movement when adding/removing shards
4. **Multiple Strategies**: Support for both integer and GUID sharding keys

### How It Works
1. When a request comes in with a sharding key (e.g., User ID)
2. The system determines the appropriate sharding strategy based on the key type
3. The strategy uses consistent hashing to map the key to a specific shard
4. A database context is created with the shard's connection string
5. All operations for that entity are performed on the designated shard

## 🚀 Setup and Installation

### Prerequisites
- .NET 10.0 SDK
- Docker and Docker Compose
- MySQL 8.0+ (or use the provided Docker setup)

### 1. Database Setup

The project includes a `docker-compose.yaml` file that sets up two MySQL instances with four shards:

```bash
# Start the database infrastructure
docker-compose up -d

# Verify containers are running
docker-compose ps
```

**Database Configuration:**
- **MySQL Instance 1** (Port 3307): Hosts Shard0 and Shard1
- **MySQL Instance 2** (Port 3308): Hosts Shard2 and Shard3
- Each instance has its own user credentials for security isolation

### 2. Application Configuration

The `appsettings.json` file contains the shard connection strings:

```json
{
  "ConnectionStrings": {
    "Shard0": "server=localhost;port=3307;user=root;password=rootpass1;database=shard0;",
    "Shard1": "server=localhost;port=3307;user=root;password=rootpass1;database=shard1;",
    "Shard2": "server=localhost;port=3308;user=root;password=rootpass2;database=shard2;",
    "Shard3": "server=localhost;port=3308;user=root;password=rootpass2;database=shard3;"
  }
}
```

### 3. Running Migrations

Use the provided PowerShell script to apply migrations to all shards:

```bash
# Run migrations on all shards
./migrate-all.ps1
```

**Manual Migration Process:**
The script performs these steps for each shard:
1. Sets the `SHARD_NAME` environment variable
2. Executes `dotnet ef database update` with the shard-specific connection string
3. Ensures all shards have the same schema

**Individual Shard Migration:**
```bash
# Migrate a specific shard
$env:SHARD_NAME="Shard0"
dotnet ef database update
```

### 4. Running the Application

```bash
# Navigate to the project directory
cd ScalingWrites.Core

# Run the application
dotnet run
```

The API will be available at `https://localhost:5001` and `http://localhost:5000`.

## 🔄 Migration Strategy for Multiple Shards

### Challenge
Traditional EF Core migrations work with a single database connection, but sharding requires applying the same schema changes to multiple databases.

### Solution
This project implements a multi-step migration process:

1. **Environment Variable Approach**: Use `SHARD_NAME` environment variable to specify which shard to migrate
2. **Automated Script**: `migrate-all.ps1` script handles migrating all shards sequentially
3. **Design-Time Factory**: `ShardedDesignTimeFactory` reads the environment variable and connects to the appropriate shard

### Migration Commands

```bash
# Create a new migration
dotnet ef migrations add NewFeature

# Apply to all shards (recommended)
./migrate-all.ps1

# Apply to specific shard
$env:SHARD_NAME="Shard1"
dotnet ef database update
```

## 📝 API Endpoints

The project includes placeholder controllers for the two main entities:

### Users Controller
- **Base URL**: `/api/users`
- **Sharding Key**: User ID (integer)
- **Strategy**: `IntHashShardStrategy`

### Publications Controller  
- **Base URL**: `/api/publications`
- **Sharding Key**: Publication ID (GUID)
- **Strategy**: `GuidHashShardStrategy`

**Note**: The controllers are currently empty placeholders. They would typically implement CRUD operations that use the sharding infrastructure.

## 🔧 Usage Examples

### Creating Database Context for a Specific Shard

```csharp
// Inject the factory
private readonly IDbContextFactory<ShardedDbContext> _contextFactory;

// Create context for a specific user (integer sharding key)
var userContext = _contextFactory.CreateDbContext(userId);

// Create context for a specific publication (GUID sharding key)
var publicationContext = _contextFactory.CreateDbContext(publicationId);
```

### Service Implementation Pattern

```csharp
public class UserService
{
    private readonly IDbContextFactory<ShardedDbContext> _contextFactory;
    
    public async Task<User> GetUserById(int userId)
    {
        using var context = _contextFactory.CreateDbContext(userId);
        return await context.Users.FindAsync(userId);
    }
    
    public async Task AddUser(User user)
    {
        using var context = _contextFactory.CreateDbContext(user.Id);
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }
}
```

## 🧪 Testing the Sharding

### Verify Shard Distribution
You can test that data is properly distributed by:
1. Creating users with different IDs
2. Checking which database each user is stored in
3. Verifying that related publications are in the same shard

### Performance Testing
- Create multiple concurrent write operations
- Monitor database performance across shards
- Verify that writes are distributed evenly

## 📈 Scaling Considerations

### Adding New Shards
1. Update `appsettings.json` with new connection string
2. Run migrations on the new shard
3. The consistent hashing algorithm will automatically redistribute data

### Monitoring
- Monitor write performance across all shards
- Track shard utilization and balance
- Set up alerts for shard-specific issues

### Backup Strategy
- Implement shard-specific backup procedures
- Consider cross-shard backup coordination
- Plan for disaster recovery scenarios

## 🚀 Future Improvements

This sharding implementation provides a solid foundation, but several enhancements could significantly improve its production readiness and capabilities:

### Cross-Shard Query Support
- **Challenge**: Current implementation requires knowing the sharding key to access data, limiting complex queries that span multiple shards
- **Solution**: Implement a query router that can:
  - Distribute queries across relevant shards based on query patterns
  - Aggregate results from multiple shards
  - Handle JOIN operations that span shards
  - Provide query optimization for cross-shard operations

### Caching Layer Integration
- **Multi-Level Caching Strategy**:
  - **Application-Level Cache**: Redis or similar for frequently accessed data
  - **Shard-Level Cache**: Local caching within each shard's context
  - **Query Result Cache**: Cache complex query results with appropriate invalidation
- **Cache Invalidation**: Implement strategies to keep cached data consistent with database changes
- **Cache Warming**: Pre-populate cache with frequently accessed data patterns

### Monitoring and Metrics
- **Shard-Level Metrics**:
  - Query performance per shard
  - Storage utilization and growth trends
  - Connection pool usage and health
  - Error rates and patterns
- **Cross-Shard Analytics**:
  - Data distribution analysis
  - Query pattern analysis
  - Performance bottlenecks identification
- **Alerting System**: Set up alerts for:
  - Shard overload or failure
  - Data distribution imbalances
  - Performance degradation
  - Storage capacity limits

### Read Replicas Implementation
- **Read/Write Separation**: Route read operations to replicas and writes to primary shards
- **Replica Management**: Automated replica creation, synchronization, and failover
- **Load Balancing**: Distribute read traffic across multiple replicas
- **Consistency Models**: Implement appropriate consistency levels for different use cases

### Sharding Key Migration Strategies
- **Dynamic Sharding**: Ability to change sharding keys without data migration
- **Shard Splitting**: Split existing shards when they reach capacity
- **Shard Merging**: Combine underutilized shards to optimize resource usage
- **Zero-Downtime Migrations**: Implement migration strategies that don't require system downtime

### Distributed Transactions Support
- **Two-Phase Commit (2PC)**: Implement distributed transaction protocols for operations spanning multiple shards
- **Saga Pattern**: Use compensating transactions for long-running distributed operations
- **Eventual Consistency**: Implement patterns for scenarios where strong consistency isn't required
- **Transaction Monitoring**: Track and manage distributed transactions across shards

### Automatic Shard Rebalancing
- **Load-Based Rebalancing**: Automatically redistribute data based on usage patterns
- **Storage-Based Rebalancing**: Move data when shards reach storage thresholds
- **Performance-Based Rebalancing**: Optimize shard distribution based on query performance
- **Intelligent Algorithms**: Use machine learning to predict and optimize shard distribution

### Additional Enhancements
- **Security Enhancements**: Implement shard-level security policies and encryption
- **Backup and Recovery**: Automated backup strategies for individual shards and cross-shard consistency
- **Development Tools**: CLI tools for shard management, data migration, and debugging
- **Documentation and Examples**: Comprehensive examples for common sharding patterns and use cases

### Implementation Priority
1. **High Priority**: Monitoring, caching, read replicas
2. **Medium Priority**: Cross-shard queries, automatic rebalancing
3. **Low Priority**: Advanced transaction support, sophisticated migration tools

These improvements would transform this basic sharding implementation into a production-ready, enterprise-grade distributed database system capable of handling complex workloads at scale.

## 🛠️ Development and Maintenance

### Adding New Entities
1. Add the entity to `ShardedDbContext`
2. Create appropriate sharding strategy if needed
3. Update migrations
4. Apply migrations to all shards

### Schema Changes
1. Create migration using `dotnet ef migrations add`
2. Run `./migrate-all.ps1` to apply to all shards
3. Test the changes work correctly across all shards

## 🤝 Contributing

When contributing to this project:
1. Ensure all shards remain in sync
2. Test migrations on all shards
3. Maintain consistent hashing behavior
4. Update documentation for any architectural changes

## 📚 Additional Resources

- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [MySQL Documentation](https://dev.mysql.com/doc/)
- [Consistent Hashing](https://en.wikipedia.org/wiki/Consistent_hashing)
- [Database Sharding Patterns](https://www.mongodb.com/basics/sharding)

## 📄 License

This project is provided as an educational example for understanding database sharding concepts.