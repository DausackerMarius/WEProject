using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace WeProject.Controllers
{
    public class ExamController : Controller
    {
        private readonly AppDbContext _context;

        public ExamController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Lädt die Liste aller Prüfungen eines Kurses
        public async Task<IActionResult> Index(int courseId)
        {
            var course = await _context.Courses
                .Include(c => c.Exams)
                    .ThenInclude(e => e.Questions)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null) return NotFound();

            return View(course);
        }

        // POST: Der 1,0-Algorithmus zur Prüfungsgenerierung
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(int courseId, int questionCount)
        {
            // Wir laden den Kurs mit ALLEN Kapiteln und ALLEN darin enthaltenen Fragen
            var course = await _context.Courses
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Questions)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null) return NotFound();

            // Alle Fragen aus allen Kapiteln in eine flache Liste packen
            var allQuestions = course.Chapters.SelectMany(ch => ch.Questions).ToList();

            if (allQuestions.Count < questionCount)
            {
                TempData["Error"] = $"Nicht genügend Fragen vorhanden. Der Kurs hat nur {allQuestions.Count} Fragen.";
                return RedirectToAction(nameof(Index), new { courseId = courseId });
            }

            // Zufällige Auswahl von n Fragen ohne Dubletten (Guid-Sortierung ist ein extrem effizienter Shuffle-Trick)
            var randomQuestions = allQuestions.OrderBy(q => Guid.NewGuid()).Take(questionCount).ToList();

            var newExam = new Exam
            {
                CourseId = courseId,
                ExamDate = DateTime.Now,
                Questions = randomQuestions
            };

            _context.Exams.Add(newExam);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Prüfung mit {questionCount} Fragen erfolgreich generiert!";
            return RedirectToAction(nameof(Index), new { courseId = courseId });
        }

        // GET: Exam/Print
        public async Task<IActionResult> Print(int id)
        {
            var exam = await _context.Exams
                .Include(e => e.Course) // Kurs-Infos für den Titel laden
                .Include(e => e.Questions) // Alle Fragen dieser Prüfung laden
                    .ThenInclude(q => q.AnswerOptions) // Und zu jeder Frage die Antwortoptionen
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
            {
                return NotFound();
            }

            // Die Fragen für die Druckansicht mischen, damit nicht jeder die gleiche Reihenfolge hat
            exam.Questions = exam.Questions.OrderBy(q => Guid.NewGuid()).ToList();

            return View(exam);
        }
    }
}