namespace NextBus.Domain.Entities;

public sealed class Rotina
{
    public long Id { get; private set; }
    public long UsuarioId { get; private set; }
    public Usuario? Usuario { get; private set; }
    public int CodigoLinha { get; private set; }
    public string Letreiro { get; private set; } = "";
    public int Sentido { get; private set; }
    public int CodigoParada { get; private set; }
    public string NomeParada { get; private set; } = "";
    public string DiasSemana { get; private set; } = "";
    public string Horario { get; private set; } = "";
    public bool Ativa { get; private set; } = true;
    public List<Aviso> Avisos { get; private set; } = [];

    private Rotina()
    {
    }

    public static Rotina Criar(
        long usuarioId,
        int codigoLinha,
        string letreiro,
        int sentido,
        int codigoParada,
        string nomeParada,
        string diasSemana,
        string horario,
        bool ativa) =>
        new()
        {
            UsuarioId = usuarioId,
            CodigoLinha = codigoLinha,
            Letreiro = letreiro,
            Sentido = sentido,
            CodigoParada = codigoParada,
            NomeParada = nomeParada,
            DiasSemana = diasSemana,
            Horario = horario,
            Ativa = ativa
        };

    public void Atualizar(
        int codigoLinha,
        string letreiro,
        int sentido,
        int codigoParada,
        string nomeParada,
        string diasSemana,
        string horario,
        bool ativa)
    {
        CodigoLinha = codigoLinha;
        Letreiro = letreiro;
        Sentido = sentido;
        CodigoParada = codigoParada;
        NomeParada = nomeParada;
        DiasSemana = diasSemana;
        Horario = horario;
        Ativa = ativa;
    }
}
