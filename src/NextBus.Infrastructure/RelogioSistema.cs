using NextBus.Application.Interfaces;

namespace NextBus.Infrastructure;

public sealed class RelogioSistema : IRelogio
{
    public DateTimeOffset Agora() => DateTimeOffset.Now;
}
