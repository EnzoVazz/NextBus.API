using NextBus.Domain.Exceptions;

namespace NextBus.Api;

public sealed record ErroResposta(string Titulo, int Status, IReadOnlyList<ErroCampo> Erros)
{
    public static ErroResposta De(string titulo, int status, string mensagem, string campo = "") =>
        new(titulo, status, [new ErroCampo(campo, mensagem)]);
}
