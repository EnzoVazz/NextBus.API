using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NextBus.Infrastructure.Persistence;

namespace NextBus.Api.Tests;

[CollectionDefinition("Oracle", DisableParallelization = true)]
public sealed class OracleCollection;

public class ApiFactory : WebApplicationFactory<Program>
{
    public int Limite { get; init; } = 1000;

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimit:Limite", Limite.ToString());
        builder.UseEnvironment("Testing");
    }
}

[Collection("Oracle")]
public class ApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_responde_e_olha_banco_e_olho_vivo()
    {
        var client = _factory.CreateClient();
        var resposta = await client.GetAsync("/health");
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("banco", corpo);
        Assert.Contains("olhovivo", corpo);
        Assert.Contains("Degraded", corpo);
    }

    [Fact]
    public async Task Login_e_eu_exigem_sessao()
    {
        var anonimo = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/auth/eu")).StatusCode);

        var client = await Entrar("marina.costa@email.com", "Marina#2026");
        var eu = await client.GetFromJsonAsync<JsonElement>("/auth/eu");
        Assert.Equal("CIDADAO", eu.GetProperty("papel").GetString());
    }

    [Fact]
    public async Task Registrar_valida_e_entra()
    {
        var client = _factory.CreateClient();
        var invalido = await client.PostAsJsonAsync("/auth/registrar", new { nome = "", email = "sem-arroba", senha = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        var erro = await invalido.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, erro.GetProperty("status").GetInt32());
        Assert.True(erro.GetProperty("erros").GetArrayLength() >= 2);

        var email = $"pessoa-{Guid.NewGuid():N}@nextbus.local";
        try
        {
            var criado = await client.PostAsJsonAsync("/auth/registrar", new { nome = "Pessoa Nova", email, senha = "Senha#2026" });
            Assert.Equal(HttpStatusCode.Created, criado.StatusCode);

            var autenticado = await Entrar(email, "Senha#2026");
            Assert.Equal(HttpStatusCode.OK, (await autenticado.GetAsync("/auth/eu")).StatusCode);
        }
        finally
        {
            await ApagarUsuario(email);
        }
    }

    [Fact]
    public async Task Rotina_e_idempotente_e_so_o_dono_enxerga()
    {
        var email = $"rotina-{Guid.NewGuid():N}@nextbus.local";
        var outroEmail = $"outro-{Guid.NewGuid():N}@nextbus.local";
        try
        {
            await Registrar(email, "Rotina Teste");
            var cidadao = await Entrar(email, "Senha#2026");
            var corpo = new
            {
                codigoLinha = 1989,
                letreiro = "7021-10",
                sentido = 1,
                codigoParada = 4200953,
                nomeParada = "Roberto Selmi Dei",
                diasSemana = new[] { "SEG", "QUA" },
                horario = "03:33"
            };

            var semChave = await cidadao.PostAsJsonAsync("/rotinas", corpo);
            Assert.Equal(HttpStatusCode.BadRequest, semChave.StatusCode);

            var chave = Guid.NewGuid().ToString();
            var criada = await PostRotina(cidadao, corpo, chave);
            Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
            var id = (await criada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

            var repetida = await PostRotina(cidadao, corpo, chave);
            Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
            Assert.Equal(id, (await repetida.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64());

            var lista = await cidadao.GetFromJsonAsync<JsonElement>("/rotinas");
            Assert.Contains(lista.EnumerateArray(), item => item.GetProperty("id").GetInt64() == id);

            var gestor = await Entrar("ricardo.nunes@email.com", "Ricardo#2026");
            Assert.Equal(HttpStatusCode.Forbidden, (await gestor.GetAsync($"/rotinas/{id}")).StatusCode);

            await Registrar(outroEmail, "Outra");
            var segunda = await Entrar(outroEmail, "Senha#2026");
            Assert.Equal(HttpStatusCode.NotFound, (await segunda.GetAsync($"/rotinas/{id}")).StatusCode);

            var desligada = await cidadao.PutAsJsonAsync($"/rotinas/{id}", new
            {
                corpo.codigoLinha,
                corpo.letreiro,
                corpo.sentido,
                corpo.codigoParada,
                corpo.nomeParada,
                corpo.diasSemana,
                corpo.horario,
                ativa = false
            });
            Assert.Equal(HttpStatusCode.OK, desligada.StatusCode);
            Assert.False((await desligada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ativa").GetBoolean());

            Assert.Equal(HttpStatusCode.NoContent, (await cidadao.DeleteAsync($"/rotinas/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await cidadao.GetAsync($"/rotinas/{id}")).StatusCode);
        }
        finally
        {
            await ApagarUsuario(email);
            await ApagarUsuario(outroEmail);
        }
    }

    [Fact]
    public async Task Aviso_do_dia_sai_uma_vez_com_o_onibus_da_feed()
    {
        var email = $"aviso-{Guid.NewGuid():N}@nextbus.local";
        try
        {
            await Registrar(email, "Aviso Teste");
            var cidadao = await Entrar(email, "Senha#2026");
            var horario = DateTimeOffset.Now.AddMinutes(15).ToString("HH:mm");
            var criada = await PostRotina(cidadao, new
            {
                codigoLinha = 1989,
                letreiro = "7021-10",
                sentido = 1,
                codigoParada = 4200953,
                nomeParada = "Roberto Selmi Dei",
                diasSemana = new[] { "SEG", "TER", "QUA", "QUI", "SEX", "SAB", "DOM" },
                horario
            }, Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.Created, criada.StatusCode);

            var primeira = await cidadao.GetFromJsonAsync<JsonElement>("/avisos");
            var avisos = primeira.EnumerateArray().ToList();
            Assert.Single(avisos);
            Assert.Equal("7021-10", avisos[0].GetProperty("letreiro").GetString());
            Assert.Equal("Roberto Selmi Dei", avisos[0].GetProperty("ponto").GetString());
            Assert.Equal("74558", avisos[0].GetProperty("veiculo").GetString());
            Assert.Equal(12, avisos[0].GetProperty("minutos").GetInt32());
            Assert.Equal("ENVIADO", avisos[0].GetProperty("status").GetString());
            Assert.Contains("Comunicado:", avisos[0].GetProperty("texto").GetString());
            var id = avisos[0].GetProperty("id").GetInt64();

            var segunda = await cidadao.GetFromJsonAsync<JsonElement>("/avisos");
            Assert.Equal(id, segunda.EnumerateArray().Single().GetProperty("id").GetInt64());
            Assert.Equal("ENVIADO", segunda.EnumerateArray().Single().GetProperty("status").GetString());
        }
        finally
        {
            await ApagarUsuario(email);
        }
    }

    [Fact]
    public async Task Planejar_recomenda_a_linha_direta_e_rejeita_endereco_desconhecido()
    {
        var cidadao = await Entrar("marina.costa@email.com", "Marina#2026");
        var ruim = await cidadao.PostAsJsonAsync("/viagens/planejar", new
        {
            origem = new { lat = -23.675901, lng = -46.752812 },
            destino = "lugar que não existe no recorte"
        });
        Assert.Equal(HttpStatusCode.BadRequest, ruim.StatusCode);

        var resposta = await cidadao.PostAsJsonAsync("/viagens/planejar", new
        {
            origem = new { lat = -23.675901, lng = -46.752812 },
            destino = "Jardim Maracá"
        });
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var opcoes = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("opcoes");
        Assert.Equal(1, opcoes.GetArrayLength());
        Assert.Equal("7021-10", opcoes[0].GetProperty("linha").GetProperty("letreiro").GetString());
        Assert.Equal(12, opcoes[0].GetProperty("minutos").GetInt32());
        Assert.True(opcoes[0].GetProperty("tracado").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Gestor_ve_linha_sem_gps_e_cidadao_nao_entra_no_painel()
    {
        var cidadao = await Entrar("marina.costa@email.com", "Marina#2026");
        Assert.Equal(HttpStatusCode.Forbidden, (await cidadao.GetAsync("/operacao/linhas-sem-gps")).StatusCode);

        var gestor = await Entrar("ricardo.nunes@email.com", "Ricardo#2026");
        var linhas = await gestor.GetFromJsonAsync<JsonElement>("/operacao/linhas-sem-gps");
        Assert.Contains(linhas.EnumerateArray(), l => l.GetProperty("letreiro").GetString() == "8000-10");
        Assert.DoesNotContain(linhas.EnumerateArray(), l => l.GetProperty("letreiro").GetString() == "7021-10");

        var busca = await gestor.GetFromJsonAsync<JsonElement>("/linhas?q=7021");
        Assert.Equal("7021-10", busca.EnumerateArray().Single().GetProperty("letreiro").GetString());
    }

    [Fact]
    public async Task Swagger_fica_publico()
    {
        var client = _factory.CreateClient();
        var resposta = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("/rotinas", json);
        Assert.Contains("/auth/login", json);
    }

    private async Task<HttpClient> Entrar(string email, string senha)
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync("/auth/login", new { email, senha });
        resposta.EnsureSuccessStatusCode();
        var token = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task Registrar(string email, string nome)
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync("/auth/registrar", new { nome, email, senha = "Senha#2026" });
        resposta.EnsureSuccessStatusCode();
    }

    private async Task ApagarUsuario(string email)
    {
        if (!email.EndsWith("@nextbus.local", StringComparison.OrdinalIgnoreCase))
            return;

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NextBusDb>();
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Email == email);
        if (usuario is null || usuario.Id < 100)
            return;

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM AVISO WHERE ID_ROTINA IN (SELECT ID_ROTINA FROM ROTINA WHERE ID_USUARIO = {usuario.Id})");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM IDEMPOTENCIA WHERE ID_USUARIO = {usuario.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ROTINA WHERE ID_USUARIO = {usuario.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM SENHA WHERE ID_USUARIO = {usuario.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM USUARIO WHERE ID_USUARIO = {usuario.Id} AND EMAIL = {email}");
    }

    private static Task<HttpResponseMessage> PostRotina(HttpClient client, object corpo, string chave)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/rotinas")
        {
            Content = JsonContent.Create(corpo)
        };
        pedido.Headers.Add("Idempotency-Key", chave);
        return client.SendAsync(pedido);
    }
}

public class RateLimitFactory : ApiFactory
{
    public RateLimitFactory() => Limite = 2;
}

[Collection("Oracle")]
public class RateLimitTests : IClassFixture<RateLimitFactory>
{
    private readonly RateLimitFactory _factory;

    public RateLimitTests(RateLimitFactory factory) => _factory = factory;

    [Fact]
    public async Task Leitura_passa_de_429_quando_estoura_o_limite()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login", new { email = "ricardo.nunes@email.com", senha = "Ricardo#2026" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/linhas?q=8000")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/linhas?q=8000")).StatusCode);
        var bloqueio = await client.GetAsync("/linhas?q=8000");
        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueio.StatusCode);
        Assert.Equal(429, (await bloqueio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
    }
}
