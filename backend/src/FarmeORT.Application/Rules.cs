using System.Net.Mail;
using System.Text.RegularExpressions;
using FarmeORT.Domain;
namespace FarmeORT.Application;

public static class Rules
{
    public static void Validate(RegisterRequest r, DateOnly today)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 100) errors.Add("El nombre es obligatorio (máximo 100 caracteres).");
        if (r.Surname is null || r.Surname.Trim().Length is < 3 or > 25) errors.Add("El apellido debe tener entre 3 y 25 caracteres.");
        if (string.IsNullOrWhiteSpace(r.Email) || r.Email.Length > 254 || !MailAddress.TryCreate(r.Email.Trim(), out var mail) || mail.Address != r.Email.Trim()) errors.Add("El email no es válido.");
        if (!Regex.IsMatch(r.Nickname ?? "", @"^[a-zA-Z0-9]{3,20}$")) errors.Add("El nickname debe ser alfanumérico, sin espacios, de 3 a 20 caracteres.");
        var p = r.Password ?? "";
        if (p.Length is < 15 or > 25 || !p.Any(char.IsUpper) || !p.Any(char.IsLower) || !p.Any(char.IsDigit) || !p.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))) errors.Add("La contraseña debe tener de 15 a 25 caracteres, mayúscula, minúscula, número y símbolo.");
        if (r.BirthDate == default || r.BirthDate > today.AddYears(-13)) errors.Add("Debés tener al menos 13 años.");
        if (errors.Count > 0) throw new AppException(400, string.Join(" ", errors));
    }
    public static Battle Generate(Tournament t, User actor, Func<int, int> choose)
    {
        if (actor.Role != "Organizador" || actor.Id != t.OrganizerId) throw new AppException(403, "Solo el organizador del torneo puede generar la ronda.");
        if (t.Rounds.Count > 0) return t.Rounds.Single().Battle;
        var players = t.Registrations.Where(r => r.Status == "Aceptada").Select(r => r.Player).OrderBy(p => p.Id).ToArray();
        if (t.Status != "Abierto" || players.Length != 2 || players[0].Id == players[1].Id) throw new AppException(409, "Se requieren dos jugadores diferentes con inscripciones aceptadas y un torneo abierto.");
        var a = players[0]; var b = players[1];
        var start = a.Elo == b.Elo ? (choose(2) == 0 ? a : b) : (a.Elo > b.Elo ? a : b);
        var round = new Round { Tournament = t, TournamentId = t.Id };
        var battle = new Battle { Round = round, RoundId = round.Id, PlayerOne = a, PlayerOneId = a.Id, PlayerTwo = b, PlayerTwoId = b.Id, PlayerOneElo = a.Elo, PlayerTwoElo = b.Elo, StartingPlayerId = start.Id };
        round.Battle = battle; t.Rounds.Add(round); t.Status = "En curso";
        return battle;
    }
}
