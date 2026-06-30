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

// 4. KI-Schnittstelle (Gemini) mit HttpClientFactory und API-Schlüssel registrieren
builder.Services.AddHttpClient<IOpenAiService, GeminiService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    
    // Liest den API-Schlüssel aus der Konfiguration (appsettings.json oder Azure App Settings)
    string? geminiApiKey = builder.Configuration["Gemini:ApiKey"];
    
    // Fügt den API-Schlüssel als Standard-Header für alle Anfragen dieses Clients hinzu
    if (!string.IsNullOrEmpty(geminiApiKey))
    {
        client.DefaultRequestHeaders.Add("x-goog-api-key", geminiApiKey);
    }
});

// 5. Den PDF-Text-Extraktionsdienst registrieren
builder.Services.AddScoped<PdfTextExtractionService>();

var app = builder.Build();

// --- Start: Zugang ohne IP-Adresse ---
app.Use(async (context, next) =>
{
    // Geheimes Zugangswort 
    string secretKey = "prüfungsgenerator-2026"; 

    // 1. Prüfen: Kommt jemand über den speziellen Link?
    if (context.Request.Query["zugang"] == secretKey)
    {
        // Cookie setzen, das 30 Tage hält
        context.Response.Cookies.Append("ProjectAccess", "Granted", new CookieOptions 
        { 
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            Secure = true, 
            SameSite = SameSiteMode.Strict
        });

        // Optional: Weiterleitung auf schöneren Link
        context.Response.Redirect("/");
        return;
    }

    // 2. Prüfen: gültiges Cookie?
    if (context.Request.Cookies.ContainsKey("ProjectAccess"))
    {
        // falls ja
        await next(); 
    }
    else
    {
        // 3. Kein Zugang: kein Cookie und falscher Link
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(@"
            <div style='font-family: sans-serif; text-align: center; margin-top: 50px;'>
                <h2>Zugriff geschützt</h2>
                <p>Bitte nutzen Sie für die Begutachtung den vollständigen Link aus der E-Mail.</p>
            </div>");
    }
});
// --- ENDE: Zugang ---

// =================================================================
// DIAGNOSE-BLOCK: Fängt Startfehler ab und gibt sie aus
// =================================================================
try
{
    // Automatische Datenbank-Initialisierung beim Start
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

    // HTTP-Pipeline konfigurieren
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthorization();

    // Standard-Routing festlegen
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
