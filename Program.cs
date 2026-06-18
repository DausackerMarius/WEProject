using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Standard MVC-Dienste hinzufügen
builder.Services.AddControllersWithViews();

// 2. Datenbank-Kontext mit SQLite verknüpfen
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Zurück zum echten Cloud-Service (spricht jetzt über den Connection String mit dem Azurite-Emulator)
builder.Services.AddScoped<IPdfStorageService, PdfStorageService>();

// 4. KI-Schnittstelle registrieren (Das KI-Gehirn bleibt erhalten, läuft im Hintergrund jetzt aber über Gemini)
builder.Services.AddHttpClient<IOpenAiService, GeminiService>();

// 5. Den PDF-Übersetzer registrieren (MUSS VOR builder.Build() STEHEN!)
builder.Services.AddScoped<PdfTextExtractionService>();

var app = builder.Build();

// 6. Automatische Datenbank-Initialisierung beim Start
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    
    // HIER: Der Seeder ist jetzt scharfgeschaltet und befüllt deine Datenbank!
    SeedData.Initialize(services);
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

app.Run();