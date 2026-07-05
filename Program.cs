using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Services;
using System;

var builder = WebApplication.CreateBuilder(args);

// Logging immer auf der Konsole aktivieren, damit Azure Log Stream Startfehler zeigt.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

// 1. Standard MVC-Dienste hinzufügen
builder.Services.AddControllersWithViews();

// 2. Datenbank-Kontext für Azure SQL konfigurieren
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? builder.Configuration["DefaultConnection"]
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? Environment.GetEnvironmentVariable("SQLCONNSTR_DefaultConnection")
    ?? Environment.GetEnvironmentVariable("SQLAZURECONNSTR_DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DefaultConnection");
Console.WriteLine($"DefaultConnection configured: {!string.IsNullOrWhiteSpace(connectionString)}");
Console.WriteLine($"Connection string length: {connectionString?.Length ?? 0}");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Die Datenbank-Verbindungszeichenfolge 'DefaultConnection' ist nicht konfiguriert oder leer.");
    }

    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(3));
});

// 3. Cloud-Speicher-Dienst registrieren
builder.Services.AddScoped<IPdfStorageService, PdfStorageService>();

// 4. KI-Schnittstelle (Gemini) mit HttpClientFactory und API-Schlüssel registrieren
builder.Services.AddHttpClient<IOpenAiService, GeminiService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    // Setzen eines sehr hohen Timeouts, da der GeminiService sein eigenes, intelligenteres Timeout-Management hat.
    // Dies verhindert den SSL-Fehler, der durch das Konflikt zwischen dem globalen Timeout und der Semaphore-Logik entsteht.
    client.Timeout = TimeSpan.FromMinutes(5);
    
    // Liest den API-Schlüssel aus der Konfiguration (appsettings.json oder Azure App Settings)
    string? geminiApiKey = builder.Configuration["Gemini:ApiKey"]
        ?? builder.Configuration["Gemini__ApiKey"]
        ?? builder.Configuration["GeminiApiKey"];

    Console.WriteLine($"Gemini API key configured: {!string.IsNullOrWhiteSpace(geminiApiKey)}");
    
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
            SameSite = SameSiteMode.Lax
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
    // Automatische Datenbank-Initialisierung beim Start (Jetzt über sichere Migrations!)
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<AppDbContext>();
        
        try
        {
            Console.WriteLine("--> Attempting to connect to DB and apply migrations...");
            
            // HIER WURDE ENSURECREATED DURCH MIGRATE ERSETZT:
            context.Database.Migrate();
            
            Console.WriteLine("--> DB migrations applied successfully and schema is present.");
            
            Console.WriteLine("--> Initializing Seed Data...");
            SeedData.Initialize(services);
            Console.WriteLine("--> Seed Data finished.");
        }
        catch (Exception dbEx)
        {
            Console.WriteLine($"--> DB initialization failed (but continuing): {dbEx.Message}");
            // Nicht fatal - die App kann ohne vollständige DB-Migration starten
        }
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