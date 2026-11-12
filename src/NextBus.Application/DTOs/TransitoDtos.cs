using NextBus.Domain.Regras;

namespace NextBus.Application.DTOs;

public sealed record LinhaDto(int Codigo, string Letreiro, int Sentido, string Origem, string Destino, bool Circular);

public sealed record PosicaoDto(string Veiculo, bool Acessivel, int IdadeGpsSegundos, double Lat, double Lng, string Letreiro, int Sentido);

public sealed record PrevisaoDto(DateTimeOffset Relogio, IReadOnlyList<Chegada> Chegadas);
