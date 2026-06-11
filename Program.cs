using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Standard MVC-Dienste hinzufügen
builder.Services.AddControllersWithViews();

// 2. Datenbank-Kontext mit SQLite verknüpfen
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. NEU: Den Azure Cloud Storage Service registrieren (Best Practice für die Architektur-Wertung)
builder.Services.AddScoped<IPdfStorageService, PdfStorageService>();

var app = builder.Build();

// 4. Automatische Datenbank-Initialisierung beim Start
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    
    // Falls eure SeedData-Klasse eine Initialize-Methode besitzt, kann sie hier aktiv sein:
    // SeedData.Initialize(services);
}

// 5. HTTP-Pipeline konfigurieren
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// 6. Standard-Routing festlegen
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();