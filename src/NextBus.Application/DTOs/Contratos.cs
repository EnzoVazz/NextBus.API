namespace NextBus.Application.DTOs;

public sealed record RotinaEntrada(
    int CodigoLinha,
    string? Letreiro,
    int Sentido,
    int CodigoParada,
    string? NomeParada,
    string[]? DiasSemana,
    string? Horario,
    bool? Ativa);

public sealed record RotinaSaida(
    long Id,
    int CodigoLinha,
    string Letreiro,
    int Sentido,
    int CodigoParada,
    string NomeParada,
    string[] DiasSemana,
    string Horario,
    bool Ativa);

public sealed record RotinaCriada(RotinaSaida Rotina, bool Reaproveitada);

public sealed record AvisoSaida(
    long Id,
    long RotinaId,
    string Letreiro,
    string Ponto,
    string? Veiculo,
    int? Minutos,
    int? IdadeGpsSegundos,
    string Texto,
    string Status,
    DateOnly Data);

public sealed record UsuarioDto(long Id, string Nome, string Email, string Papel);

public sealed record ParadaDto(int Codigo, string Nome, double Lat, double Lng);

public sealed record OpcaoViagemDto(
    ParadaDto Embarque,
    ParadaDto Desembarque,
    int CodigoLinha,
    string Letreiro,
    int Sentido,
    int? Minutos,
    IReadOnlyList<double[]> Tracado);

public sealed record LinhaSemGpsDto(int Codigo, string Letreiro, int? IdadeGpsSegundos);
