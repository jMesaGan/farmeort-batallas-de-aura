using FarmeORT.Domain;
using Microsoft.EntityFrameworkCore;
namespace FarmeORT.Infrastructure;

public class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Battle> Battles => Set<Battle>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        var u = m.Entity<User>(); u.Property(x => x.Name).HasMaxLength(100); u.Property(x => x.Surname).HasMaxLength(25); u.Property(x => x.Email).HasMaxLength(254); u.Property(x => x.Nickname).HasMaxLength(20); u.Property(x => x.PasswordHash).HasMaxLength(512); u.Property(x => x.Role).HasMaxLength(20);
        u.HasIndex(x => x.Email).IsUnique(); u.HasIndex(x => x.Nickname).IsUnique();
        // Collation explícita: email y nickname únicos sin distinguir mayúsculas.
        u.Property(x => x.Email).UseCollation("Latin1_General_100_CI_AS"); u.Property(x => x.Nickname).UseCollation("Latin1_General_100_CI_AS");
        var s = m.Entity<Session>(); s.HasKey(x => x.TokenHash); s.Property(x => x.TokenHash).HasMaxLength(64); s.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); s.HasIndex(x => x.ExpiresAt);
        var t = m.Entity<Tournament>(); t.Property(x => x.Name).HasMaxLength(100); t.Property(x => x.Status).HasMaxLength(20); t.HasOne(x => x.Organizer).WithMany().HasForeignKey(x => x.OrganizerId).OnDelete(DeleteBehavior.Restrict);
        var r = m.Entity<Registration>(); r.HasKey(x => new { x.TournamentId, x.PlayerId }); r.Property(x => x.Status).HasMaxLength(20); r.HasOne(x => x.Tournament).WithMany(x => x.Registrations).HasForeignKey(x => x.TournamentId); r.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
        var round = m.Entity<Round>(); round.HasOne(x => x.Tournament).WithMany(x => x.Rounds).HasForeignKey(x => x.TournamentId); round.HasIndex(x => new { x.TournamentId, x.Number }).IsUnique();
        var b = m.Entity<Battle>(); b.HasOne(x => x.Round).WithOne(x => x.Battle).HasForeignKey<Battle>(x => x.RoundId); b.HasIndex(x => x.RoundId).IsUnique();
        b.HasOne(x => x.PlayerOne).WithMany().HasForeignKey(x => x.PlayerOneId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.PlayerTwo).WithMany().HasForeignKey(x => x.PlayerTwoId).OnDelete(DeleteBehavior.Restrict); b.Property(x => x.Status).HasMaxLength(20);
        b.ToTable(table => { table.HasCheckConstraint("CK_Battle_DifferentPlayers", "[PlayerOneId] <> [PlayerTwoId]"); table.HasCheckConstraint("CK_Battle_StartingPlayer", "[StartingPlayerId] = [PlayerOneId] OR [StartingPlayerId] = [PlayerTwoId]"); });
    }
}
