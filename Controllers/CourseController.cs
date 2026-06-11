using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Models;
using System.Threading.Tasks;

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
            // Lädt alle Kurse inkl. Kapitel UND Prüfungen für die Badge-Zähler
            var courses = await _context.Courses
                                        .Include(c => c.Chapters)
                                        .Include(c => c.Exams)
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
            if (ModelState.IsValid)
            {
                _context.Add(course);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Der Kurs wurde erfolgreich erstellt.";
                return RedirectToAction(nameof(Index));
            }
            return View(course);
        }

        // GET: Course/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                return NotFound();
            }
            return View(course);
        }

        // POST: Course/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,LecturerName,IsMasterCourse")] Course course)
        {
            if (id != course.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(course);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Der Kurs wurde erfolgreich aktualisiert.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Courses.Any(e => e.Id == course.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(course);
        }

        // POST: Course/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                return NotFound();
            }

            // Kaskadierendes Löschen durch EF Core entfernt auch Kapitel und Prüfungen
            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Der Kurs wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }
    }
}