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
             .UsingEntity(j =>
             {
                 j.ToTable("UserPublications");
                 j.HasIndex("PublicationId").IsUnique();
             });
        });

        modelBuilder.Entity<Publication>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).IsRequired();
            e.Property(p => p.CreatedAt).IsRequired();
        });
    }
}
