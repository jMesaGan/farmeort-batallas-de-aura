using FarmeORT.Application;
using FarmeORT.Domain;
using FarmeORT.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace FarmeORT.Tests;

public class SqlFactAttribute : FactAttribute { public SqlFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FARMEORT_TEST_SQL"))) Skip = "Requiere FARMEORT_TEST_SQL y permiso CREATE DATABASE en SQL Server Express 2025."; } }
public class SqlIntegrationTests
{
    [SqlFact]
    public async Task MigrationSeedConcurrencyRestartUniquenessAndReset()
    {
        var cs = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FARMEORT_TEST_SQL")) { InitialCatalog = "FarmeORT_Test_" + Guid.NewGuid().ToString("N") };
        GameDb Create() => new(new DbContextOptionsBuilder<GameDb>().UseSqlServer(cs.ConnectionString).Options);
        var passwords = new Passwords(); const string password = "PasswordDemo123!"; Guid playerId; Guid battleId;
        await using var setup = Create();
        try
        {
            await setup.Database.MigrateAsync(); await DemoSeed.Run(setup, passwords, password, password, false); await DemoSeed.Run(setup, passwords, password, password, false); Assert.Equal(2, await setup.Users.CountAsync());
            var service = new GameService(new SqlStore(setup), passwords, TimeProvider.System, 120); var player = await service.Register(RulesTests.Valid()); playerId = player.Id; await service.Join(DemoSeed.TournamentId, (await setup.Users.FindAsync(playerId))!);
            async Task<BattleDto> Generate() { await using var db = Create(); var actor = (await db.Users.FindAsync(DemoSeed.OrganizerId))!; return await new GameService(new SqlStore(db), passwords, TimeProvider.System, 120).Generate(DemoSeed.TournamentId, actor); }
            var results = await Task.WhenAll(Generate(), Generate(), Generate()); battleId = results[0].Id; Assert.All(results, b => Assert.Equal(battleId, b.Id));
            await using (var restarted = Create())
            {
                Assert.Equal(1, await restarted.Battles.CountAsync()); var b = await new SqlStore(restarted).GetBattle(battleId); Assert.NotNull(b); Assert.Equal("En curso", b!.Round.Tournament.Status);
                restarted.Users.Add(new User { Name = "Otra", Surname = "Persona", Email = "ANA@EXAMPLE.COM", Nickname = "Another", PasswordHash = "irrelevant" }); await Assert.ThrowsAsync<DbUpdateException>(() => restarted.SaveChangesAsync());
            }
            // Una excepción dentro de la transacción no deja escrituras parciales.
            await using (var rollback = Create())
            {
                await Assert.ThrowsAsync<AppException>(() => new SqlStore(rollback).InTournament<int>(DemoSeed.TournamentId, t => { t.Status = "Corrupto"; rollback.SaveChanges(); throw new AppException(409, "test"); }));
            }
            await using (var check = Create()) { Assert.Equal("En curso", (await check.Tournaments.FindAsync(DemoSeed.TournamentId))!.Status); }
            await using (var reset = Create())
            {
                await DemoSeed.Run(reset, passwords, password, password, true); Assert.Empty(await reset.Battles.ToListAsync()); Assert.True(await reset.Users.AnyAsync(u => u.Id == playerId)); Assert.Single((await new SqlStore(reset).GetDemo())!.Registrations);
            }
        }
        finally { await setup.Database.EnsureDeletedAsync(); }
    }
}
