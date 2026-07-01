using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using WeProject.Data;
using WeProject.Models;

namespace WeProject.Controllers
{
    public class CourseController : Controller
    {
        private readonly AppDbContext _context;

        public CourseController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Course
        public async Task<IActionResult> Index()
        {
            // Lastenheft: Die Liste ist nach dem Titel der Lehrveranstaltungen sortiert.
            var courses = await _context.Courses
                .Include(c => c.Chapters)
                .Include(c => c.Exams)
                .OrderBy(c => c.Title) 
                .ToListAsync();

            return View(courses);
        }

        // GET: Course/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Course/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,LecturerName,IsMasterCourse")] Course course)
        {
            ModelState.Remove("Chapters");
            ModelState.Remove("Exams");

            if (ModelState.IsValid)
            {
                _context.Add(course);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Lehrveranstaltung erfolgreich angelegt.";
                return RedirectToAction(nameof(Index));
            }
            return View(course);
        }

        // GET: Course/Edit/X
        public async Task<IActionResult> Edit(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();
            
            return View(course);
        }

        // POST: Course/Edit/X
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,LecturerName,IsMasterCourse")] Course course)
        {
            if (id != course.Id) return NotFound();

            ModelState.Remove("Chapters");
            ModelState.Remove("Exams");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(course);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Lehrveranstaltung erfolgreich aktualisiert.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Courses.Any(e => e.Id == course.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(course);
        }

        // POST: Course/Delete/X
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Exams)
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Questions)
                        .ThenInclude(q => q.AnswerOptions)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course != null)
            {
                // Lastenheft: Beim Löschen werden auch abhängige Datensätze anderer Tabellen gelöscht. (Kaskade)
                foreach (var chapter in course.Chapters)
                {
                    foreach (var question in chapter.Questions)
                    {
                        _context.AnswerOptions.RemoveRange(question.AnswerOptions);
                    }
                    _context.Questions.RemoveRange(chapter.Questions);
                }
                _context.Chapters.RemoveRange(course.Chapters);
                _context.Exams.RemoveRange(course.Exams);
                _context.Courses.Remove(course);

                await _context.SaveChangesAsync();
                TempData["Success"] = "Lehrveranstaltung inklusive aller Kapitel, Fragen und Prüfungen restlos gelöscht.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}