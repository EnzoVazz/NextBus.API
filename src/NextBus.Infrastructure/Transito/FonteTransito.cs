using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NextBus.Application.DTOs;
using NextBus.Application.Interfaces;
using NextBus.Domain.Catalogo;
using NextBus.Domain.Regras;

namespace NextBus.Infrastructure.Transito;

public sealed class FonteAmostra(IRelogio relogio)
{
    public IReadOnlyList<LinhaDto> BuscarLinhas(string? q)
    {
        var termo = string.IsNullOrWhiteSpace(q) ? "" : Catalogo.Normalizar(q);
        return Catalogo.Linhas
            .Where(l => termo.Length == 0
                || Catalogo.Normalizar(l.Letreiro).Contains(termo)
                || Catalogo.Normalizar(l.Origem).Contains(termo)
                || Catalogo.Normalizar(l.Destino).Contains(termo))
            .Select(l => new LinhaDto(l.Codigo, l.Letreiro, l.Sentido, l.Origem, l.Destino, l.Circular))
            .ToList();
    }

    public IReadOnlyList<PosicaoDto> Posicoes(int codigoLinha)
    {
        var linha = Catalogo.Linhas.FirstOrDefault(l => l.Codigo == codigoLinha);
        if (linha is null)
            return [];

        if (codigoLinha == Catalogo.Codigo7021)
        {
            return
            [
                new PosicaoDto("74558", true, 20, -23.67603, -46.75891, linha.Letreiro, linha.Sentido)
            ];
        }

        if (codigoLinha == Catalogo.Codigo8000)
        {
            return
            [
                new PosicaoDto("80001", false, 300, -23.53000, -46.69000, linha.Letreiro, linha.Sentido)
            ];
        }

        return [];
    }

    public PrevisaoDto? Previsao(int codigoParada, int codigoLinha)
    {
        if (codigoParada != Catalogo.ParadaRoberto || codigoLinha != Catalogo.Codigo7021)
            return new PrevisaoDto(relogio.Agora(), []);

        var agora = relogio.Agora();
        return new PrevisaoDto(agora,
        [
            new Chegada("11402", agora.AddMinutes(-8), false, 50),
            new Chegada("74558", agora.AddMinutes(12), true, 20),
            new Chegada("11441", agora.AddMinutes(38), false, 90)
        ]);
    }
}

public sealed class FonteTransito(IOlhoVivo olhoVivo, FonteAmostra amostra, IMemoryCache cache, ILogger<FonteTransito> log) : IFonteTransito
{
    private static readonly TimeSpan Cache = TimeSpan.FromSeconds(15);

    public async Task<bool> OlhoVivoDisponivel(CancellationToken ct) => await olhoVivo.GarantirAsync(ct);

    public Task<IReadOnlyList<LinhaDto>> BuscarLinhas(string? q, CancellationToken ct) =>
        ComCache($"linhas:{q}", () => olhoVivo.BuscarLinhasAsync(q, ct), () => amostra.BuscarLinhas(q), ct);

    public Task<IReadOnlyList<PosicaoDto>> Posicoes(int codigoLinha, CancellationToken ct) =>
        ComCache($"posicoes:{codigoLinha}", () => olhoVivo.PosicoesAsync(codigoLinha, ct), () => amostra.Posicoes(codigoLinha), ct);

    public Task<PrevisaoDto?> Previsao(int codigoParada, int codigoLinha, CancellationToken ct) =>
        ComCache($"previsao:{codigoParada}:{codigoLinha}", () => olhoVivo.PrevisaoAsync(codigoParada, codigoLinha, ct), () => amostra.Previsao(codigoParada, codigoLinha), ct);

    private async Task<T> ComCache<T>(string chave, Func<Task<T>> aoVivo, Func<T> local, CancellationToken ct)
    {
        if (cache.TryGetValue(chave, out T? salvo) && salvo is not null)
            return salvo;

        T valor;
        try
        {
            if (await olhoVivo.GarantirAsync(ct))
                valor = await aoVivo();
            else
                valor = local();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "A Olho Vivo falhou. A consulta usa o recorte local.");
            valor = local();
        }

        cache.Set(chave, valor, Cache);
        return valor;
    }

}
