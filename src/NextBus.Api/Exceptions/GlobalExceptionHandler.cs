using Microsoft.AspNetCore.Diagnostics;
using NextBus.Domain.Exceptions;
using ErroResposta = NextBus.Api.ErroResposta;

namespace NextBus.Api.Exceptions;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var resposta = exception switch
        {
            ValidationException validacao => new ErroResposta("Erro de validação", StatusCodes.Status400BadRequest, validacao.Erros),
            ConflictException conflito => ErroResposta.De("Conflito", StatusCodes.Status409Conflict, conflito.Message, conflito.Campo),
            ResourceNotFoundException ausente => ErroResposta.De("Não encontrado", StatusCodes.Status404NotFound, ausente.Message),
            DomainException dominio => ErroResposta.De("Erro de validação", StatusCodes.Status400BadRequest, dominio.Message),
            _ => null
        };

        if (resposta is null)
        {
            logger.LogError(exception, "Erro não tratado.");
            resposta = ErroResposta.De("Erro interno", StatusCodes.Status500InternalServerError, "A requisição não pôde ser concluída.");
        }

        httpContext.Response.StatusCode = resposta.Status;
        await httpContext.Response.WriteAsJsonAsync(resposta, cancellationToken);
        return true;
    }
}
