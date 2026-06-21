using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeProject.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Baut die Konfiguration NUR aus app settings.json auf
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("app settings.json")
            .Build();

        // Erstellt den DbContextOptionsBuilder
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        
        // Holt den Connection String und konfiguriert SQL Server
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        optionsBuilder.UseSqlServer(connectionString);

        // Gibt eine neue Instanz von AppDbContext zurück
        return new AppDbContext(optionsBuilder.Options);
    }
}