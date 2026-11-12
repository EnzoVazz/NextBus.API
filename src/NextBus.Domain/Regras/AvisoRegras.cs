namespace NextBus.Domain.Regras;

public sealed record Chegada(string Prefixo, DateTimeOffset Quando, bool Acessivel, int IdadeGpsSegundos);

public sealed record EstadoAviso(string Status, string? Veiculo, int? Marco, int? Minutos = null);

public sealed record ResultadoAviso(
    bool Alterou,
    string? Status,
    string? Veiculo,
    int? Minutos,
    int? IdadeGpsSegundos,
    string? Texto,
    int? Marco)
{
    public static ResultadoAviso Nada() => new(false, null, null, null, null, null, null);
}

public static class AvisoRegras
{
    public const int AntecedenciaMinutos = 20;
    public const int LimiteAposEmbarqueHoras = 2;

    public static int MinutosEntre(DateTimeOffset previsao, DateTimeOffset relogio)
    {
        var minutos = (int)Math.Floor((previsao - relogio).TotalMinutes);
        return minutos < 0 ? 0 : minutos;
    }

    public static bool EstaNaJanela(TimeOnly horario, DateTimeOffset agora, bool veiculoJaPassou) =>
        !veiculoJaPassou && EmbarqueNaJanela(horario, agora, DiasSemana.Todos) is not null;

    public static DateTimeOffset? EmbarqueNaJanela(TimeOnly horario, DateTimeOffset agora, IReadOnlySet<DayOfWeek> dias)
    {
        var dia = DateOnly.FromDateTime(agora.DateTime);
        foreach (var deslocamento in new[] { 0, 1 })
        {
            var embarque = new DateTimeOffset(dia.AddDays(deslocamento).ToDateTime(horario), agora.Offset);
            if (!dias.Contains(embarque.DayOfWeek))
                continue;

            var inicio = embarque.AddMinutes(-AntecedenciaMinutos);
            var fim = embarque.AddHours(LimiteAposEmbarqueHoras);
            if (agora >= inicio && agora <= fim)
                return embarque;
        }

        return null;
    }

    public static Chegada? MaisPerto(DateTimeOffset embarque, DateTimeOffset relogio, IReadOnlyList<Chegada> chegadas)
    {
        return chegadas
            .Where(c => c.Quando >= relogio)
            .OrderBy(c => Math.Abs((c.Quando - embarque).TotalSeconds))
            .ThenBy(c => c.Quando)
            .FirstOrDefault();
    }

    public static int? MarcoDe(int minutos)
    {
        if (minutos > 15)
            return null;
        if (minutos == 0)
            return 0;
        if (minutos <= 2)
            return 2;
        if (minutos <= 5)
            return 5;
        if (minutos <= 10)
            return 10;
        return 15;
    }

    public static string FrasePadrao(string letreiro, string ponto, int? minutos, int? idadeGpsSegundos, string? comunicado)
    {
        var texto = minutos is null
            ? $"{letreiro} sem previsão no seu ponto agora."
            : $"{letreiro} no ponto {ponto}: {minutos} minutos";

        if (minutos is not null && idadeGpsSegundos is not null)
            texto += $". GPS capturado há {idadeGpsSegundos} segundos";

        if (!string.IsNullOrWhiteSpace(comunicado))
            texto += $". Comunicado: {comunicado}";

        return texto;
    }

    public static ResultadoAviso Avaliar(
        string letreiro,
        string ponto,
        DateTimeOffset embarque,
        DateTimeOffset relogio,
        IReadOnlyList<Chegada> chegadas,
        EstadoAviso? estado,
        string? comunicado)
    {
        if (estado?.Veiculo is not null && estado.Status is "ENVIADO" or "ATUALIZADO")
        {
            var mesmo = chegadas.FirstOrDefault(c => c.Prefixo == estado.Veiculo);
            if (mesmo is null || mesmo.Quando < relogio)
                return Encerrar(letreiro, ponto, estado);

            return AplicarMarcos(letreiro, ponto, mesmo, relogio, estado, comunicado);
        }

        var escolhido = MaisPerto(embarque, relogio, chegadas);
        if (escolhido is null)
        {
            if (estado is not null)
                return ResultadoAviso.Nada();

            return new ResultadoAviso(
                true,
                "SEM_PREVISAO",
                null,
                null,
                null,
                FrasePadrao(letreiro, ponto, null, null, comunicado),
                null);
        }

        return AplicarMarcos(letreiro, ponto, escolhido, relogio, estado, comunicado);
    }

    private static ResultadoAviso AplicarMarcos(
        string letreiro,
        string ponto,
        Chegada veiculo,
        DateTimeOffset relogio,
        EstadoAviso? estado,
        string? comunicado)
    {
        var minutos = MinutosEntre(veiculo.Quando, relogio);
        var marco = MarcoDe(minutos);

        if (minutos == 0)
        {
            if (estado is null)
                return ResultadoAviso.Nada();

            return new ResultadoAviso(
                true,
                "ENCERRADO",
                veiculo.Prefixo,
                0,
                veiculo.IdadeGpsSegundos,
                FrasePadrao(letreiro, ponto, 0, veiculo.IdadeGpsSegundos, comunicado),
                0);
        }

        if (estado is null)
        {
            if (marco is null)
                return ResultadoAviso.Nada();

            return new ResultadoAviso(
                true,
                "ENVIADO",
                veiculo.Prefixo,
                minutos,
                veiculo.IdadeGpsSegundos,
                FrasePadrao(letreiro, ponto, minutos, veiculo.IdadeGpsSegundos, comunicado),
                marco);
        }

        var atrasou = estado.Minutos is int anteriores && minutos > anteriores;
        if (atrasou)
        {
            return new ResultadoAviso(
                true,
                "ATUALIZADO",
                veiculo.Prefixo,
                minutos,
                veiculo.IdadeGpsSegundos,
                FrasePadrao(letreiro, ponto, minutos, veiculo.IdadeGpsSegundos, comunicado),
                marco ?? estado.Marco);
        }

        if (marco is null || estado.Marco == marco)
            return ResultadoAviso.Nada();

        if (marco < estado.Marco)
        {
            return new ResultadoAviso(
                true,
                "ATUALIZADO",
                veiculo.Prefixo,
                minutos,
                veiculo.IdadeGpsSegundos,
                FrasePadrao(letreiro, ponto, minutos, veiculo.IdadeGpsSegundos, comunicado),
                marco);
        }

        return ResultadoAviso.Nada();
    }

    private static ResultadoAviso Encerrar(string letreiro, string ponto, EstadoAviso estado) =>
        new(
            true,
            "ENCERRADO",
            estado.Veiculo,
            0,
            null,
            FrasePadrao(letreiro, ponto, 0, null, null),
            0);
}
