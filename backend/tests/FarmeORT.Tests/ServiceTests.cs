using FarmeORT.Application;
using FarmeORT.Domain;
using FarmeORT.Infrastructure;
using Xunit;
namespace FarmeORT.Tests;
// Dobles de prueba de los puertos; la aplicación real siempre usa SqlStore.
public class StubStore : IStore
{
    public List<User> Users = []; public List<Session> Sessions = []; public Tournament? Tournament; public Battle? BattleValue;
    public Task<User?> FindUser(string email) => Task.FromResult(Users.SingleOrDefault(u => u.Email == email));
    public Task<bool> UserExists(string email, string nickname) => Task.FromResult(Users.Any(u => u.Email == email || u.Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase)));
    public Task AddUser(User u) { Users.Add(u); return Task.CompletedTask; }
    public Task AddSession(Session s) { s.User = Users.Single(u => u.Id == s.UserId); Sessions.Add(s); return Task.CompletedTask; }
    public Task<Session?> FindSession(string hash) => Task.FromResult(Sessions.SingleOrDefault(s => s.TokenHash == hash));
    public Task RemoveSession(string hash) { Sessions.RemoveAll(s => s.TokenHash == hash); return Task.CompletedTask; }
    public Task<Tournament?> GetDemo() => Task.FromResult(Tournament);
    public Task<Battle?> GetBattle(Guid id) => Task.FromResult(BattleValue);
    public Task<T> InTournament<T>(Guid id, Func<Tournament, T> action) => Task.FromResult(action(Tournament!));
}
public class TestClock : TimeProvider { public DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public class ServiceTests
{
    readonly StubStore store = new(); readonly Passwords passwords = new(); readonly TestClock clock = new();
    GameService Game => new(store, passwords, clock, 120);
    [Fact]
    public async Task RegistrationDefaultsHashAndCaseInsensitiveUniqueness()
    {
        var r = RulesTests.Valid(); var dto = await Game.Register(r with { Email = " ANA@example.com " }); var user = Assert.Single(store.Users); Assert.Equal("Jugador", dto.Role); Assert.Equal(1200, dto.Elo); Assert.NotEqual(Guid.Empty, dto.Id); Assert.Equal("ana@example.com", user.Email); Assert.NotEqual(r.Password, user.PasswordHash); Assert.True(passwords.Verify(user, r.Password)); Assert.False(passwords.Verify(user, "wrong")); Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(() => Game.Register(r with { Email = "other@example.com", Nickname = r.Nickname.ToUpperInvariant() }))).Status);
    }
    [Fact]
    public async Task LoginTokenIsHashedAndLogoutRevokesIt()
    {
        var r = RulesTests.Valid(); await Game.Register(r); var s = await Game.Login(new(r.Email, r.Password)); Assert.Equal(64, s.Token.Length); Assert.NotEqual(s.Token, Assert.Single(store.Sessions).TokenHash); Assert.NotNull(await Game.Authenticate(s.Token)); await Game.Logout(s.Token); Assert.Null(await Game.Authenticate(s.Token));
    }
    [Fact]
    public async Task LoginErrorsDoNotRevealWhetherEmailExists()
    {
        var r = RulesTests.Valid(); await Game.Register(r); GameService.DummyHash = new(() => passwords.Hash(new User(), "DummyPassword123!"));
        var a = await Assert.ThrowsAsync<AppException>(() => Game.Login(new(r.Email, "wrong"))); var b = await Assert.ThrowsAsync<AppException>(() => Game.Login(new("unknown@example.com", "wrong"))); Assert.Equal(401, a.Status); Assert.Equal(a.Message, b.Message);
    }
    [Fact] public async Task ExpiredSessionRejected() { var r = RulesTests.Valid(); await Game.Register(r); var s = await Game.Login(new(r.Email, r.Password)); clock.Now = clock.Now.AddHours(3); Assert.Null(await Game.Authenticate(s.Token)); }
    [Fact]
    public async Task JoinEnforcesRoleCapacityAndRetryIsIdempotent()
    {
        var (t, o) = RulesTests.Arena(); t.Registrations.RemoveAt(1); store.Tournament = t; var player = new User(); await Game.Join(t.Id, player); await Game.Join(t.Id, player); Assert.Equal(2, t.Registrations.Count); Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(() => Game.Join(t.Id, o))).Status); Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(() => Game.Join(t.Id, new User()))).Status);
    }
    [Fact]
    public async Task BattleDetailOnlyForParticipantsOrOwner()
    {
        var (t, o) = RulesTests.Arena(); store.BattleValue = Rules.Generate(t, o, _ => 0); await Game.Battle(store.BattleValue.Id, o); await Game.Battle(store.BattleValue.Id, store.BattleValue.PlayerOne); Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(() => Game.Battle(store.BattleValue.Id, new User { Role = "Organizador" }))).Status);
    }
}
