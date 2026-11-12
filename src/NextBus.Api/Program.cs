using NextBus.Api.Exceptions;
using NextBus.Api.Extensions;
using NextBus.Api.Endpoints;
using NextBus.Application;
using NextBus.Infrastructure;
using NextBus.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddApplication()
    .AddNextBusAuth(builder.Configuration)
    .AddNextBusRateLimit(builder.Configuration)
    .AddNextBusSwagger()
    .AddExceptionHandler<GlobalExceptionHandler>()
    .AddProblemDetails();

var app = builder.Build();
await BancoInicial.GarantirAsync(app.Services);

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NextBus");
    c.RoutePrefix = "swagger";
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapNextBusHealth();
ApiEndpoints.Mapear(app);
app.Run();

public partial class Program;
