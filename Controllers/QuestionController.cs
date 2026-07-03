using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Models;
using WeProject.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WeProject.Controllers
{
    public class QuestionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IOpenAiService _openAiService;

        public QuestionController(AppDbContext context, IOpenAiService openAiService)
        {
            _context = context;
            _openAiService = openAiService;
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

            // ViewModel erstellen und befüllen
            var viewModel = new QuestionIndexViewModel
            {
                Chapter = chapter,
                PdfFileName = !string.IsNullOrEmpty(chapter.PdfFilePath) 
                    ? Path.GetFileName(new Uri(chapter.PdfFilePath).LocalPath) 
                    : null
            };

            return View(viewModel);
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
                _context.Questions.Add(question);
                await _context.SaveChangesAsync(); 

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
                _context.Update(question);

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
                .Include(q => q.Exams)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            int chapterId = question.ChapterId;

            // Entferne alle Zuordnungen zu Prüfungen (behebt Foreign-Key-Konflikt)
            if (question.Exams.Any())
            {
                question.Exams.Clear();
            }

            _context.AnswerOptions.RemoveRange(question.AnswerOptions);
            _context.Questions.Remove(question);
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { chapterId = chapterId });
        }

        // ========================================================
        // NEU: HIER IST DAS KI-VALIDIERUNGS-MODUL
        // ========================================================
        [HttpPost]
        public async Task<IActionResult> ValidateQuestion(int questionId, int chapterId)
        {
            var question = await _context.Questions
                .Include(q => q.AnswerOptions)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question != null)
            {
                var answers = question.AnswerOptions.Select(a => a.Text).ToList();
                
                try
                {
                    string feedback = await _openAiService.ValidateQuestionAsync(question.Text, answers);
                    TempData["ValidationResult"] = feedback;
                    TempData["ValidatedQuestionText"] = question.Text;
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Fehler bei der KI-Prüfung: " + ex.Message;
                }
            }
            
            return RedirectToAction("Index", new { chapterId = chapterId });
        }
    }
}