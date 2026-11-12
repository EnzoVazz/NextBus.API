using NextBus.Domain.Regras;

namespace NextBus.Domain.Tests;

public class AvisoRegrasTests
{
    private static readonly TimeSpan Fuso = TimeSpan.FromHours(-3);

    private static DateTimeOffset Em(int hora, int minuto) =>
        new(2026, 11, 11, hora, minuto, 0, Fuso);

    private static Chegada Onibus(string prefixo, int hora, int minuto, int idade = 20) =>
        new(prefixo, Em(hora, minuto), prefixo == "74558", idade);

    [Fact]
    public void Janela_abre_vinte_minutos_antes_e_fecha_quando_o_veiculo_passa()
    {
        var horario = new TimeOnly(7, 15);
        Assert.False(AvisoRegras.EstaNaJanela(horario, Em(6, 54), false));
        Assert.True(AvisoRegras.EstaNaJanela(horario, Em(6, 55), false));
        Assert.True(AvisoRegras.EstaNaJanela(horario, Em(7, 15), false));
        Assert.False(AvisoRegras.EstaNaJanela(horario, Em(7, 10), true));
        Assert.False(AvisoRegras.EstaNaJanela(horario, Em(9, 16), false));
    }

    [Fact]
    public void Janela_da_meia_noite_comeca_no_dia_anterior()
    {
        var agora = new DateTimeOffset(2026, 11, 11, 23, 55, 0, Fuso);
        Assert.True(AvisoRegras.EstaNaJanela(new TimeOnly(0, 10), agora, false));
    }

    [Fact]
    public void Dia_fora_da_rotina_fica_de_fora()
    {
        var soDomingo = new HashSet<DayOfWeek> { DayOfWeek.Sunday };
        Assert.Null(AvisoRegras.EmbarqueNaJanela(new TimeOnly(7, 15), Em(7, 0), soDomingo));
    }

    [Fact]
    public void Minutos_usam_o_relogio_da_feed()
    {
        Assert.Equal(12, AvisoRegras.MinutosEntre(Em(7, 18), Em(7, 6)));
    }

    [Fact]
    public void Escolhe_o_onibus_mais_perto_do_horario_da_rotina()
    {
        var chegadas = new[]
        {
            Onibus("A", 7, 2),
            Onibus("74558", 7, 18),
            Onibus("C", 7, 41)
        };

        var escolhido = AvisoRegras.MaisPerto(Em(7, 15), Em(7, 0), chegadas);
        Assert.Equal("74558", escolhido!.Prefixo);
    }

    [Fact]
    public void Uma_notificacao_acompanha_o_mesmo_onibus_em_15_10_5_e_2()
    {
        var embarque = Em(7, 15);
        var chegadas = new List<Chegada>
        {
            Onibus("A", 7, 2),
            Onibus("74558", 7, 18),
            Onibus("C", 7, 41)
        };
        EstadoAviso? estado = null;
        var alteracoes = new List<ResultadoAviso>();

        foreach (var minuto in new[] { 3, 8, 9, 13, 16, 18 })
        {
            var resultado = AvisoRegras.Avaliar(
                "7021-10",
                "Roberto Selmi Dei",
                embarque,
                Em(7, minuto),
                chegadas,
                estado,
                comunicado: null);
            if (!resultado.Alterou)
                continue;

            alteracoes.Add(resultado);
            estado = new EstadoAviso(resultado.Status!, resultado.Veiculo, resultado.Marco, resultado.Minutos);
        }

        Assert.Equal(["ENVIADO", "ATUALIZADO", "ATUALIZADO", "ATUALIZADO", "ENCERRADO"], alteracoes.Select(a => a.Status!).ToArray());
        Assert.Equal([15, 10, 5, 2, 0], alteracoes.Select(a => a.Minutos!.Value).ToArray());
        Assert.All(alteracoes, a => Assert.Equal("74558", a.Veiculo));
        Assert.StartsWith("7021-10 no ponto Roberto Selmi Dei: 15 minutos", alteracoes[0].Texto);
    }

    [Fact]
    public void Atraso_nao_troca_o_veiculo_grudado()
    {
        var estado = new EstadoAviso("ENVIADO", "74558", 15, 15);
        var chegadas = new[]
        {
            Onibus("74558", 7, 30),
            Onibus("OUTRO", 7, 16)
        };

        var resultado = AvisoRegras.Avaliar("7021-10", "Roberto Selmi Dei", Em(7, 15), Em(7, 10), chegadas, estado, null);

        Assert.Equal("74558", resultado.Veiculo);
        Assert.Equal(20, resultado.Minutos);
        Assert.Equal("ATUALIZADO", resultado.Status);
    }

    [Fact]
    public void Sem_veiculo_avisa_uma_vez_e_nao_inventa_ocorrencia()
    {
        var primeiro = AvisoRegras.Avaliar("7021-10", "Roberto Selmi Dei", Em(7, 15), Em(7, 0), [], null, null);
        var segundo = AvisoRegras.Avaliar(
            "7021-10",
            "Roberto Selmi Dei",
            Em(7, 15),
            Em(7, 5),
            [],
            new EstadoAviso(primeiro.Status!, primeiro.Veiculo, primeiro.Marco),
            null);

        Assert.Equal("SEM_PREVISAO", primeiro.Status);
        Assert.Equal("7021-10 sem previsão no seu ponto agora.", primeiro.Texto);
        Assert.DoesNotContain("acidente", primeiro.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("desvio", primeiro.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lotação", primeiro.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.False(segundo.Alterou);
    }

    [Fact]
    public void Comunicado_vigente_entra_na_frase_padrao()
    {
        var texto = AvisoRegras.FrasePadrao("7021-10", "Roberto Selmi Dei", 12, 20, "Operação parcial no sentido João Dias.");
        Assert.Contains("7021-10 no ponto Roberto Selmi Dei: 12 minutos", texto);
        Assert.Contains("GPS capturado há 20 segundos", texto);
        Assert.Contains("Comunicado: Operação parcial no sentido João Dias.", texto);
    }
}
