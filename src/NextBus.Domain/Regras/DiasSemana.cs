namespace NextBus.Domain.Regras;

public static class DiasSemana
{
    public static readonly string[] Codigos = ["SEG", "TER", "QUA", "QUI", "SEX", "SAB", "DOM"];

    public static readonly IReadOnlySet<DayOfWeek> Todos = new HashSet<DayOfWeek>(Enum.GetValues<DayOfWeek>());

    public static bool TryParse(IEnumerable<string>? codigos, out IReadOnlySet<DayOfWeek> dias, out string? erro)
    {
        dias = new HashSet<DayOfWeek>();
        erro = null;
        if (codigos is null || !codigos.Any())
        {
            erro = "Escolha ao menos um dia da semana.";
            return false;
        }

        var conjunto = new HashSet<DayOfWeek>();
        foreach (var codigo in codigos)
        {
            if (!TryUm(codigo, out var dia))
            {
                erro = "Use 1 para segunda até 7 para domingo, ou SEG, TER, QUA, QUI, SEX, SAB e DOM.";
                return false;
            }

            conjunto.Add(dia);
        }

        dias = conjunto;
        return true;
    }

    public static string Juntar(IEnumerable<string> codigos)
    {
        TryParse(codigos, out var dias, out _);
        return string.Join(",", dias.Select(Numero).OrderBy(n => n));
    }

    public static IReadOnlySet<DayOfWeek> Ler(string armazenado)
    {
        TryParse(armazenado.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), out var dias, out _);
        return dias;
    }

    private static int Numero(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => 1,
        DayOfWeek.Tuesday => 2,
        DayOfWeek.Wednesday => 3,
        DayOfWeek.Thursday => 4,
        DayOfWeek.Friday => 5,
        DayOfWeek.Saturday => 6,
        _ => 7
    };

    private static bool TryUm(string codigo, out DayOfWeek dia)
    {
        var texto = codigo.Trim().ToUpperInvariant();
        if (texto.Length == 1 && texto[0] is >= '1' and <= '7')
        {
            dia = texto[0] switch
            {
                '1' => DayOfWeek.Monday,
                '2' => DayOfWeek.Tuesday,
                '3' => DayOfWeek.Wednesday,
                '4' => DayOfWeek.Thursday,
                '5' => DayOfWeek.Friday,
                '6' => DayOfWeek.Saturday,
                _ => DayOfWeek.Sunday
            };
            return true;
        }

        dia = texto switch
        {
            "SEG" => DayOfWeek.Monday,
            "TER" => DayOfWeek.Tuesday,
            "QUA" => DayOfWeek.Wednesday,
            "QUI" => DayOfWeek.Thursday,
            "SEX" => DayOfWeek.Friday,
            "SAB" => DayOfWeek.Saturday,
            "DOM" => DayOfWeek.Sunday,
            _ => (DayOfWeek)(-1)
        };
        return Enum.IsDefined(dia);
    }
}
