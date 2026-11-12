namespace NextBus.Domain.Entities;

public sealed class Usuario
{
    public long Id { get; private set; }
    public string Nome { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string Papel { get; private set; } = "";
    public List<Rotina> Rotinas { get; private set; } = [];

    private Usuario()
    {
    }

    public static Usuario Criar(string nome, string email, string papel) =>
        new()
        {
            Nome = nome.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Papel = papel
        };
}
