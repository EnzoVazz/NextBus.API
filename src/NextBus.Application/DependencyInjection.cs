using Microsoft.Extensions.DependencyInjection;
using NextBus.Application.Interfaces;
using NextBus.Application.Services;

namespace NextBus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthAppService, AuthAppService>();
        services.AddScoped<IRotinaAppService, RotinaAppService>();
        services.AddScoped<IAvisoAppService, AvisoAppService>();
        services.AddScoped<IPlanejamentoAppService, PlanejamentoAppService>();
        services.AddScoped<IConsultaAppService, ConsultaAppService>();
        services.AddScoped<IOperacaoAppService, OperacaoAppService>();
        return services;
    }
}
