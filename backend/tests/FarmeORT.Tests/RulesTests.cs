using FarmeORT.Application;
using FarmeORT.Domain;
using Xunit;
namespace FarmeORT.Tests;

public class RulesTests
{
    static readonly DateOnly Today = new(2026, 10, 27);
    public static RegisterRequest Valid() => new("Ana", "Pérez", "ana@example.com", "Ana123", "PasswordDemo123!", new(2000, 1, 1));
    [Fact] public void ValidRegistrationAndExactlyThirteenAreAccepted() { Rules.Validate(Valid(), Today); Rules.Validate(Valid() with { BirthDate = Today.AddYears(-13) }, Today); Rules.Validate(Valid() with { Name = "A" }, Today); }
    [Theory]
    [InlineData("short")]
    [InlineData("abcdefghijklmn1!")]
    [InlineData("ABCDEFGHIJKLMN1!")]
    [InlineData("Abcdefghijklmn!!")]
    [InlineData("Abcdefghijklmn12")]
    [InlineData("PasswordDemo123!12345678901234567890")]
    public void RejectsWeakPasswords(string password) => Assert.Equal(400, Assert.Throws<AppException>(() => Rules.Validate(Valid() with { Password = password }, Today)).Status);
    [Theory]
    [InlineData("ab")]
    [InlineData("with space")]
    [InlineData("ñandu")]
    [InlineData("123456789012345678901")]
    public void RejectsNicknames(string nickname) => Assert.Throws<AppException>(() => Rules.Validate(Valid() with { Nickname = nickname }, Today));
    [Fact]
    public void RejectsInvalidFields()
    {
        Assert.Throws<AppException>(() => Rules.Validate(Valid() with { Name = " " }, Today));
        Assert.Throws<AppException>(() => Rules.Validate(Valid() with { Surname = "ab" }, Today));
        Assert.Throws<AppException>(() => Rules.Validate(Valid() with { Surname = new string('a', 26) }, Today));
        Assert.Throws<AppException>(() => Rules.Validate(Valid() with { Email = "invalid" }, Today));
        Assert.Throws<AppException>(() => Rules.Validate(Valid() with { BirthDate = Today.AddYears(-13).AddDays(1) }, Today));
    }
    public static (Tournament, User) Arena()
    {
        var organizer = new User { Role = "Organizador" }; var a = new User { Nickname = "PlayerOne", Elo = 1200 }; var b = new User { Nickname = "PlayerTwo", Elo = 1400 };
        var t = new Tournament { Organizer = organizer, OrganizerId = organizer.Id, Registrations = [new() { Player = a, PlayerId = a.Id }, new() { Player = b, PlayerId = b.Id }] }; return (t, organizer);
    }
    [Fact]
    public void HigherEloStartsAndSnapshotAndRelationsPersistInAggregate()
    {
        var (t, o) = Arena(); var b = Rules.Generate(t, o, _ => throw new Exception("No random expected"));
        Assert.Equal(1400, b.PlayerTwoElo > b.PlayerOneElo ? b.PlayerTwoElo : b.PlayerOneElo); Assert.Equal(t.Registrations.Single(r => r.Player.Elo == 1400).PlayerId, b.StartingPlayerId); Assert.Equal("En curso", t.Status); Assert.Equal("Pendiente", b.Status); Assert.Same(t, b.Round.Tournament); Assert.Equal(b.Round.Id, b.RoundId); Assert.NotEqual(b.PlayerOneId, b.PlayerTwoId);
        b.PlayerOne.Elo = 2000; Assert.Equal(1200, b.PlayerOneElo == 1200 ? b.PlayerOneElo : b.PlayerTwoElo);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TieUsesRandomOnceAndRetriesReturnSameBattle(int selected)
    {
        var (t, o) = Arena(); t.Registrations.ForEach(r => r.Player.Elo = 1200); var calls = 0; var b = Rules.Generate(t, o, _ => { calls++; return selected; }); Assert.Equal(selected == 0 ? b.PlayerOneId : b.PlayerTwoId, b.StartingPlayerId); Assert.Same(b, Rules.Generate(t, o, _ => throw new Exception())); Assert.Equal(1, calls); Assert.Single(t.Rounds);
    }
    [Fact]
    public void OnlyOwnerWithOrganizerRoleCanGenerate()
    {
        var (t, o) = Arena(); Assert.Equal(403, Assert.Throws<AppException>(() => Rules.Generate(t, new User { Role = "Organizador" }, _ => 0)).Status); o.Role = "Jugador"; Assert.Throws<AppException>(() => Rules.Generate(t, o, _ => 0)); Assert.Empty(t.Rounds);
    }
    [Fact]
    public void RequiresTwoDistinctAcceptedPlayersAndOpenTournament()
    {
        var (t, o) = Arena(); t.Registrations[1].Status = "Pendiente"; Assert.Throws<AppException>(() => Rules.Generate(t, o, _ => 0)); t.Registrations[1].Status = "Aceptada"; t.Registrations[1].Player = t.Registrations[0].Player; Assert.Throws<AppException>(() => Rules.Generate(t, o, _ => 0));
        (t, o) = Arena(); t.Status = "Cerrado"; Assert.Throws<AppException>(() => Rules.Generate(t, o, _ => 0));
    }
}
