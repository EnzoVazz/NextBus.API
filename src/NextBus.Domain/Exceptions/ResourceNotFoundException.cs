namespace NextBus.Domain.Exceptions;

public sealed class ResourceNotFoundException : DomainException
{
    public ResourceNotFoundException(string message) : base(message)
    {
    }
}
