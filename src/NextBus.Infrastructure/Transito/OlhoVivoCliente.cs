using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NextBus.Application.DTOs;
using NextBus.Application.Interfaces;
using NextBus.Domain.Catalogo;

namespace NextBus.Infrastructure.Transito;

public sealed class OlhoVivoCliente : IOlhoVivo
{
    private readonly HttpClient _http;
    private readonly ILogger<OlhoVivoCliente> _log;
    private readonly IRelogio _relogio;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string? _token;
    private DateTimeOffset _validoAte = DateTimeOffset.MinValue;

    public OlhoVivoCliente(HttpClient http, IConfiguration config, IRelogio relogio, ILogger<OlhoVivoCliente> log)
    {
        _http = http;
        _relogio = relogio;
        _log = log;
        _token = Environment.GetEnvironmentVariable("OLHOVIVO_TOKEN");
        if (string.IsNullOrWhiteSpace(_token))
            _token = config["OlhoVivo:Token"];
    }

    public async Task<bool> GarantirAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow < _validoAte)
            return true;
        if (string.IsNullOrWhiteSpace(_token))
            return false;

        await _gate.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow < _validoAte)
                return true;

            var url = "Login/Autenticar?token=" + Uri.EscapeDataString(_token);
            using var resposta = await _http.PostAsync(url, new StringContent(""), ct);
            var corpo = (await resposta.Content.ReadAsStringAsync(ct)).Trim().Trim('"');
            var ok = corpo.Equals("true", StringComparison.OrdinalIgnoreCase);
            _validoAte = ok ? DateTimeOffset.UtcNow.AddMinutes(20) : DateTimeOffset.MinValue;
            if (!ok)
                _log.LogWarning("A autenticação na Olho Vivo respondeu false.");
            return ok;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<LinhaDto>> BuscarLinhasAsync(string? q, CancellationToken ct)
    {
        var json = await GetAsync("Linha/Buscar?termosBusca=" + Uri.EscapeDataString(q ?? ""), ct);
        return json is null ? [] : OlhoVivoJson.LerLinhas(json);
    }

    public async Task<IReadOnlyList<PosicaoDto>> PosicoesAsync(int codigoLinha, CancellationToken ct)
    {
        var json = await GetAsync("Posicao/Linha?codigoLinha=" + codigoLinha, ct);
        if (json is null)
            return [];

        var linha = Catalogo.Linhas.FirstOrDefault(l => l.Codigo == codigoLinha);
        return OlhoVivoJson.LerPosicoes(json, codigoLinha, linha?.Letreiro ?? codigoLinha.ToString(), linha?.Sentido ?? 1, _relogio.Agora());
    }

    public async Task<PrevisaoDto?> PrevisaoAsync(int codigoParada, int codigoLinha, CancellationToken ct)
    {
        var json = await GetAsync($"Previsao?codigoParada={codigoParada}&codigoLinha={codigoLinha}", ct);
        return json is null ? null : OlhoVivoJson.LerPrevisao(json, _relogio.Agora());
    }

    private async Task<string?> GetAsync(string caminho, CancellationToken ct)
    {
        using var resposta = await _http.GetAsync(caminho, ct);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized)
        {
            _validoAte = DateTimeOffset.MinValue;
            if (!await GarantirAsync(ct))
                return null;
            using var repetida = await _http.GetAsync(caminho, ct);
            if (!repetida.IsSuccessStatusCode)
                return null;
            return await repetida.Content.ReadAsStringAsync(ct);
        }

        if (!resposta.IsSuccessStatusCode)
            return null;
        return await resposta.Content.ReadAsStringAsync(ct);
    }
}

public sealed class OlhoVivoHealthCheck(IOlhoVivo olhoVivo) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await olhoVivo.GarantirAsync(cancellationToken);
            return ok
                ? HealthCheckResult.Healthy("Autenticação na Olho Vivo respondeu true.")
                : HealthCheckResult.Degraded("Autenticação na Olho Vivo respondeu false.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("A Olho Vivo não respondeu.", ex);
        }
    }
}
