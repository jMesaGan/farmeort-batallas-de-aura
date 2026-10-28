namespace FarmeORT.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Surname { get; set; } = "";
    public string Email { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    public string Role { get; set; } = "Jugador";
    public int Elo { get; set; } = 1200;
    // Esta demo no permite declarar skills; todos los jugadores comienzan sin ellas.
}
public class Session
{
    public string TokenHash { get; set; } = "";
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
}
public class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Copa de demostración";
    public Guid OrganizerId { get; set; }
    public User Organizer { get; set; } = null!;
    public string Status { get; set; } = "Abierto";
    public int Capacity { get; set; } = 2;
    public bool IsDemo { get; set; } = true;
    public List<Registration> Registrations { get; set; } = [];
    public List<Round> Rounds { get; set; } = [];
}
public class Registration
{
    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public Guid PlayerId { get; set; }
    public User Player { get; set; } = null!;
    public string Status { get; set; } = "Aceptada";
}
public class Round
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public int Number { get; set; } = 1;
    public Battle Battle { get; set; } = null!;
}
public class Battle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoundId { get; set; }
    public Round Round { get; set; } = null!;
    public Guid PlayerOneId { get; set; }
    public User PlayerOne { get; set; } = null!;
    public Guid PlayerTwoId { get; set; }
    public User PlayerTwo { get; set; } = null!;
    public int PlayerOneElo { get; set; }
    public int PlayerTwoElo { get; set; }
    public Guid StartingPlayerId { get; set; }
    public string Status { get; set; } = "Pendiente";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
