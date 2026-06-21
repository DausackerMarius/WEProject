using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Services;
using System;

var builder = WebApplication.CreateBuilder(args);

// 1. Standard MVC-Dienste hinzufügen
builder.Services.AddControllersWithViews();

// 2. Datenbank-Kontext für Azure SQL konfigurieren
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 3. Cloud-Speicher-Dienst registrieren
builder.Services.AddScoped<IPdfStorageService, PdfStorageService>();

// 4. KI-Schnittstelle registrieren
builder.Services.AddHttpClient<IOpenAiService, GeminiService>();

// 5. Den PDF-Text-Extraktionsdienst registrieren
builder.Services.AddScoped<PdfTextExtractionService>();

var app = builder.Build();

// =================================================================
// DIAGNOSE-BLOCK: Fängt Startfehler ab und gibt sie aus
// =================================================================
try
{
    // 6. Automatische Datenbank-Initialisierung beim Start
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<AppDbContext>();
        Console.WriteLine("--> Attempting to connect to DB and ensure it is created...");
        context.Database.EnsureCreated();
        Console.WriteLine("--> DB connection successful and schema is present.");
        
        Console.WriteLine("--> Initializing Seed Data...");
        SeedData.Initialize(services);
        Console.WriteLine("--> Seed Data finished.");
    }

    // 7. HTTP-Pipeline konfigurieren
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthorization();

    // 8. Standard-Routing festlegen
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    Console.WriteLine("--> Application is starting up...");
    app.Run();
}
catch (Exception ex)
{
    // Gibt die vollständige Fehlermeldung in der Konsole aus
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("!!!!!!!!!! APPLICATION FAILED TO START !!!!!!!!!!");
    Console.WriteLine(ex.ToString());
    Console.ResetColor();
}
