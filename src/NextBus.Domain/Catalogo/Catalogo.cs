namespace NextBus.Domain.Catalogo;

public sealed record ParadaCatalogo(int Codigo, string Nome, double Lat, double Lng);

public sealed record LinhaCatalogo(int Codigo, string Letreiro, int Sentido, string Origem, string Destino, bool Circular);

public sealed record ComunicadoCatalogo(string Letreiro, string Texto, DateOnly Inicio, DateOnly Fim);

public sealed record EnderecoCatalogo(string Nome, double Lat, double Lng);

public static class Catalogo
{
    public const int Codigo7021 = 1989;
    public const string Letreiro7021 = "7021-10";
    public const int ParadaRoberto = 4200953;
    public const string NomeRoberto = "Roberto Selmi Dei";
    public const int ParadaMaraca = 4201001;
    public const string NomeMaraca = "Jardim Maracá";
    public const int Codigo8000 = 1273;
    public const string Letreiro8000 = "8000-10";

    public static readonly IReadOnlyList<LinhaCatalogo> Linhas =
    [
        new(Codigo7021, Letreiro7021, 1, "TERM. JOÃO DIAS", "JD. MARACÁ", false),
        new(Codigo8000, Letreiro8000, 1, "PCA. RAMOS DE AZEVEDO", "TERMINAL LAPA", false)
    ];

    public static readonly IReadOnlyList<ParadaCatalogo> Paradas =
    [
        new(ParadaRoberto, NomeRoberto, -23.675901, -46.752812),
        new(ParadaMaraca, NomeMaraca, -23.688000, -46.740000),
        new(4300001, "Terminal Lapa", -23.520500, -46.701200),
        new(4300002, "Praça Ramos de Azevedo", -23.545600, -46.638800)
    ];

    public static readonly IReadOnlyList<(int CodigoLinha, int CodigoParada)> Atendimentos =
    [
        (Codigo7021, ParadaRoberto),
        (Codigo7021, ParadaMaraca),
        (Codigo8000, 4300001),
        (Codigo8000, 4300002)
    ];

    public static readonly IReadOnlyDictionary<int, IReadOnlyList<double[]>> Tracados =
        new Dictionary<int, IReadOnlyList<double[]>>
        {
            [Codigo7021] =
            [
                [-23.675901, -46.752812],
                [-23.682000, -46.746400],
                [-23.688000, -46.740000]
            ],
            [Codigo8000] =
            [
                [-23.545600, -46.638800],
                [-23.533000, -46.670000],
                [-23.520500, -46.701200]
            ]
        };

    public static readonly IReadOnlyList<EnderecoCatalogo> Enderecos =
    [
        new("Jardim Maracá", -23.688200, -46.740200),
        new("Roberto Selmi Dei", -23.675901, -46.752812),
        new("Terminal Lapa", -23.520500, -46.701200),
        new("Avenida Paulista, 1000", -23.564800, -46.652800)
    ];

    public static IReadOnlyList<ComunicadoCatalogo> Comunicados(DateOnly hoje) =>
    [
        new(Letreiro7021, "Operação parcial no sentido João Dias.", hoje.AddDays(-1), hoje.AddDays(30)),
        new(Letreiro8000, "Comunicado encerrado da linha 8000.", hoje.AddDays(-40), hoje.AddDays(-1))
    ];

    public static string? ComunicadoVigente(string letreiro, DateOnly dia) =>
        Comunicados(dia).FirstOrDefault(c =>
            c.Letreiro.Equals(letreiro, StringComparison.OrdinalIgnoreCase) &&
            dia >= c.Inicio && dia <= c.Fim)?.Texto;

    public static EnderecoCatalogo? Geocodificar(string endereco)
    {
        var busca = Normalizar(endereco);
        return Enderecos.FirstOrDefault(e => Normalizar(e.Nome).Contains(busca) || busca.Contains(Normalizar(e.Nome)));
    }

    public static IReadOnlyList<ParadaCatalogo> Proximas(double lat, double lng, double raioMetros) =>
        Paradas
            .Select(p => (p, metros: Geo.Metros(lat, lng, p.Lat, p.Lng)))
            .Where(x => x.metros <= raioMetros)
            .OrderBy(x => x.metros)
            .Select(x => x.p)
            .ToList();

    public static string Normalizar(string texto)
    {
        var form = texto.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = form.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray());
    }
}

public static class Geo
{
    public static double Metros(double lat1, double lng1, double lat2, double lng2)
    {
        const double raio = 6371000d;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return raio * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double Rad(double graus) => graus * Math.PI / 180d;
}
