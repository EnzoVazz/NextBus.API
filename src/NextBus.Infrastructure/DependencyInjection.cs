using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NextBus.Application.Interfaces;
using NextBus.Infrastructure.Persistence;
using NextBus.Infrastructure.Security;
using NextBus.Infrastructure.Transito;
using Oracle.EntityFrameworkCore.Infrastructure;

namespace NextBus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var conexao = ConexaoOracle(configuration);
        services.AddDbContext<NextBusDb>(o =>
            o.UseOracle(conexao, oracle =>
                oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)));

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRotinaRepository, RotinaRepository>();
        services.AddScoped<IAvisoRepository, AvisoRepository>();
        services.AddScoped<IIdempotenciaRepository, IdempotenciaRepository>();
        services.AddScoped<ISenhaRepository, SenhaRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<ISenhaHasher, SenhaHasher>();
        services.AddSingleton<IRelogio, RelogioSistema>();
        services.AddMemoryCache();
        services.AddSingleton<FonteAmostra>();
        services.AddSingleton<IFonteTransito, FonteTransito>();

        if (environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IOlhoVivo, OlhoVivoIndisponivel>();
        }
        else
        {
            var baseUrl = (configuration["OlhoVivo:BaseUrl"] ?? "https://api.olhovivo.sptrans.com.br/v2.1").TrimEnd('/') + "/";
            services.AddSingleton(_ => new HttpClient(new HttpClientHandler
            {
                CookieContainer = new System.Net.CookieContainer(),
                UseCookies = true
            })
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(20)
            });
            services.AddSingleton<IOlhoVivo, OlhoVivoCliente>();
        }

        services.AddHealthChecks()
            .AddDbContextCheck<NextBusDb>("banco")
            .AddCheck<OlhoVivoHealthCheck>("olhovivo");

        return services;
    }

    private static string ConexaoOracle(IConfiguration configuration)
    {
        var host = configuration["Oracle:Host"] ?? "oracle.fiap.com.br";
        var port = configuration["Oracle:Port"] ?? "1521";
        var sid = configuration["Oracle:Sid"] ?? "ORCL";
        var user = configuration["Oracle:User"] ?? Environment.GetEnvironmentVariable("ORACLE_USER");
        var password = configuration["Oracle:Password"] ?? Environment.GetEnvironmentVariable("ORACLE_PASSWORD");
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Defina Oracle:User e Oracle:Password em appsettings.Local.json, ou as variáveis ORACLE_USER e ORACLE_PASSWORD.");

        return $"User Id={user};Password={password};Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SID={sid})));";
    }
}
