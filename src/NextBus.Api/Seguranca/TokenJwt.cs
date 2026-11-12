using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NextBus.Application.DTOs;

namespace NextBus.Api.Seguranca;

public sealed class TokenJwt(IConfiguration config)
{
    public string Emitir(UsuarioDto usuario)
    {
        var chave = config["Jwt:Chave"] ?? throw new InvalidOperationException("Defina Jwt:Chave com pelo menos 32 caracteres.");
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Emissor"],
            audience: config["Jwt:Audiencia"],
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim(ClaimTypes.Role, usuario.Papel)
            ],
            expires: DateTime.UtcNow.AddHours(int.TryParse(config["Jwt:Horas"], out var horas) ? horas : 8),
            signingCredentials: credenciais);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static long UsuarioId(ClaimsPrincipal usuario) =>
        long.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
