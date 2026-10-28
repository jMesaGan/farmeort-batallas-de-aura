using FarmeORT.Domain;
namespace FarmeORT.Application;

public record RegisterRequest(string Name, string Surname, string Email, string Nickname, string Password, DateOnly BirthDate);
public record LoginRequest(string Email, string Password);
public record UserDto(Guid Id, string Name, string Nickname, string Role, int Elo);
public record LoginDto(string Token, DateTimeOffset ExpiresAt, UserDto User);
public record PlayerDto(Guid Id, string Nickname, int Elo);
public record BattleDto(Guid Id, Guid TournamentId, string Tournament, int Round, PlayerDto PlayerOne, PlayerDto PlayerTwo, string Status, Guid StartingPlayerId);
public record TournamentDto(Guid Id, string Name, string Status, int Capacity, Guid OrganizerId, PlayerDto[] Players, Guid? BattleId);
public sealed class AppException(int status, string message) : Exception(message) { public int Status { get; } = status; }
public interface IPasswords { string Hash(User user, string password); bool Verify(User user, string password); }
public interface IStore
{
    Task<User?> FindUser(string email);
    Task<bool> UserExists(string email, string nickname);
    Task AddUser(User user);
    Task AddSession(Session session);
    Task<Session?> FindSession(string hash);
    Task RemoveSession(string hash);
    Task<Tournament?> GetDemo();
    Task<Battle?> GetBattle(Guid id);
    // Implementación SQL: lock exclusivo del torneo y transacción con lectura serializada.
    Task<T> InTournament<T>(Guid id, Func<Tournament, T> action);
}
public static class Mapping
{
    public static UserDto Dto(this User u) => new(u.Id, u.Name, u.Nickname, u.Role, u.Elo);
    public static TournamentDto Dto(this Tournament t) => new(t.Id, t.Name, t.Status, t.Capacity, t.OrganizerId, t.Registrations.Where(r => r.Status == "Aceptada").Select(r => new PlayerDto(r.PlayerId, r.Player.Nickname, r.Player.Elo)).ToArray(), t.Rounds.FirstOrDefault()?.Battle?.Id);
    public static BattleDto Dto(this Battle b) => new(b.Id, b.Round.TournamentId, b.Round.Tournament.Name, b.Round.Number, new(b.PlayerOneId, b.PlayerOne.Nickname, b.PlayerOneElo), new(b.PlayerTwoId, b.PlayerTwo.Nickname, b.PlayerTwoElo), b.Status, b.StartingPlayerId);
}
