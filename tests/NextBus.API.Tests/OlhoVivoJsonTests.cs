using NextBus.Domain.Regras;
using NextBus.Infrastructure.Transito;

namespace NextBus.Api.Tests;

public class OlhoVivoJsonTests
{
    [Fact]
    public void Leitura_da_feed_reconhece_a_linha_e_a_previsao_do_guia()
    {
        const string linhas = """
            [{ "cl": 1989, "lc": false, "lt": "7021", "sl": 1, "tl": 10, "tp": "TERM. JOÃO DIAS", "ts": "JD. MARACÁ" }]
            """;
        const string previsao = """
            {
              "hr": "20:09",
              "p": {
                "cp": 4200953,
                "np": "PARADA ROBERTO SELMI DEI B/C",
                "l": [{
                  "c": "7021-10",
                  "cl": 1989,
                  "vs": [{ "p": "74558", "t": "20:21", "a": true, "ta": "2026-11-11T23:08:49Z", "py": -23.67603, "px": -46.75891 }]
                }]
              }
            }
            """;

        var linha = OlhoVivoJson.LerLinhas(linhas).Single();
        Assert.Equal(1989, linha.Codigo);
        Assert.Equal("7021-10", linha.Letreiro);

        var agora = new DateTimeOffset(2026, 11, 11, 20, 9, 0, TimeSpan.FromHours(-3));
        var lida = OlhoVivoJson.LerPrevisao(previsao, agora)!;
        Assert.Equal("74558", lida.Chegadas.Single().Prefixo);
        Assert.True(lida.Chegadas.Single().Acessivel);
        Assert.Equal(12, AvisoRegras.MinutosEntre(lida.Chegadas.Single().Quando, lida.Relogio));
    }
}
