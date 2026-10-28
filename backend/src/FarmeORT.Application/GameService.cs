using System.Security.Cryptography;
using System.Text;
using FarmeORT.Domain;
namespace FarmeORT.Application;

public class GameService(IStore store, IPasswords passwords, TimeProvider clock, int sessionMinutes)
{
    public static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public async Task<UserDto> Register(RegisterRequest request)
    {
        Rules.Validate(request, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        var email = request.Email.Trim().ToLowerInvariant(); var nick = request.Nickname.ToLowerInvariant();
        if (await store.UserExists(email, nick)) throw new AppException(409, "El email o nickname ya está en uso.");
        var user = new User { Name = request.Name.Trim(), Surname = request.Surname.Trim(), Email = email, Nickname = request.Nickname, BirthDate = request.BirthDate };
        user.PasswordHash = passwords.Hash(user, request.Password); await store.AddUser(user); return user.Dto();
    }
    public async Task<LoginDto> Login(LoginRequest request)
    {
        var user = await store.FindUser((request.Email ?? "").Trim().ToLowerInvariant());
        // Verificar también cuando el email no existe evita un atajo de tiempo evidente.
        var candidate = user ?? new User { PasswordHash = DummyHash.Value };
        if (!passwords.Verify(candidate, request.Password ?? "") || user is null) throw new AppException(401, "Email o contraseña incorrectos.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var expires = clock.GetUtcNow().AddMinutes(sessionMinutes);
        await store.AddSession(new Session { TokenHash = Digest(token), UserId = user.Id, ExpiresAt = expires }); return new(token, expires, user.Dto());
    }
    // Se inicializa con el mismo algoritmo/costo que las contraseñas reales.
    public static Lazy<string> DummyHash { get; set; } = new(() => "");
    public async Task<User?> Authenticate(string token) { if (token.Length != 64) return null; var s = await store.FindSession(Digest(token)); return s is not null && s.ExpiresAt > clock.GetUtcNow() ? s.User : null; }
    public Task Logout(string token) => store.RemoveSession(Digest(token));
    public async Task<TournamentDto> Demo() => (await store.GetDemo() ?? throw new AppException(404, "No hay un torneo de demostración configurado.")).Dto();
    public Task<TournamentDto> Join(Guid id, User actor) => store.InTournament(id, t =>
    {
        if (actor.Role != "Jugador") throw new AppException(403, "Solo los jugadores pueden inscribirse.");
        if (t.Registrations.Any(r => r.PlayerId == actor.Id)) return t.Dto();
        if (t.Status != "Abierto" || t.Registrations.Count(r => r.Status == "Aceptada") >= t.Capacity) throw new AppException(409, "El torneo está cerrado o completo.");
        t.Registrations.Add(new Registration { TournamentId = t.Id, PlayerId = actor.Id, Player = actor }); return t.Dto();
    });
    public Task<BattleDto> Generate(Guid id, User actor) => store.InTournament(id, t => Rules.Generate(t, actor, RandomNumberGenerator.GetInt32).Dto());
    public async Task<BattleDto> Battle(Guid id, User actor)
    {
        var b = await store.GetBattle(id) ?? throw new AppException(404, "Batalla no encontrada.");
        if (actor.Id != b.Round.Tournament.OrganizerId && actor.Id != b.PlayerOneId && actor.Id != b.PlayerTwoId) throw new AppException(403, "No tenés permiso para ver esta batalla.");
        return b.Dto();
    }
}
