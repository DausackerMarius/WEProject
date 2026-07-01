using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace WeProject.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Baut die Konfiguration auf und liest die Datei OHNE Leerzeichen
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            
            // Holt sich den Connection String
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // Nutzt den SQL Server
            builder.UseSqlServer(connectionString);

            return new AppDbContext(builder.Options);
        }
    }
}