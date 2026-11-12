using NextBus.Application.DTOs;
using NextBus.Domain.Entities;

namespace NextBus.Application.Interfaces;

public interface IRelogio
{
    DateTimeOffset Agora();
}

public interface ISenhaHasher
{
    string GerarHash(string senha);
    bool Conferir(string hash, string senha);
}

public interface IFonteTransito
{
    Task<IReadOnlyList<LinhaDto>> BuscarLinhas(string? q, CancellationToken ct);
    Task<IReadOnlyList<PosicaoDto>> Posicoes(int codigoLinha, CancellationToken ct);
    Task<PrevisaoDto?> Previsao(int codigoParada, int codigoLinha, CancellationToken ct);
}

public interface IOlhoVivo
{
    Task<bool> GarantirAsync(CancellationToken ct);
    Task<IReadOnlyList<LinhaDto>> BuscarLinhasAsync(string? q, CancellationToken ct);
    Task<IReadOnlyList<PosicaoDto>> PosicoesAsync(int codigoLinha, CancellationToken ct);
    Task<PrevisaoDto?> PrevisaoAsync(int codigoParada, int codigoLinha, CancellationToken ct);
}

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct);
    Task<Usuario?> ObterPorIdAsync(long id, CancellationToken ct);
    Task<bool> EmailExisteAsync(string email, CancellationToken ct);
    Task AdicionarAsync(Usuario usuario, CancellationToken ct);
}

public interface IRotinaRepository
{
    Task<IReadOnlyList<Rotina>> ListarPorUsuarioAsync(long usuarioId, CancellationToken ct);
    Task<IReadOnlyList<Rotina>> ListarAtivasPorUsuarioAsync(long usuarioId, CancellationToken ct);
    Task<Rotina?> ObterDoUsuarioAsync(long usuarioId, long id, CancellationToken ct);
    Task<Rotina?> ObterPorIdAsync(long id, CancellationToken ct);
    Task<bool> ExisteAtivaAsync(long usuarioId, int codigoLinha, int codigoParada, string horario, long? ignorar, CancellationToken ct);
    Task AdicionarAsync(Rotina rotina, CancellationToken ct);
    void Remover(Rotina rotina);
}

public interface IAvisoRepository
{
    Task<Aviso?> ObterDoDiaAsync(long rotinaId, DateOnly data, CancellationToken ct);
    Task<IReadOnlyList<Aviso>> ListarPorUsuarioAsync(long usuarioId, CancellationToken ct);
    Aviso Adicionar(Aviso aviso);
}

public interface IIdempotenciaRepository
{
    Task<Idempotencia?> ObterAsync(long usuarioId, string chave, CancellationToken ct);
    Task AdicionarAsync(Idempotencia item, CancellationToken ct);
}

public interface ISenhaRepository
{
    Task<string?> ObterHashAsync(long usuarioId, CancellationToken ct);
    Task SalvarAsync(long usuarioId, string hash, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task SalvarAsync(CancellationToken ct);
}
