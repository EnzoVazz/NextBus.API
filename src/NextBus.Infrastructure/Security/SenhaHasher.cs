using Microsoft.AspNetCore.Identity;
using NextBus.Application.Interfaces;

namespace NextBus.Infrastructure.Security;

public sealed class SenhaHasher : ISenhaHasher
{
    private readonly PasswordHasher<string> _hasher = new();

    public string GerarHash(string senha) => _hasher.HashPassword("nextbus", senha);

    public bool Conferir(string hash, string senha) =>
        _hasher.VerifyHashedPassword("nextbus", hash, senha) != PasswordVerificationResult.Failed;
}
