namespace NextBus.Domain.Exceptions;

public sealed class ConflictException : DomainException
{
    public string Campo { get; }

    public ConflictException(string message, string campo = "") : base(message)
    {
        Campo = campo;
    }
}
