using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NextBus.Api;

namespace NextBus.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNextBusAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var chaveJwt = configuration["Jwt:Chave"] ?? throw new InvalidOperationException("Defina Jwt:Chave.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Emissor"],
                    ValidAudience = configuration["Jwt:Audiencia"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJwt)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                o.Events = new JwtBearerEvents
                {
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await ctx.Response.WriteAsJsonAsync(ErroResposta.De("Não autenticado", 401, "Informe um token JWT válido."));
                    },
                    OnForbidden = async ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await ctx.Response.WriteAsJsonAsync(ErroResposta.De("Acesso negado", 403, "Seu papel não pode usar este recurso."));
                    }
                };
            });
        services.AddAuthorization(o =>
        {
            o.AddPolicy("Cidadao", p => p.RequireRole("CIDADAO"));
            o.AddPolicy("Gestor", p => p.RequireRole("GESTOR"));
        });
        services.AddSingleton<Seguranca.TokenJwt>();
        return services;
    }

    public static IServiceCollection AddNextBusRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var limite = int.TryParse(configuration["RateLimit:Limite"], out var configurado) ? configurado : 60;
        services.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ctx.HttpContext.Response.WriteAsJsonAsync(
                    ErroResposta.De("Muitas requisições", 429, "O limite de consultas foi atingido. Tente de novo em instantes."), ct);
            };
            opcoes.AddPolicy("leitura", http =>
            {
                var chave = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? http.Connection.RemoteIpAddress?.ToString()
                    ?? "anon";
                return RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite,
                    Window = TimeSpan.FromSeconds(60),
                    QueueLimit = 0
                });
            });
        });
        return services;
    }

    public static IServiceCollection AddNextBusSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NextBus",
                Version = "v1",
                Description = "Aviso de chegada do ônibus em São Paulo. A frase do aviso é padrão; a geração por modelo fica fora desta API."
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Cole só o token do login, sem a palavra Bearer."
            });
            c.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", documento)] = []
            });
        });
        return services;
    }

    public static WebApplication MapNextBusHealth(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = EscreverSaude,
            AllowCachingResponses = false
        })
        .WithTags("Acesso")
        .WithSummary("Verifica o banco e a autenticação na Olho Vivo.")
        .AllowAnonymous();
        return app;
    }

    private static async Task EscreverSaude(HttpContext contexto, HealthReport relatorio)
    {
        contexto.Response.ContentType = "application/json";
        var corpo = new
        {
            status = relatorio.Status.ToString(),
            resultados = relatorio.Entries.Select(e => new
            {
                nome = e.Key,
                status = e.Value.Status.ToString(),
                descricao = e.Value.Description
            })
        };
        await contexto.Response.WriteAsync(JsonSerializer.Serialize(corpo));
    }
}
