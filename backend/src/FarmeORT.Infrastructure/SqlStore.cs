using System.Data;
using FarmeORT.Application;
using FarmeORT.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
namespace FarmeORT.Infrastructure;

public class SqlStore(GameDb db) : IStore
{
    public Task<User?> FindUser(string email) => db.Users.SingleOrDefaultAsync(u => u.Email == email);
    public Task<bool> UserExists(string email, string nickname) => db.Users.AnyAsync(u => u.Email == email || u.Nickname == nickname);
    public async Task AddUser(User user) { db.Users.Add(user); try { await db.SaveChangesAsync(); } catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 }) { throw new AppException(409, "El email o nickname ya está en uso."); } }
    public async Task AddSession(Session session) { db.Sessions.Add(session); await db.SaveChangesAsync(); }
    public Task<Session?> FindSession(string hash) => db.Sessions.Include(s => s.User).SingleOrDefaultAsync(s => s.TokenHash == hash);
    public async Task RemoveSession(string hash) => await db.Sessions.Where(s => s.TokenHash == hash).ExecuteDeleteAsync();
    private IQueryable<Tournament> Complete() => db.Tournaments.Include(t => t.Registrations).ThenInclude(r => r.Player).Include(t => t.Rounds).ThenInclude(r => r.Battle).ThenInclude(b => b.PlayerOne).Include(t => t.Rounds).ThenInclude(r => r.Battle).ThenInclude(b => b.PlayerTwo).AsSplitQuery();
    public Task<Tournament?> GetDemo() => Complete().AsNoTracking().SingleOrDefaultAsync(t => t.Id == DemoSeed.TournamentId && t.IsDemo);
    public Task<Battle?> GetBattle(Guid id) => db.Battles.AsNoTracking().Include(b => b.Round).ThenInclude(r => r.Tournament).Include(b => b.PlayerOne).Include(b => b.PlayerTwo).SingleOrDefaultAsync(b => b.Id == id);
    public async Task<T> InTournament<T>(Guid id, Func<Tournament, T> action)
    {
        // sp_getapplock serializa inscripciones/generación para este torneo entre instancias.
        // Reintentos del cliente reciben la misma batalla. No hay retries automáticos de EF.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var resource = "farmeort:tournament:" + id;
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @r < 0 THROW 51000, 'Tournament lock timeout', 1;");
        var tournament = await Complete().SingleOrDefaultAsync(t => t.Id == id) ?? throw new AppException(404, "Torneo no encontrado.");
        var result = action(tournament); await db.SaveChangesAsync(); await tx.CommitAsync(); return result;
    }
}
