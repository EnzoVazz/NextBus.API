using System.Security.Cryptography;
using System.Text;
using NextBus.Application.DTOs;
using NextBus.Application.Interfaces;
using NextBus.Domain.Entities;
using NextBus.Domain.Exceptions;
using NextBus.Domain.Regras;

namespace NextBus.Application.Services;

public sealed class RotinaAppService(
    IRotinaRepository rotinas,
    IIdempotenciaRepository idempotencias,
    IUnitOfWork unidade) : IRotinaAppService
{
    public async Task<RotinaCriada> CriarAsync(long usuarioId, RotinaEntrada entrada, string? chaveIdempotencia, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(chaveIdempotencia))
            throw new ValidationException("Idempotency-Key", "Informe o cabeçalho Idempotency-Key.");

        var chave = chaveIdempotencia.Trim();
        if (chave.Length > 80)
            throw new ValidationException("Idempotency-Key", "A chave de idempotência aceita até 80 caracteres.");

        Validar(entrada);
        var hash = Hash(entrada);
        var existente = await idempotencias.ObterAsync(usuarioId, chave, ct);
        if (existente is not null)
        {
            if (!existente.HashCorpo.Equals(hash, StringComparison.Ordinal))
                throw new ConflictException("Esta chave já criou outra rotina.", "Idempotency-Key");

            var rotina = await rotinas.ObterPorIdAsync(existente.RotinaId, ct)
                ?? throw new ResourceNotFoundException("Rotina não encontrada.");
            return new RotinaCriada(Mapa(rotina), true);
        }

        if (await rotinas.ExisteAtivaAsync(usuarioId, entrada.CodigoLinha, entrada.CodigoParada, entrada.Horario!, null, ct))
            throw new ConflictException("Já existe uma rotina ativa com esta linha, este ponto e este horário.", "horario");

        var nova = Nova(usuarioId, entrada);
        await rotinas.AdicionarAsync(nova, ct);
        await unidade.SalvarAsync(ct);
        await idempotencias.AdicionarAsync(Idempotencia.Criar(usuarioId, chave, hash, nova.Id), ct);
        return new RotinaCriada(Mapa(nova), false);
    }

    public async Task<IReadOnlyList<RotinaSaida>> ListarAsync(long usuarioId, CancellationToken ct)
    {
        var lista = await rotinas.ListarPorUsuarioAsync(usuarioId, ct);
        return lista.Select(Mapa).ToList();
    }

    public async Task<RotinaSaida> ObterAsync(long usuarioId, long id, CancellationToken ct)
    {
        var rotina = await rotinas.ObterDoUsuarioAsync(usuarioId, id, ct)
            ?? throw new ResourceNotFoundException("Rotina não encontrada.");
        return Mapa(rotina);
    }

    public async Task<RotinaSaida> AtualizarAsync(long usuarioId, long id, RotinaEntrada entrada, CancellationToken ct)
    {
        Validar(entrada);
        var rotina = await rotinas.ObterDoUsuarioAsync(usuarioId, id, ct)
            ?? throw new ResourceNotFoundException("Rotina não encontrada.");

        if (AtivaDe(entrada) && await rotinas.ExisteAtivaAsync(usuarioId, entrada.CodigoLinha, entrada.CodigoParada, entrada.Horario!, id, ct))
            throw new ConflictException("Já existe uma rotina ativa com esta linha, este ponto e este horário.", "horario");

        rotina.Atualizar(
            entrada.CodigoLinha,
            entrada.Letreiro!.Trim(),
            entrada.Sentido,
            entrada.CodigoParada,
            entrada.NomeParada!.Trim(),
            DiasSemana.Juntar(entrada.DiasSemana!),
            entrada.Horario!,
            AtivaDe(entrada));
        await unidade.SalvarAsync(ct);
        return Mapa(rotina);
    }

    public async Task ExcluirAsync(long usuarioId, long id, CancellationToken ct)
    {
        var rotina = await rotinas.ObterDoUsuarioAsync(usuarioId, id, ct)
            ?? throw new ResourceNotFoundException("Rotina não encontrada.");
        rotinas.Remover(rotina);
        await unidade.SalvarAsync(ct);
    }

    private static void Validar(RotinaEntrada entrada)
    {
        var erros = new List<ErroCampo>();
        if (entrada.CodigoLinha <= 0)
            erros.Add(new("codigoLinha", "Escolha a linha na lista."));
        if (string.IsNullOrWhiteSpace(entrada.Letreiro))
            erros.Add(new("letreiro", "Informe o letreiro da linha."));
        if (entrada.Sentido is not (1 or 2))
            erros.Add(new("sentido", "O sentido é 1 ou 2."));
        if (entrada.CodigoParada <= 0)
            erros.Add(new("codigoParada", "Escolha o ponto na lista."));
        if (string.IsNullOrWhiteSpace(entrada.NomeParada))
            erros.Add(new("nomeParada", "Informe o nome do ponto."));
        if (!DiasSemana.TryParse(entrada.DiasSemana, out _, out var erroDias))
            erros.Add(new("diasSemana", erroDias!));
        if (!TimeOnly.TryParseExact(entrada.Horario, "HH:mm", out _))
            erros.Add(new("horario", "Informe o horário no formato HH:mm."));
        if (erros.Count > 0)
            throw new ValidationException(erros);
    }

    private static Rotina Nova(long usuarioId, RotinaEntrada entrada) =>
        Rotina.Criar(
            usuarioId,
            entrada.CodigoLinha,
            entrada.Letreiro!.Trim(),
            entrada.Sentido,
            entrada.CodigoParada,
            entrada.NomeParada!.Trim(),
            DiasSemana.Juntar(entrada.DiasSemana!),
            entrada.Horario!,
            AtivaDe(entrada));

    public static RotinaSaida Mapa(Rotina rotina) => new(
        rotina.Id,
        rotina.CodigoLinha,
        rotina.Letreiro,
        rotina.Sentido,
        rotina.CodigoParada,
        rotina.NomeParada,
        rotina.DiasSemana.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        rotina.Horario,
        rotina.Ativa);

    private static bool AtivaDe(RotinaEntrada entrada) => entrada.Ativa ?? true;

    public static string Hash(RotinaEntrada entrada)
    {
        var dias = entrada.DiasSemana is null ? "" : string.Join(",", entrada.DiasSemana.Select(d => d.Trim().ToUpperInvariant()).OrderBy(d => d));
        var bruto = $"{entrada.CodigoLinha}|{entrada.Letreiro}|{entrada.Sentido}|{entrada.CodigoParada}|{entrada.NomeParada}|{dias}|{entrada.Horario}|{AtivaDe(entrada)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bruto)));
    }
}
