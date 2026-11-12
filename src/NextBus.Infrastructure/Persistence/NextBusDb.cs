using Microsoft.EntityFrameworkCore;
using NextBus.Domain.Entities;

namespace NextBus.Infrastructure.Persistence;

public sealed class NextBusDb(DbContextOptions<NextBusDb> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rotina> Rotinas => Set<Rotina>();
    public DbSet<Aviso> Avisos => Set<Aviso>();
    public DbSet<SenhaGuardada> Senhas => Set<SenhaGuardada>();
    public DbSet<Idempotencia> Idempotencias => Set<Idempotencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("USUARIO");
            e.Property(u => u.Id).HasColumnName("ID_USUARIO").ValueGeneratedOnAdd();
            e.Property(u => u.Nome).HasColumnName("NOME").HasMaxLength(120);
            e.Property(u => u.Email).HasColumnName("EMAIL").HasMaxLength(160);
            e.Property(u => u.Papel).HasColumnName("PAPEL").HasMaxLength(20);
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Rotina>(e =>
        {
            e.ToTable("ROTINA");
            e.Property(r => r.Id).HasColumnName("ID_ROTINA").ValueGeneratedOnAdd();
            e.Property(r => r.UsuarioId).HasColumnName("ID_USUARIO");
            e.Property(r => r.CodigoLinha).HasColumnName("CODIGO_LINHA").HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => int.Parse(v.Trim()));
            e.Property(r => r.Letreiro).HasColumnName("LETREIRO").HasMaxLength(20);
            e.Property(r => r.Sentido).HasColumnName("SENTIDO");
            e.Property(r => r.CodigoParada).HasColumnName("CODIGO_PARADA").HasMaxLength(20)
                .HasConversion(v => v.ToString(), v => int.Parse(v.Trim()));
            e.Property(r => r.NomeParada).HasColumnName("NOME_PARADA").HasMaxLength(160);
            e.Property(r => r.DiasSemana).HasColumnName("DIAS_SEMANA").HasMaxLength(20);
            e.Property(r => r.Horario).HasColumnName("HORARIO_EMBARQUE").HasMaxLength(5)
                .HasConversion(v => v, v => v.Trim());
            e.Property(r => r.Ativa).HasColumnName("ATIVA").HasMaxLength(1)
                .HasConversion(v => v ? "S" : "N", v => v.Trim() == "S");
            e.HasOne(r => r.Usuario).WithMany(u => u.Rotinas).HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Aviso>(e =>
        {
            e.ToTable("AVISO");
            e.Property(a => a.Id).HasColumnName("ID_AVISO").ValueGeneratedOnAdd();
            e.Property(a => a.RotinaId).HasColumnName("ID_ROTINA");
            e.Property(a => a.DataReferencia).HasColumnName("DATA_AVISO").HasColumnType("DATE")
                .HasConversion(v => v.ToDateTime(TimeOnly.MinValue), v => DateOnly.FromDateTime(v));
            e.Property(a => a.Letreiro).HasColumnName("LETREIRO").HasMaxLength(20);
            e.Property(a => a.Ponto).HasColumnName("NOME_PONTO").HasMaxLength(160);
            e.Property(a => a.Veiculo).HasColumnName("IDENTIFICADOR_VEICULO").HasMaxLength(20);
            e.Property(a => a.Minutos).HasColumnName("MINUTOS");
            e.Property(a => a.IdadeGpsSegundos).HasColumnName("IDADE_GPS_SEGUNDOS");
            e.Property(a => a.Texto).HasColumnName("TEXTO").HasMaxLength(400);
            e.Property(a => a.Status).HasColumnName("STATUS").HasMaxLength(15);
            e.HasOne(a => a.Rotina).WithMany(r => r.Avisos).HasForeignKey(a => a.RotinaId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => new { a.RotinaId, a.DataReferencia }).IsUnique();
        });

        modelBuilder.Entity<SenhaGuardada>(e =>
        {
            e.ToTable("SENHA");
            e.HasKey(s => s.UsuarioId);
            e.Property(s => s.UsuarioId).HasColumnName("ID_USUARIO");
            e.Property(s => s.Hash).HasColumnName("HASH").HasMaxLength(500);
        });

        modelBuilder.Entity<Idempotencia>(e =>
        {
            e.ToTable("IDEMPOTENCIA");
            e.Property(i => i.Id).HasColumnName("ID_IDEMPOTENCIA").ValueGeneratedOnAdd().UseIdentityColumn();
            e.Property(i => i.UsuarioId).HasColumnName("ID_USUARIO");
            e.Property(i => i.Chave).HasColumnName("CHAVE").HasMaxLength(80);
            e.Property(i => i.HashCorpo).HasColumnName("HASH_CORPO").HasMaxLength(128);
            e.Property(i => i.RotinaId).HasColumnName("ID_ROTINA");
            e.HasIndex(i => new { i.UsuarioId, i.Chave }).IsUnique();
        });

        foreach (var entidade in new[] { typeof(Usuario), typeof(Rotina), typeof(Aviso) })
        {
            modelBuilder.Entity(entidade).Property("Id").UseIdentityColumn();
        }
    }
}

public sealed class SenhaGuardada
{
    public long UsuarioId { get; set; }
    public string Hash { get; set; } = "";
}
