using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Models;
using System.Linq;
using System.Threading.Tasks;

namespace WeProject.Controllers
{
    public class QuestionController : Controller
    {
        private readonly AppDbContext _context;

        public QuestionController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Question?chapterId=X
        public async Task<IActionResult> Index(int chapterId)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Course)
                .Include(c => c.Questions)
                    .ThenInclude(q => q.AnswerOptions)
                .FirstOrDefaultAsync(c => c.Id == chapterId);

            if (chapter == null)
            {
                return NotFound();
            }

            return View(chapter);
        }

        // GET: Question/Create?chapterId=X
        public IActionResult Create(int chapterId)
        {
            ViewBag.ChapterId = chapterId;
            return View();
        }

        // POST: Question/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Text,ChapterId")] Question question, string[] options, int correctOptionIndex)
        {
            if (ModelState.IsValid && options != null && options.Length == 4)
            {
                // 1. Frage in der Datenbank speichern
                _context.Questions.Add(question);
                await _context.SaveChangesAsync(); 

                // 2. Die 4 zugehörigen Antwortoptionen anlegen
                for (int i = 0; i < 4; i++)
                {
                    var option = new AnswerOption
                    {
                        Text = options[i],
                        IsCorrect = (i == correctOptionIndex),
                        QuestionId = question.Id
                    };
                    _context.AnswerOptions.Add(option);
                }
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index), new { chapterId = question.ChapterId });
            }

            ViewBag.ChapterId = question.ChapterId;
            return View(question);
        }

        // GET: Question/Edit/X
        public async Task<IActionResult> Edit(int id)
        {
            var question = await _context.Questions
                .Include(q => q.AnswerOptions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            // Ermitteln, welche Option aktuell als korrekt markiert ist
            var optionsList = question.AnswerOptions.ToList();
            int correctIndex = optionsList.FindIndex(o => o.IsCorrect);
            ViewBag.CorrectOptionIndex = correctIndex >= 0 ? correctIndex : 0;

            return View(question);
        }

        // POST: Question/Edit/X
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Text,ChapterId")] Question question, int[] optionIds, string[] options, int correctOptionIndex)
        {
            if (id != question.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid && options != null && options.Length == 4 && optionIds != null && optionIds.Length == 4)
            {
                // 1. Fragentext aktualisieren
                _context.Update(question);

                // 2. Bestehende Antwortoptionen über ihre IDs aktualisieren
                for (int i = 0; i < 4; i++)
                {
                    var existingOption = await _context.AnswerOptions.FindAsync(optionIds[i]);
                    if (existingOption != null)
                    {
                        existingOption.Text = options[i];
                        existingOption.IsCorrect = (i == correctOptionIndex);
                        _context.Update(existingOption);
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { chapterId = question.ChapterId });
            }

            return View(question);
        }

        // POST: Question/Delete/X
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var question = await _context.Questions
                .Include(q => q.AnswerOptions)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            int chapterId = question.ChapterId;

            // Kaskadierendes Löschen der Optionen erzwingen
            _context.AnswerOptions.RemoveRange(question.AnswerOptions);
            _context.Questions.Remove(question);
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { chapterId = chapterId });
        }
    }
}