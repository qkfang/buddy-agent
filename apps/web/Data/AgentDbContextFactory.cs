using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BuddyAgent.Web.Data;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> migration commands.
/// Not used at runtime. The connection string here is a placeholder for
/// local development on Windows; override via the DESIGNTIME_CONNECTION_STRING
/// environment variable for non-Windows environments.
/// </summary>
public class AgentDbContextFactory : IDesignTimeDbContextFactory<AgentDbContext>
{
    public AgentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DESIGNTIME_CONNECTION_STRING")
            ?? "Server=localhost;Database=BuddyAgentDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AgentDbContext(options);
    }
}
