using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using WeProject.Services;
using WeProject.Data;
using WeProject.Models;

namespace WeProject.Controllers
{
    public class GeneratorController : Controller
    {
        private readonly IPdfStorageService _storageService;
        private readonly PdfTextExtractionService _pdfExtractionService;
        private readonly IOpenAiService _openAiService;
        private readonly AppDbContext _context;

        public GeneratorController(
            IPdfStorageService storageService, 
            PdfTextExtractionService pdfExtractionService, 
            IOpenAiService openAiService,
            AppDbContext context)
        {
            _storageService = storageService;
            _pdfExtractionService = pdfExtractionService;
            _openAiService = openAiService;
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadAndGenerate(IFormFile pdfFile, string mode = "student", int questionCount = 3)
        {
            if (pdfFile == null || pdfFile.Length == 0) 
            {
                ViewBag.Error = "Bitte wähle ein PDF aus.";
                return View("Index");
            }

            ViewBag.Mode = mode;
            var cloudUrl = await _storageService.UploadPdfAsync(pdfFile);
            string tempPath = Path.GetTempFileName();
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }
            string extractedText = _pdfExtractionService.ExtractTextFromPdf(tempPath);
            System.IO.File.Delete(tempPath);

            try
            {
                string jsonResult = await _openAiService.GenerateQuestionsFromTextAsync(extractedText, questionCount);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                ViewBag.Questions = JsonSerializer.Deserialize<List<AiQuestionModel>>(jsonResult, options);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("503"))
            {
                ViewBag.Error = "Die KI-Server sind aktuell ausgelastet. Bitte warte kurz und versuche es erneut.";
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Ein unerwarteter Fehler ist aufgetreten: {ex.Message}";
            }

            ViewBag.CloudUrl = cloudUrl;
            return View("Index");
        }

        // ==========================================
        // 1. KI aufrufen & Vorschau anzeigen
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> GenerateForChapter(IFormFile pdfFile, int chapterId, int questionCount = 3)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                return RedirectToAction("Index", "Question", new { chapterId = chapterId });
            }

            await _storageService.UploadPdfAsync(pdfFile);
            string tempPath = Path.GetTempFileName();
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }
            string extractedText = _pdfExtractionService.ExtractTextFromPdf(tempPath);
            System.IO.File.Delete(tempPath);

            try
            {
                // HIER: Die korrekte Übergabe an das aktualisierte Interface
                string jsonResult = await _openAiService.GenerateQuestionsFromTextAsync(extractedText, questionCount);
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var generatedQuestions = JsonSerializer.Deserialize<List<AiQuestionModel>>(jsonResult, options);

                var viewModel = new PreviewViewModel 
                {
                    ChapterId = chapterId,
                    Questions = generatedQuestions ?? new List<AiQuestionModel>()
                };

                return View("Preview", viewModel);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("503"))
            {
                TempData["Error"] = "Die KI-Server sind aktuell ausgelastet. Bitte versuche es in ein paar Minuten noch einmal.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Fehler beim Verarbeiten der KI-Fragen: " + ex.Message;
            }

            return RedirectToAction("Index", "Question", new { chapterId = chapterId });
        }

        // ==========================================
        // 2. Nur die ausgewählten Fragen speichern!
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> SaveSelectedQuestions(int chapterId, List<AiQuestionModel> questions, List<int> selectedIndices)
        {
            if (selectedIndices != null && selectedIndices.Any())
            {
                foreach (var index in selectedIndices)
                {
                    var aiQ = questions[index];
                    var newQuestion = new Question
                    {
                        Text = aiQ.Frage,
                        ChapterId = chapterId, 
                        AnswerOptions = new List<AnswerOption>()
                    };

                    for (int i = 0; i < aiQ.Antworten.Count; i++)
                    {
                        newQuestion.AnswerOptions.Add(new AnswerOption 
                        {
                            Text = aiQ.Antworten[i],
                            IsCorrect = (i == aiQ.KorrekteAntwortIndex),
                            QuestionId = default 
                        });
                    }
                    _context.Questions.Add(newQuestion);
                }
                
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{selectedIndices.Count} Frage(n) erfolgreich in den Klausur-Pool aufgenommen!";
            }
            else
            {
                TempData["Error"] = "Du hast keine Fragen ausgewählt. Es wurde nichts gespeichert.";
            }

            return RedirectToAction("Index", "Question", new { chapterId = chapterId });
        }
    }

    // Hilfsklassen
    public class AiQuestionModel
    {
        public string Frage { get; set; } = string.Empty;
        public List<string> Antworten { get; set; } = new List<string>();
        public int KorrekteAntwortIndex { get; set; }
    }

    public class PreviewViewModel
    {
        public int ChapterId { get; set; }
        public List<AiQuestionModel> Questions { get; set; } = new List<AiQuestionModel>();
    }
}