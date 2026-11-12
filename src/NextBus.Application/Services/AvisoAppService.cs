using NextBus.Application.DTOs;
using NextBus.Application.Interfaces;
using NextBus.Domain.Catalogo;
using NextBus.Domain.Regras;

namespace NextBus.Application.Services;

public sealed class AvisoAppService(IRotinaRepository rotinas, IAvisoRepository avisos, IFonteTransito fonte, IRelogio relogio, IUnitOfWork unidade) : IAvisoAppService
{
    public async Task<IReadOnlyList<AvisoSaida>> ListarAsync(long usuarioId, CancellationToken ct)
    {
        var agora = relogio.Agora();
        foreach (var rotina in await rotinas.ListarAtivasPorUsuarioAsync(usuarioId, ct))
        {
            if (!TimeOnly.TryParseExact(rotina.Horario, "HH:mm", out var horario))
                continue;

            var embarque = AvisoRegras.EmbarqueNaJanela(horario, agora, DiasSemana.Ler(rotina.DiasSemana));
            if (embarque is null)
                continue;

            var data = DateOnly.FromDateTime(embarque.Value.DateTime);
            var aviso = await avisos.ObterDoDiaAsync(rotina.Id, data, ct);
            if (aviso?.Status is "ENCERRADO" or "SEM_PREVISAO")
                continue;

            var previsao = await fonte.Previsao(rotina.CodigoParada, rotina.CodigoLinha, ct);
            var relogioFeed = previsao?.Relogio ?? agora;
            var comunicado = Catalogo.ComunicadoVigente(rotina.Letreiro, DateOnly.FromDateTime(relogioFeed.DateTime));
            var resultado = AvisoRegras.Avaliar(
                rotina.Letreiro,
                rotina.NomeParada,
                embarque.Value,
                relogioFeed,
                previsao?.Chegadas ?? [],
                aviso is null ? null : new EstadoAviso(aviso.Status, aviso.Veiculo, MarcoLembrado(aviso), aviso.Minutos),
                comunicado);

            if (!resultado.Alterou)
                continue;

            aviso ??= avisos.Adicionar(Domain.Entities.Aviso.Novo(rotina.Id, data));
            aviso.Aplicar(
                rotina.Letreiro,
                rotina.NomeParada,
                resultado.Veiculo,
                resultado.Minutos,
                resultado.IdadeGpsSegundos,
                resultado.Texto ?? "",
                resultado.Status ?? aviso.Status);
        }

        await unidade.SalvarAsync(ct);
        var lista = await avisos.ListarPorUsuarioAsync(usuarioId, ct);
        return lista.Select(a => new AvisoSaida(a.Id, a.RotinaId, a.Letreiro, a.Ponto, a.Veiculo, a.Minutos, a.IdadeGpsSegundos, a.Texto, a.Status, a.DataReferencia)).ToList();
    }

    private static int? MarcoLembrado(Domain.Entities.Aviso aviso)
    {
        if (aviso.Minutos is not int minutos)
            return null;

        return AvisoRegras.MarcoDe(minutos) ?? 15;
    }
}
