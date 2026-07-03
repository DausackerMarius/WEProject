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
using Microsoft.EntityFrameworkCore;

namespace WeProject.Controllers
{
    public class GeneratorController : Controller
    {
        private readonly IPdfStorageService _storageService;
        private readonly PdfTextExtractionService _pdfExtractionService;
        private readonly IOpenAiService _openAiService;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public GeneratorController(
            IPdfStorageService storageService, 
            PdfTextExtractionService pdfExtractionService, 
            IOpenAiService openAiService,
            AppDbContext context,
            IHttpClientFactory httpClientFactory)
        {
            _storageService = storageService;
            _pdfExtractionService = pdfExtractionService;
            _openAiService = openAiService;
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return NotFound();
        }

        // NOTE: STUDENT MODE
        // Default parameter `mode = "student"` enables the interactive student view (Selbsttest).
        // We only annotate this for clarity — do not delete or change without coordinating with the team.
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
        // 1. KI aufrufen & Vorschau anzeigen (NEUES PDF)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> GenerateForChapter(IFormFile pdfFile, int chapterId, int questionCount = 3)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                TempData["Error"] = "Bitte wählen Sie eine PDF-Datei zum Hochladen aus.";
                return RedirectToAction("Index", "Question", new { chapterId = chapterId });
            }

            string tempPath = Path.GetTempFileName();
            try
            {
                // PDF in temporäre Datei speichern für die Text-Extraktion
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }
                string extractedText = _pdfExtractionService.ExtractTextFromPdf(tempPath);

                // KI aufrufen
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
            catch (Exception ex)
            {
                TempData["Error"] = "Fehler beim Verarbeiten des PDFs: " + ex.Message;
                return RedirectToAction("Index", "Question", new { chapterId = chapterId });
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }
        }

        // =====================================================
        // NEUE ACTION: Generiert Fragen aus einem bereits hochgeladenen PDF
        // =====================================================
        [HttpPost]
        public async Task<IActionResult> GenerateFromExisting(int chapterId, int questionCount = 3)
        {
            var chapter = await _context.Chapters.FirstOrDefaultAsync(c => c.Id == chapterId);

            if (chapter == null || string.IsNullOrEmpty(chapter.PdfFilePath))
            {
                TempData["Error"] = "Für dieses Kapitel ist kein PDF hinterlegt. Bitte laden Sie zuerst eine Datei hoch.";
                return RedirectToAction("Index", "Question", new { chapterId = chapterId });
            }

            string tempPath = Path.GetTempFileName();
            try
            {
                // PDF aus dem Cloud-Speicher herunterladen
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(chapter.PdfFilePath);
                response.EnsureSuccessStatusCode();
                await using (var fs = new FileStream(tempPath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }

                // Text aus dem heruntergeladenen PDF extrahieren
                string extractedText = _pdfExtractionService.ExtractTextFromPdf(tempPath);

                // KI aufrufen und Vorschau anzeigen
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
            catch (Exception ex)
            {
                TempData["Error"] = "Fehler beim Verarbeiten des vorhandenen PDFs: " + ex.Message;
                return RedirectToAction("Index", "Question", new { chapterId = chapterId });
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }
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

        // =====================================================
        // NEUE ACTION: Unsichtbarer Endpunkt für den KI-Titel-Button
        // =====================================================
        [HttpPost]
        public async Task<IActionResult> SuggestTitle(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
                return Json(new { success = false, message = "Bitte lade zuerst ein PDF hoch, damit die KI den Titel generieren kann." });

            string tempPath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }

                string extractedText = _pdfExtractionService.ExtractTextFromPdf(tempPath);
                string suggestedTitle = await _openAiService.GenerateTitleFromTextAsync(extractedText);

                if (string.IsNullOrWhiteSpace(suggestedTitle))
                {
                    return Json(new { success = false, message = "Die KI konnte aus diesem PDF keinen Titel ableiten. Bitte trage den Titel selbst ein." });
                }

                return Json(new { success = true, title = suggestedTitle.Trim() });
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("503"))
            {
                return Json(new { success = false, message = "Die KI ist gerade nicht verfügbar. Bitte versuche es später erneut oder trage den Titel selbst ein." });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Die Titelgenerierung konnte gerade nicht abgeschlossen werden. Bitte prüfe das PDF oder trage den Titel selbst ein." });
            }
            finally
            {
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
            }
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