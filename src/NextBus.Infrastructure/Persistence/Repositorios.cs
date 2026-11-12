using Microsoft.EntityFrameworkCore;
using NextBus.Application.Interfaces;
using NextBus.Domain.Entities;

namespace NextBus.Infrastructure.Persistence;

public sealed class UsuarioRepository(NextBusDb db) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct) =>
        db.Usuarios.SingleOrDefaultAsync(u => u.Email == email, ct);

    public Task<Usuario?> ObterPorIdAsync(long id, CancellationToken ct) =>
        db.Usuarios.SingleOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExisteAsync(string email, CancellationToken ct) =>
        db.Usuarios.AnyAsync(u => u.Email == email, ct);

    public async Task AdicionarAsync(Usuario usuario, CancellationToken ct) =>
        await db.Usuarios.AddAsync(usuario, ct);
}

public sealed class RotinaRepository(NextBusDb db) : IRotinaRepository
{
    public async Task<IReadOnlyList<Rotina>> ListarPorUsuarioAsync(long usuarioId, CancellationToken ct) =>
        await db.Rotinas.Where(r => r.UsuarioId == usuarioId).OrderBy(r => r.Horario).ToListAsync(ct);

    public async Task<IReadOnlyList<Rotina>> ListarAtivasPorUsuarioAsync(long usuarioId, CancellationToken ct) =>
        await db.Rotinas.Where(r => r.UsuarioId == usuarioId && r.Ativa).ToListAsync(ct);

    public Task<Rotina?> ObterDoUsuarioAsync(long usuarioId, long id, CancellationToken ct) =>
        db.Rotinas.SingleOrDefaultAsync(r => r.Id == id && r.UsuarioId == usuarioId, ct);

    public Task<Rotina?> ObterPorIdAsync(long id, CancellationToken ct) =>
        db.Rotinas.SingleOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExisteAtivaAsync(long usuarioId, int codigoLinha, int codigoParada, string horario, long? ignorar, CancellationToken ct) =>
        db.Rotinas.AnyAsync(r =>
            r.UsuarioId == usuarioId &&
            r.Ativa &&
            (ignorar == null || r.Id != ignorar) &&
            r.CodigoLinha == codigoLinha &&
            r.CodigoParada == codigoParada &&
            r.Horario == horario, ct);

    public async Task AdicionarAsync(Rotina rotina, CancellationToken ct) =>
        await db.Rotinas.AddAsync(rotina, ct);

    public void Remover(Rotina rotina)
    {
        var avisos = db.Avisos.Where(a => a.RotinaId == rotina.Id);
        db.Avisos.RemoveRange(avisos);
        db.Rotinas.Remove(rotina);
    }
}

public sealed class AvisoRepository(NextBusDb db) : IAvisoRepository
{
    public Task<Aviso?> ObterDoDiaAsync(long rotinaId, DateOnly data, CancellationToken ct) =>
        db.Avisos.SingleOrDefaultAsync(a => a.RotinaId == rotinaId && a.DataReferencia == data, ct);

    public async Task<IReadOnlyList<Aviso>> ListarPorUsuarioAsync(long usuarioId, CancellationToken ct) =>
        await db.Avisos
            .Where(a => a.Rotina!.UsuarioId == usuarioId)
            .OrderByDescending(a => a.DataReferencia)
            .ThenBy(a => a.Letreiro)
            .ToListAsync(ct);

    public Aviso Adicionar(Aviso aviso)
    {
        db.Avisos.Add(aviso);
        return aviso;
    }
}

public sealed class IdempotenciaRepository(NextBusDb db) : IIdempotenciaRepository
{
    public Task<Idempotencia?> ObterAsync(long usuarioId, string chave, CancellationToken ct) =>
        db.Idempotencias.SingleOrDefaultAsync(i => i.UsuarioId == usuarioId && i.Chave == chave, ct);

    public async Task AdicionarAsync(Idempotencia item, CancellationToken ct)
    {
        await db.Idempotencias.AddAsync(item, ct);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SenhaRepository(NextBusDb db) : ISenhaRepository
{
    public async Task<string?> ObterHashAsync(long usuarioId, CancellationToken ct) =>
        (await db.Senhas.SingleOrDefaultAsync(s => s.UsuarioId == usuarioId, ct))?.Hash;

    public async Task SalvarAsync(long usuarioId, string hash, CancellationToken ct)
    {
        var existente = await db.Senhas.SingleOrDefaultAsync(s => s.UsuarioId == usuarioId, ct);
        if (existente is null)
            db.Senhas.Add(new SenhaGuardada { UsuarioId = usuarioId, Hash = hash });
        else
            existente.Hash = hash;

        await db.SaveChangesAsync(ct);
    }
}

public sealed class UnitOfWork(NextBusDb db) : IUnitOfWork
{
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
