using NextBus.Application.DTOs;
using NextBus.Application.Interfaces;
using NextBus.Domain.Entities;
using NextBus.Domain.Exceptions;

namespace NextBus.Application.Services;

public sealed class AuthAppService(
    IUsuarioRepository usuarios,
    ISenhaRepository senhasArmazenadas,
    ISenhaHasher senhas,
    IUnitOfWork unidade) : IAuthAppService
{
    public async Task<UsuarioDto> RegistrarAsync(string nome, string email, string senha, CancellationToken ct)
    {
        var erros = new List<ErroCampo>();
        if (string.IsNullOrWhiteSpace(nome))
            erros.Add(new("nome", "Informe o nome."));
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            erros.Add(new("email", "Informe um e-mail válido."));
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < 6)
            erros.Add(new("senha", "A senha precisa ter ao menos 6 caracteres."));
        if (erros.Count > 0)
            throw new ValidationException(erros);

        var normalizado = email.Trim().ToLowerInvariant();
        if (await usuarios.EmailExisteAsync(normalizado, ct))
            throw new ConflictException("Este e-mail já está cadastrado.", "email");

        var usuario = Usuario.Criar(nome, normalizado, "CIDADAO");
        await usuarios.AdicionarAsync(usuario, ct);
        await unidade.SalvarAsync(ct);
        await senhasArmazenadas.SalvarAsync(usuario.Id, senhas.GerarHash(senha), ct);
        return Mapa(usuario);
    }

    public async Task<UsuarioDto?> AutenticarAsync(string? email, string? senha, CancellationToken ct)
    {
        var normalizado = (email ?? "").Trim().ToLowerInvariant();
        var usuario = await usuarios.ObterPorEmailAsync(normalizado, ct);
        if (usuario is null)
            return null;

        var hash = await senhasArmazenadas.ObterHashAsync(usuario.Id, ct);
        if (hash is null || !senhas.Conferir(hash, senha ?? ""))
            return null;

        return Mapa(usuario);
    }

    public async Task<UsuarioDto> ObterAsync(long id, CancellationToken ct)
    {
        var usuario = await usuarios.ObterPorIdAsync(id, ct);
        if (usuario is null)
            throw new ResourceNotFoundException("Usuário não encontrado.");

        return Mapa(usuario);
    }

    private static UsuarioDto Mapa(Usuario usuario) => new(usuario.Id, usuario.Nome, usuario.Email, usuario.Papel);
}
