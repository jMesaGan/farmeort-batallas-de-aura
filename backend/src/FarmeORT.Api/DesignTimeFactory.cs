using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace FarmeORT.Infrastructure;

public class DesignTimeFactory : IDesignTimeDbContextFactory<GameDb>
{
    public GameDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<GameDb>().UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer") ?? "Server=localhost;Database=FarmeORTDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=False").Options);
}
