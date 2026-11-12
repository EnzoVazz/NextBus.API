using System.Text.Json;
using NextBus.Application.DTOs;
using NextBus.Application.Interfaces;
using NextBus.Domain.Regras;

namespace NextBus.Infrastructure.Transito;

public sealed class OlhoVivoIndisponivel : IOlhoVivo
{
    public Task<bool> GarantirAsync(CancellationToken ct) => Task.FromResult(false);
    public Task<IReadOnlyList<LinhaDto>> BuscarLinhasAsync(string? q, CancellationToken ct) => Task.FromResult<IReadOnlyList<LinhaDto>>([]);
    public Task<IReadOnlyList<PosicaoDto>> PosicoesAsync(int codigoLinha, CancellationToken ct) => Task.FromResult<IReadOnlyList<PosicaoDto>>([]);
    public Task<PrevisaoDto?> PrevisaoAsync(int codigoParada, int codigoLinha, CancellationToken ct) => Task.FromResult<PrevisaoDto?>(null);
}

public static class OlhoVivoJson
{
    public static IReadOnlyList<LinhaDto> LerLinhas(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var lista = new List<LinhaDto>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var lt = Texto(item, "lt") ?? "";
            var tl = Texto(item, "tl") ?? "";
            var letreiro = string.IsNullOrEmpty(tl) ? lt : $"{lt}-{tl}";
            lista.Add(new LinhaDto(
                Inteiro(item, "cl"),
                letreiro,
                Inteiro(item, "sl"),
                Texto(item, "tp") ?? "",
                Texto(item, "ts") ?? "",
                item.TryGetProperty("lc", out var lc) && lc.ValueKind == JsonValueKind.True));
        }

        return lista;
    }

    public static IReadOnlyList<PosicaoDto> LerPosicoes(string json, int codigoLinha, string letreiro, int sentido, DateTimeOffset agora)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("vs", out var vs) || vs.ValueKind != JsonValueKind.Array)
            return [];

        return vs.EnumerateArray().Select(v => new PosicaoDto(
            Texto(v, "p") ?? "",
            v.TryGetProperty("a", out var a) && a.ValueKind == JsonValueKind.True,
            Idade(v, agora),
            Decimal(v, "py"),
            Decimal(v, "px"),
            letreiro,
            sentido)).ToList();
    }

    public static PrevisaoDto? LerPrevisao(string json, DateTimeOffset agora)
    {
        using var doc = JsonDocument.Parse(json);
        var relogio = CombinarHorario(Texto(doc.RootElement, "hr"), agora) ?? agora;
        if (!doc.RootElement.TryGetProperty("p", out var p) || p.ValueKind != JsonValueKind.Object)
            return new PrevisaoDto(relogio, []);
        if (!p.TryGetProperty("l", out var linhas) || linhas.ValueKind != JsonValueKind.Array)
            return new PrevisaoDto(relogio, []);

        var chegadas = new List<Chegada>();
        foreach (var linha in linhas.EnumerateArray())
        {
            if (!linha.TryGetProperty("vs", out var vs) || vs.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var v in vs.EnumerateArray())
            {
                var quando = CombinarHorario(Texto(v, "t"), relogio) ?? relogio;
                if (quando < relogio.AddMinutes(-1))
                    quando = quando.AddDays(1);

                chegadas.Add(new Chegada(
                    Texto(v, "p") ?? "",
                    quando,
                    v.TryGetProperty("a", out var a) && a.ValueKind == JsonValueKind.True,
                    Idade(v, relogio)));
            }
        }

        return new PrevisaoDto(relogio, chegadas);
    }

    private static int Idade(JsonElement item, DateTimeOffset agora)
    {
        var ta = Texto(item, "ta");
        if (ta is null || !DateTimeOffset.TryParse(ta, out var capturado))
            return 0;
        return (int)Math.Max(0, (agora.ToUniversalTime() - capturado.ToUniversalTime()).TotalSeconds);
    }

    private static DateTimeOffset? CombinarHorario(string? hhmm, DateTimeOffset referencia)
    {
        if (string.IsNullOrWhiteSpace(hhmm) || !TimeOnly.TryParseExact(hhmm, "HH:mm", out var hora))
            return null;
        var dia = DateOnly.FromDateTime(referencia.DateTime);
        return new DateTimeOffset(dia.ToDateTime(hora), referencia.Offset);
    }

    private static string? Texto(JsonElement item, string nome)
    {
        if (!item.TryGetProperty(nome, out var valor))
            return null;
        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            _ => null
        };
    }

    private static int Inteiro(JsonElement item, string nome)
    {
        if (!item.TryGetProperty(nome, out var valor))
            return 0;
        return valor.ValueKind switch
        {
            JsonValueKind.Number => valor.GetInt32(),
            JsonValueKind.String when int.TryParse(valor.GetString(), out var n) => n,
            _ => 0
        };
    }

    private static double Decimal(JsonElement item, string nome)
    {
        if (!item.TryGetProperty(nome, out var valor) || valor.ValueKind != JsonValueKind.Number)
            return 0;
        return valor.GetDouble();
    }
}
