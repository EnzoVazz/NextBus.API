using NextBus.Application.DTOs;
using NextBus.Application.Interfaces;
using NextBus.Api.Seguranca;
using ErroResposta = NextBus.Api.ErroResposta;

namespace NextBus.Api.Endpoints;

public static class ApiEndpoints
{
    public static void Mapear(WebApplication app)
    {
        var auth = app.MapGroup("/auth").WithTags("Acesso");
        auth.MapPost("/registrar", Registrar).AllowAnonymous()
            .WithSummary("Cria uma conta de cidadão.")
            .Produces<UsuarioDto>(StatusCodes.Status201Created)
            .Produces<ErroResposta>(StatusCodes.Status400BadRequest)
            .Produces<ErroResposta>(StatusCodes.Status409Conflict);
        auth.MapPost("/login", Login).AllowAnonymous()
            .WithSummary("Devolve um JWT com o papel CIDADAO ou GESTOR.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<ErroResposta>(StatusCodes.Status401Unauthorized);
        auth.MapGet("/eu", Eu).RequireAuthorization()
            .WithSummary("Devolve a pessoa autenticada.")
            .Produces<UsuarioDto>(StatusCodes.Status200OK)
            .Produces<ErroResposta>(StatusCodes.Status401Unauthorized);

        var leitura = app.MapGroup("").RequireAuthorization().RequireRateLimiting("leitura").WithTags("Consulta");
        leitura.MapGet("/linhas", Linhas)
            .WithSummary("Busca linhas pelo número ou pelo nome.")
            .Produces<IReadOnlyList<LinhaDto>>(StatusCodes.Status200OK)
            .Produces<ErroResposta>(StatusCodes.Status429TooManyRequests);
        leitura.MapGet("/paradas/proximas", Paradas)
            .WithSummary("Lista paradas do catálogo local perto de uma coordenada.")
            .Produces<IReadOnlyList<ParadaDto>>(StatusCodes.Status200OK);
        leitura.MapGet("/linhas/{codigo:int}/posicoes", Posicoes)
            .WithSummary("Posição dos ônibus da linha, com letreiro, sentido, acessibilidade e idade do GPS.")
            .Produces<IReadOnlyList<PosicaoDto>>(StatusCodes.Status200OK);
        leitura.MapGet("/previsao", Previsao)
            .WithSummary("Previsão da parada e da linha, com o relógio da feed.")
            .Produces<PrevisaoDto>(StatusCodes.Status200OK);

        var cidadao = app.MapGroup("").RequireAuthorization("Cidadao").WithTags("Cidadão");
        cidadao.MapPost("/viagens/planejar", Planejar)
            .WithSummary("Recomenda até três viagens diretas entre a origem e o endereço de destino.")
            .Produces<PlanejarResponse>(StatusCodes.Status200OK)
            .Produces<ErroResposta>(StatusCodes.Status400BadRequest);
        cidadao.MapGet("/rotinas", ListarRotinas)
            .WithSummary("Lista as rotinas da pessoa autenticada.")
            .Produces<IReadOnlyList<RotinaSaida>>(StatusCodes.Status200OK);
        cidadao.MapPost("/rotinas", CriarRotina)
            .WithSummary("Cria uma rotina. A mesma Idempotency-Key devolve a rotina já criada.")
            .Produces<RotinaSaida>(StatusCodes.Status201Created)
            .Produces<RotinaSaida>(StatusCodes.Status200OK)
            .Produces<ErroResposta>(StatusCodes.Status400BadRequest)
            .Produces<ErroResposta>(StatusCodes.Status409Conflict);
        cidadao.MapGet("/rotinas/{id:long}", ObterRotina)
            .Produces<RotinaSaida>(StatusCodes.Status200OK)
            .Produces<ErroResposta>(StatusCodes.Status404NotFound);
        cidadao.MapPut("/rotinas/{id:long}", AtualizarRotina)
            .WithSummary("Edita ou desliga a rotina.")
            .Produces<RotinaSaida>(StatusCodes.Status200OK)
            .Produces<ErroResposta>(StatusCodes.Status404NotFound);
        cidadao.MapDelete("/rotinas/{id:long}", ExcluirRotina)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErroResposta>(StatusCodes.Status404NotFound);
        cidadao.MapGet("/avisos", Avisos)
            .WithSummary("Calcula e devolve os avisos do dia das rotinas da pessoa.")
            .Produces<IReadOnlyList<AvisoSaida>>(StatusCodes.Status200OK);

        app.MapGet("/operacao/linhas-sem-gps", SemGps).RequireAuthorization("Gestor").WithTags("Operação")
            .WithSummary("Lista linhas cujo GPS mais recente passou de três minutos.")
            .Produces<IReadOnlyList<LinhaSemGpsDto>>(StatusCodes.Status200OK)
            .Produces<ErroResposta>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> Registrar(RegistrarBody body, IAuthAppService auth, CancellationToken ct)
    {
        var usuario = await auth.RegistrarAsync(body.Nome, body.Email, body.Senha, ct);
        return Results.Created("/auth/eu", usuario);
    }

    private static async Task<IResult> Login(LoginBody body, IAuthAppService auth, TokenJwt jwt, CancellationToken ct)
    {
        var usuario = await auth.AutenticarAsync(body.Email, body.Senha, ct);
        if (usuario is null)
            return Results.Json(ErroResposta.De("Não autenticado", 401, "E-mail ou senha inválidos."), statusCode: 401);

        return Results.Ok(new LoginResponse(jwt.Emitir(usuario), usuario.Papel, usuario.Nome));
    }

    private static async Task<IResult> Eu(HttpContext http, IAuthAppService auth, CancellationToken ct) =>
        Results.Ok(await auth.ObterAsync(TokenJwt.UsuarioId(http.User), ct));

    private static async Task<IResult> Linhas(string? q, IConsultaAppService consulta, CancellationToken ct) =>
        Results.Ok(await consulta.LinhasAsync(q, ct));

    private static IResult Paradas(double lat, double lng, double? raio, IConsultaAppService consulta) =>
        Results.Ok(consulta.Paradas(lat, lng, raio));

    private static async Task<IResult> Posicoes(int codigo, IConsultaAppService consulta, CancellationToken ct) =>
        Results.Ok(await consulta.PosicoesAsync(codigo, ct));

    private static async Task<IResult> Previsao(int parada, int linha, IConsultaAppService consulta, CancellationToken ct) =>
        Results.Ok(await consulta.PrevisaoAsync(parada, linha, ct));

    private static async Task<IResult> Planejar(PlanejarBody body, IPlanejamentoAppService planejador, CancellationToken ct)
    {
        var opcoes = await planejador.PlanejarAsync(body.Origem?.Lat, body.Origem?.Lng, body.Destino, ct);
        return Results.Ok(new PlanejarResponse(opcoes.Select(o => new OpcaoResponse(
            new PontoResponse(o.Embarque.Codigo, o.Embarque.Nome, o.Embarque.Lat, o.Embarque.Lng),
            new PontoResponse(o.Desembarque.Codigo, o.Desembarque.Nome, o.Desembarque.Lat, o.Desembarque.Lng),
            new LinhaResumo(o.CodigoLinha, o.Letreiro, o.Sentido),
            o.Minutos,
            o.Tracado)).ToList()));
    }

    private static async Task<IResult> ListarRotinas(HttpContext http, IRotinaAppService servico, CancellationToken ct) =>
        Results.Ok(await servico.ListarAsync(TokenJwt.UsuarioId(http.User), ct));

    private static async Task<IResult> CriarRotina(HttpContext http, RotinaEntrada entrada, IRotinaAppService servico, CancellationToken ct)
    {
        var chave = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
        var criada = await servico.CriarAsync(TokenJwt.UsuarioId(http.User), entrada, chave, ct);
        return criada.Reaproveitada
            ? Results.Ok(criada.Rotina)
            : Results.Created($"/rotinas/{criada.Rotina.Id}", criada.Rotina);
    }

    private static async Task<IResult> ObterRotina(long id, HttpContext http, IRotinaAppService servico, CancellationToken ct) =>
        Results.Ok(await servico.ObterAsync(TokenJwt.UsuarioId(http.User), id, ct));

    private static async Task<IResult> AtualizarRotina(long id, RotinaEntrada entrada, HttpContext http, IRotinaAppService servico, CancellationToken ct) =>
        Results.Ok(await servico.AtualizarAsync(TokenJwt.UsuarioId(http.User), id, entrada, ct));

    private static async Task<IResult> ExcluirRotina(long id, HttpContext http, IRotinaAppService servico, CancellationToken ct)
    {
        await servico.ExcluirAsync(TokenJwt.UsuarioId(http.User), id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Avisos(HttpContext http, IAvisoAppService servico, CancellationToken ct) =>
        Results.Ok(await servico.ListarAsync(TokenJwt.UsuarioId(http.User), ct));

    private static async Task<IResult> SemGps(IOperacaoAppService operacao, CancellationToken ct) =>
        Results.Ok(await operacao.LinhasSemGpsAsync(ct));
}

public sealed record RegistrarBody(string Nome, string Email, string Senha);
public sealed record LoginBody(string Email, string Senha);
public sealed record LoginResponse(string Token, string Papel, string Nome);
public sealed record OrigemBody(double Lat, double Lng);
public sealed record PlanejarBody(OrigemBody? Origem, string? Destino);
public sealed record PontoResponse(int Codigo, string Nome, double Lat, double Lng);
public sealed record LinhaResumo(int Codigo, string Letreiro, int Sentido);
public sealed record OpcaoResponse(PontoResponse Embarque, PontoResponse Desembarque, LinhaResumo Linha, int? Minutos, IReadOnlyList<double[]> Tracado);
public sealed record PlanejarResponse(IReadOnlyList<OpcaoResponse> Opcoes);
