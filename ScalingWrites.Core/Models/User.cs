namespace ScalingWrites.Core.Models;

public class User
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; set; }
    public List<Publication> Publications { get; } = [];
}
