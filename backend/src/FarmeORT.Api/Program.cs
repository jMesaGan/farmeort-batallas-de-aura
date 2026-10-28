using System.Threading.RateLimiting;
using FarmeORT.Api;
using FarmeORT.Application;
using FarmeORT.Domain;
using FarmeORT.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
var reset = args.Contains("--reset-demo");
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--reset-demo").ToArray());
var connection = builder.Configuration.GetConnectionString("SqlServer") ?? throw new InvalidOperationException("Configurá ConnectionStrings__SqlServer.");
if (!builder.Environment.IsDevelopment())
{
    var cs = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
    var host = cs.DataSource.Replace("tcp:", "", StringComparison.OrdinalIgnoreCase).Split(',')[0].Split('\\')[0];
    var localDemo = builder.Environment.IsEnvironment("Demo") && builder.Configuration.GetValue<bool>("Demo:AllowLocalUntrustedCertificate") && new[] { "localhost", "127.0.0.1", "." }.Contains(host, StringComparer.OrdinalIgnoreCase);
    if (!cs.Encrypt || (cs.TrustServerCertificate && !localDemo)) throw new InvalidOperationException("Se requiere Encrypt=True y TrustServerCertificate=False; solo se admite excepción explícita para certificados locales en Demo.");
}
var sessionMinutes = builder.Configuration.GetValue<int?>("Auth:SessionMinutes") ?? 120;
if (sessionMinutes is < 1 or > 1440) throw new InvalidOperationException("Auth:SessionMinutes debe estar entre 1 y 1440.");
builder.Services.AddDbContext<GameDb>(o => o.UseSqlServer(connection, o => o.CommandTimeout(30)));
builder.Services.AddScoped<IStore, SqlStore>(); builder.Services.AddSingleton<IPasswords, Passwords>(); builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped(s => new GameService(s.GetRequiredService<IStore>(), s.GetRequiredService<IPasswords>(), s.GetRequiredService<TimeProvider>(), sessionMinutes));
builder.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions, SessionAuthentication>("Session", _ => { }); builder.Services.AddAuthorization();
var origins = (builder.Configuration["Cors:Origins"] ?? "http://localhost:4200").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).WithMethods("GET", "POST").WithHeaders("Content-Type", "Authorization")));
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.OnRejected = async (c, ct) => await c.HttpContext.Response.WriteAsJsonAsync(new { message = "Demasiados intentos. Esperá un minuto." }, ct); o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
var app = builder.Build();
GameService.DummyHash = new(() => app.Services.GetRequiredService<IPasswords>().Hash(new User(), "DummyPassword123!"));
_ = GameService.DummyHash.Value;
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    var error = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = error is AppException a ? a.Status : error is BadHttpRequestException ? 400 : 500;
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { message = error is AppException ? error.Message : status == 400 ? "Solicitud inválida. Revisá los campos enviados." : "No se pudo completar la operación. Intentá nuevamente.", traceId = ctx.TraceIdentifier });
}));
app.Use(async (ctx, next) => { ctx.Response.Headers.CacheControl = "no-store"; ctx.Response.Headers["X-Content-Type-Options"] = "nosniff"; await next(ctx); });
app.UseCors(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (GameDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapPost("/api/auth/register", async (RegisterRequest r, GameService game) => Results.Ok(await game.Register(r))).RequireRateLimiting("auth");
app.MapPost("/api/auth/login", async (LoginRequest r, GameService game) => Results.Ok(await game.Login(r))).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout", async (HttpContext c, GameService game) => { await game.Logout(c.Request.Headers.Authorization.ToString()[7..]); return Results.NoContent(); }).RequireAuthorization();
app.MapGet("/api/auth/me", (HttpContext c) => ((User)c.Items["User"]!).Dto()).RequireAuthorization();
app.MapGet("/api/tournaments/demo", (GameService game) => game.Demo()).RequireAuthorization();
app.MapPost("/api/tournaments/{id:guid}/registrations", (Guid id, HttpContext c, GameService game) => game.Join(id, (User)c.Items["User"]!)).RequireAuthorization();
app.MapPost("/api/tournaments/{id:guid}/round", (Guid id, HttpContext c, GameService game) => game.Generate(id, (User)c.Items["User"]!)).RequireAuthorization(p => p.RequireRole("Organizador"));
app.MapGet("/api/battles/{id:guid}", (Guid id, HttpContext c, GameService game) => game.Battle(id, (User)c.Items["User"]!)).RequireAuthorization();
var demoEnabled = builder.Configuration.GetValue<bool>("Demo:Enabled");
if (reset && (!demoEnabled || !app.Environment.IsEnvironment("Demo"))) throw new InvalidOperationException("Reset permitido exclusivamente en entorno Demo con Demo__Enabled=true.");
if (demoEnabled && !app.Environment.IsEnvironment("Demo")) throw new InvalidOperationException("El seed requiere ASPNETCORE_ENVIRONMENT=Demo.");
if (demoEnabled)
{
    using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<GameDb>();
    // Las migraciones se aplican explícitamente antes de arrancar; nunca EnsureCreated.
    await DemoSeed.Run(db, scope.ServiceProvider.GetRequiredService<IPasswords>(), builder.Configuration["Demo:OrganizerPassword"] ?? throw new InvalidOperationException("Falta Demo__OrganizerPassword."), builder.Configuration["Demo:RivalPassword"] ?? throw new InvalidOperationException("Falta Demo__RivalPassword."), reset);
}
if (reset) return;
app.Run();
