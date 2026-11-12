using NextBus.Application.DTOs;

namespace NextBus.Application.Interfaces;

public interface IAuthAppService
{
    Task<UsuarioDto> RegistrarAsync(string nome, string email, string senha, CancellationToken ct);
    Task<UsuarioDto?> AutenticarAsync(string? email, string? senha, CancellationToken ct);
    Task<UsuarioDto> ObterAsync(long id, CancellationToken ct);
}

public interface IRotinaAppService
{
    Task<RotinaCriada> CriarAsync(long usuarioId, RotinaEntrada entrada, string? chaveIdempotencia, CancellationToken ct);
    Task<IReadOnlyList<RotinaSaida>> ListarAsync(long usuarioId, CancellationToken ct);
    Task<RotinaSaida> ObterAsync(long usuarioId, long id, CancellationToken ct);
    Task<RotinaSaida> AtualizarAsync(long usuarioId, long id, RotinaEntrada entrada, CancellationToken ct);
    Task ExcluirAsync(long usuarioId, long id, CancellationToken ct);
}

public interface IAvisoAppService
{
    Task<IReadOnlyList<AvisoSaida>> ListarAsync(long usuarioId, CancellationToken ct);
}

public interface IPlanejamentoAppService
{
    Task<IReadOnlyList<OpcaoViagemDto>> PlanejarAsync(double? lat, double? lng, string? destino, CancellationToken ct);
}

public interface IConsultaAppService
{
    Task<IReadOnlyList<LinhaDto>> LinhasAsync(string? q, CancellationToken ct);
    IReadOnlyList<ParadaDto> Paradas(double lat, double lng, double? raio);
    Task<IReadOnlyList<PosicaoDto>> PosicoesAsync(int codigo, CancellationToken ct);
    Task<PrevisaoDto?> PrevisaoAsync(int parada, int linha, CancellationToken ct);
}

public interface IOperacaoAppService
{
    Task<IReadOnlyList<LinhaSemGpsDto>> LinhasSemGpsAsync(CancellationToken ct);
}
