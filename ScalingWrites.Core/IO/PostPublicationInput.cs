namespace ScalingWrites.Core.IO;

public record PostPublicationInput(string Title, Guid? UserId = null);