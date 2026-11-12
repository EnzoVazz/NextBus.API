namespace NextBus.Domain.Exceptions;

public sealed record ErroCampo(string Campo, string Mensagem);

public sealed class ValidationException : DomainException
{
    public IReadOnlyList<ErroCampo> Erros { get; }

    public ValidationException(IReadOnlyList<ErroCampo> erros) : base("Erro de validação")
    {
        Erros = erros;
    }

    public ValidationException(string campo, string mensagem) : this([new ErroCampo(campo, mensagem)])
    {
    }
}
