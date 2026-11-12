namespace NextBus.Domain.Entities;

public sealed class Idempotencia
{
    public long Id { get; private set; }
    public long UsuarioId { get; private set; }
    public string Chave { get; private set; } = "";
    public string HashCorpo { get; private set; } = "";
    public long RotinaId { get; private set; }

    private Idempotencia()
    {
    }

    public static Idempotencia Criar(long usuarioId, string chave, string hashCorpo, long rotinaId) =>
        new()
        {
            UsuarioId = usuarioId,
            Chave = chave,
            HashCorpo = hashCorpo,
            RotinaId = rotinaId
        };
}
