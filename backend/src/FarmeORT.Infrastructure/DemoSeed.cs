using FarmeORT.Application;
using FarmeORT.Domain;
using Microsoft.EntityFrameworkCore;
namespace FarmeORT.Infrastructure;

public static class DemoSeed
{
    public static readonly Guid OrganizerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid RivalId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid TournamentId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static async Task Run(GameDb db, IPasswords passwords, string organizerPassword, string rivalPassword, bool reset)
    {
        Rules.Validate(new("Demo", "Organizador", "organizador@farmeort.example", "OrganizadorDemo", organizerPassword, new(1990, 1, 1)), DateOnly.FromDateTime(DateTime.UtcNow));
        Rules.Validate(new("Demo", "Rival", "rival@farmeort.example", "RivalDemo", rivalPassword, new(1990, 1, 1)), DateOnly.FromDateTime(DateTime.UtcNow));
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='farmeort:seed', @LockMode='Exclusive', @LockOwner='Transaction'; IF @r<0 THROW 51000, 'Seed lock failure', 1;");
        if (reset)
        {
            // Solo elimina el torneo conocido; conserva jugadores registrados y otros torneos.
            await db.Tournaments.Where(t => t.Id == TournamentId && t.IsDemo).ExecuteDeleteAsync();
            await db.Sessions.Where(s => s.UserId == OrganizerId || s.UserId == RivalId).ExecuteDeleteAsync();
        }
        async Task<User> Ensure(Guid id, string email, string nick, string role, string password)
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
            if (user is not null) return user;
            user = new User { Id = id, Name = "Demo", Surname = "FarmeORT", Email = email, Nickname = nick, Role = role, BirthDate = new(1990, 1, 1) };
            user.PasswordHash = passwords.Hash(user, password); db.Users.Add(user); return user;
        }
        var organizer = await Ensure(OrganizerId, "organizador@farmeort.example", "OrganizadorDemo", "Organizador", organizerPassword);
        var rival = await Ensure(RivalId, "rival@farmeort.example", "RivalDemo", "Jugador", rivalPassword);
        if (!await db.Tournaments.AnyAsync(t => t.Id == TournamentId)) db.Tournaments.Add(new Tournament { Id = TournamentId, OrganizerId = organizer.Id, Organizer = organizer, Registrations = [new Registration { PlayerId = rival.Id, Player = rival, TournamentId = TournamentId }] });
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
