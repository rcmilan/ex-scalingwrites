namespace ScalingWrites.Core.Models;

public class User
{
    public int Id { get; }
    public required string Name { get; set; }
    public List<Publication> Publications { get; } = [];
}
