using NextBus.Application.DTOs;
using NextBus.Application.Interfaces;
using NextBus.Domain.Catalogo;
using NextBus.Domain.Exceptions;
using NextBus.Domain.Regras;

namespace NextBus.Application.Services;

public sealed class PlanejamentoAppService(IFonteTransito fonte, IRelogio relogio) : IPlanejamentoAppService
{
    public async Task<IReadOnlyList<OpcaoViagemDto>> PlanejarAsync(double? lat, double? lng, string? destino, CancellationToken ct)
    {
        if (lat is null || lng is null)
            throw new ValidationException("origem", "Informe a latitude e a longitude de origem.");
        if (string.IsNullOrWhiteSpace(destino))
            throw new ValidationException("destino", "Informe o endereço de destino.");

        var ponto = Catalogo.Geocodificar(destino);
        if (ponto is null)
            throw new ValidationException("destino", "Não encontrei esse endereço no recorte local.");

        const double raio = 800;
        var origem = Catalogo.Proximas(lat.Value, lng.Value, raio);
        var chegada = Catalogo.Proximas(ponto.Lat, ponto.Lng, raio);
        var opcoes = new List<OpcaoViagemDto>();

        foreach (var embarque in origem)
        {
            foreach (var desembarque in chegada)
            {
                if (embarque.Codigo == desembarque.Codigo)
                    continue;

                var linhas = Catalogo.Atendimentos
                    .Where(a => a.CodigoParada == embarque.Codigo)
                    .Select(a => a.CodigoLinha)
                    .Intersect(Catalogo.Atendimentos.Where(a => a.CodigoParada == desembarque.Codigo).Select(a => a.CodigoLinha));

                foreach (var codigo in linhas)
                {
                    var linha = Catalogo.Linhas.First(l => l.Codigo == codigo);
                    var previsao = await fonte.Previsao(embarque.Codigo, linha.Codigo, ct);
                    var relogioFeed = previsao?.Relogio ?? relogio.Agora();
                    var embarqueHorario = relogioFeed.AddMinutes(15);
                    var veiculo = previsao is null ? null : AvisoRegras.MaisPerto(embarqueHorario, relogioFeed, previsao.Chegadas);
                    int? minutos = veiculo is null ? null : AvisoRegras.MinutosEntre(veiculo.Quando, relogioFeed);
                    var tracado = Catalogo.Tracados.TryGetValue(linha.Codigo, out var pontos) ? pontos : [];
                    opcoes.Add(new OpcaoViagemDto(
                        new ParadaDto(embarque.Codigo, embarque.Nome, embarque.Lat, embarque.Lng),
                        new ParadaDto(desembarque.Codigo, desembarque.Nome, desembarque.Lat, desembarque.Lng),
                        linha.Codigo,
                        linha.Letreiro,
                        linha.Sentido,
                        minutos,
                        tracado));
                }
            }
        }

        return opcoes
            .OrderBy(o => o.Minutos ?? int.MaxValue)
            .ThenBy(o => o.Letreiro)
            .Take(3)
            .ToList();
    }
}

public sealed class ConsultaAppService(IFonteTransito fonte) : IConsultaAppService
{
    public Task<IReadOnlyList<LinhaDto>> LinhasAsync(string? q, CancellationToken ct) => fonte.BuscarLinhas(q, ct);

    public IReadOnlyList<ParadaDto> Paradas(double lat, double lng, double? raio)
    {
        var metros = raio is > 0 and <= 3000 ? raio.Value : 800;
        return Catalogo.Proximas(lat, lng, metros)
            .Select(p => new ParadaDto(p.Codigo, p.Nome, p.Lat, p.Lng))
            .ToList();
    }

    public Task<IReadOnlyList<PosicaoDto>> PosicoesAsync(int codigo, CancellationToken ct) => fonte.Posicoes(codigo, ct);

    public Task<PrevisaoDto?> PrevisaoAsync(int parada, int linha, CancellationToken ct) => fonte.Previsao(parada, linha, ct);
}

public sealed class OperacaoAppService(IFonteTransito fonte) : IOperacaoAppService
{
    public async Task<IReadOnlyList<LinhaSemGpsDto>> LinhasSemGpsAsync(CancellationToken ct)
    {
        var semGps = new List<LinhaSemGpsDto>();
        foreach (var linha in Catalogo.Linhas)
        {
            var posicoes = await fonte.Posicoes(linha.Codigo, ct);
            var idade = posicoes.Count == 0 ? (int?)null : posicoes.Min(p => p.IdadeGpsSegundos);
            if (idade is null || idade > 180)
                semGps.Add(new LinhaSemGpsDto(linha.Codigo, linha.Letreiro, idade));
        }

        return semGps;
    }
}
