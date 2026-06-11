using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
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

        // Das globale Labor für studentische Testzwecke
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadAndGenerate(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0) 
            {
                ViewBag.Error = "Bitte wähle ein PDF aus.";
                return View("Index");
            }

            var cloudUrl = await _storageService.UploadPdfAsync(pdfFile);
            string tempPath = Path.GetTempFileName();
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }

            string extractedText = _pdfExtractionService.ExtractTextFromPdf(tempPath);
            System.IO.File.Delete(tempPath);

            string jsonResult = await _openAiService.GenerateQuestionsFromTextAsync(extractedText);

            ViewBag.Result = jsonResult;
            ViewBag.CloudUrl = cloudUrl;

            return View("Index");
        }

        // ==========================================
        // Die scharfe Logik zum Speichern in die DB
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> GenerateForChapter(IFormFile pdfFile, int chapterId)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                // Fallback, falls kein PDF gewählt wurde
                return RedirectToAction("Index", "Chapter", new { id = chapterId });
            }

            // 1. PDF im Emulator (Azurite) hochladen
            await _storageService.UploadPdfAsync(pdfFile);

            // 2. Text extrahieren
            string tempPath = Path.GetTempFileName();
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }
            string extractedText = _pdfExtractionService.ExtractTextFromPdf(tempPath);
            System.IO.File.Delete(tempPath);

            // 3. KI-Antwort abholen
            string jsonResult = await _openAiService.GenerateQuestionsFromTextAsync(extractedText);

            try
            {
                // 4. JSON in Hilfsobjekte parsen
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var generatedQuestions = JsonSerializer.Deserialize<List<AiQuestionModel>>(jsonResult, options);

                if (generatedQuestions != null)
                {
                    foreach (var aiQ in generatedQuestions)
                    {
                        // 5. In deine exakten Domain Models umwandeln
                        var newQuestion = new Question
                        {
                            Text = aiQ.Frage,
                            ChapterId = chapterId, // Zuweisung zum Kapitel
                            AnswerOptions = new List<AnswerOption>()
                        };

                        // Antworten hinzufügen
                        for (int i = 0; i < aiQ.Antworten.Count; i++)
                        {
                            newQuestion.AnswerOptions.Add(new AnswerOption 
                            {
                                Text = aiQ.Antworten[i],
                                IsCorrect = (i == aiQ.KorrekteAntwortIndex),
                                QuestionId = default // Signalisiert EF Core den "Transient"-Zustand
                            });
                        }

                        _context.Questions.Add(newQuestion);
                    }

                    // Alles in die SQLite-Datenbank schreiben
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Fehler beim Verarbeiten der KI-Fragen: " + ex.Message;
            }

            return RedirectToAction("Index", "Chapter", new { id = chapterId });
        }
    }

    // Hilfsklasse für das JSON-Parsing
    public class AiQuestionModel
    {
        public string Frage { get; set; } = string.Empty;
        public List<string> Antworten { get; set; } = new List<string>();
        public int KorrekteAntwortIndex { get; set; }
    }
}