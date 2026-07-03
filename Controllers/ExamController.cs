using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace WeProject.Controllers
{
    public class ExamController : Controller
    {
        private readonly AppDbContext _context;

        public ExamController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int courseId)
        {
            var course = await _context.Courses
                .Include(c => c.Exams)
                    .ThenInclude(e => e.Questions)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null) return NotFound();

            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(int courseId, int questionCount, DateOnly examDate, TimeOnly examTime)
        {
            var course = await _context.Courses
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Questions)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null) return NotFound();

            var allQuestions = course.Chapters.SelectMany(ch => ch.Questions).ToList();

            if (allQuestions.Count < questionCount)
            {
                TempData["Error"] = $"Nicht genügend Fragen vorhanden. Der Kurs hat nur {allQuestions.Count} Fragen.";
                return RedirectToAction(nameof(Index), new { courseId = courseId });
            }

            var randomQuestions = allQuestions.OrderBy(q => Guid.NewGuid()).Take(questionCount).ToList();

            // Kombinieren von Datum und Uhrzeit zu einem einzigen DateTime-Objekt
            var examDateTime = examDate.ToDateTime(examTime);

            var newExam = new Exam
            {
                CourseId = courseId,
                ExamDate = examDateTime, // Das neue, festgelegte Datum verwenden
                Questions = randomQuestions
            };

            _context.Exams.Add(newExam);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Prüfung für den {examDateTime:dd.MM.yyyy 'um' HH:mm 'Uhr'} mit {questionCount} Fragen erfolgreich generiert!";
            return RedirectToAction(nameof(Index), new { courseId = courseId });
        }

        public async Task<IActionResult> Print(int id)
        {
            var exam = await _context.Exams
                .Include(e => e.Course)
                .Include(e => e.Questions)
                    .ThenInclude(q => q.AnswerOptions)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
            {
                return NotFound();
            }

            exam.Questions = exam.Questions.OrderBy(q => Guid.NewGuid()).ToList();

            return View(exam);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var exam = await _context.Exams
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
            {
                TempData["Error"] = "Prüfung nicht gefunden.";
                return NotFound();
            }

            int courseId = exam.CourseId;
            _context.Exams.Remove(exam);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Prüfung vom {exam.ExamDate:dd.MM.yyyy} erfolgreich gelöscht.";
            return RedirectToAction(nameof(Index), new { courseId = courseId });
        }
    }
}