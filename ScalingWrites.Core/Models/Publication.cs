namespace ScalingWrites.Core.Models;

public class Publication
{
    public Guid Id { get; } = Guid.NewGuid();
    public required DateTime CreatedAt { get; set; } = DateTime.Now;
    public required string Title { get; set; }
}
