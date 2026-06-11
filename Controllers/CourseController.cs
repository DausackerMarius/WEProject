using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Models;

namespace WeProject.Controllers
{
    public class CourseController : Controller
    {
        private readonly AppDbContext _context;

        // Die Datenbank wird hier per "Dependency Injection" automatisch übergeben
        public CourseController(AppDbContext context)
        {
            _context = context;
        }

        // Diese Methode lädt die Liste der Kurse für die Startseite
        public async Task<IActionResult> Index()
        {
            // Wir laden alle Kurse UND inkludieren die Kapitel, damit wir sie zählen können
            var courses = await _context.Courses
                                        .Include(c => c.Chapters)
                                        .ToListAsync();
            return View(courses);
        }

        // --- NEUE LOGIK START ---

        // GET: Zeigt das Formular zum Erstellen eines neuen Kurses an
        public IActionResult Create()
        {
            return View();
        }

        // POST: Nimmt die Formulardaten entgegen und speichert sie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,LecturerName,IsMasterCourse")] Course course)
        {
            if (ModelState.IsValid)
            {
                _context.Add(course);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index)); // Nach dem Speichern zurück zur Liste
            }
            return View(course); // Falls Fehler (z.B. Titel vergessen), zeige Formular erneut
        }

        // --- NEUE LOGIK ENDE ---
    }
}