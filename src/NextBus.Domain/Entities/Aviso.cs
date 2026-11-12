namespace NextBus.Domain.Entities;

public sealed class Aviso
{
    public long Id { get; private set; }
    public long RotinaId { get; private set; }
    public Rotina? Rotina { get; private set; }
    public DateOnly DataReferencia { get; private set; }
    public string Letreiro { get; private set; } = "";
    public string Ponto { get; private set; } = "";
    public string? Veiculo { get; private set; }
    public int? Minutos { get; private set; }
    public int? IdadeGpsSegundos { get; private set; }
    public string Texto { get; private set; } = "";
    public string Status { get; private set; } = "";

    private Aviso()
    {
    }

    public static Aviso Novo(long rotinaId, DateOnly data) =>
        new()
        {
            RotinaId = rotinaId,
            DataReferencia = data
        };

    public void Aplicar(string letreiro, string ponto, string? veiculo, int? minutos, int? idadeGpsSegundos, string texto, string status)
    {
        Letreiro = letreiro;
        Ponto = ponto;
        Veiculo = veiculo;
        Minutos = minutos;
        IdadeGpsSegundos = idadeGpsSegundos;
        Texto = texto;
        Status = status;
    }
}
