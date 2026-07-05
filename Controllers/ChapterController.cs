using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeProject.Data;
using WeProject.Models;
using WeProject.Services;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace WeProject.Controllers
{
    public class ChapterController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IPdfStorageService _storageService;
        private readonly IOpenAiService _openAiService;

        public ChapterController(AppDbContext context, IPdfStorageService storageService, IOpenAiService openAiService)
        {
            _context = context;
            _storageService = storageService;
            _openAiService = openAiService;
        }

        [HttpPost]
        public async Task<IActionResult> SuggestFileName(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                return Json(new { success = false, message = "Keine Datei empfangen." });
            }

            try
            {
                string suggestedName = await _openAiService.SuggestFileNameForPdfAsync(pdfFile);
                return Json(new { success = true, suggestedName });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Fehler bei der KI-Analyse: {ex.Message}" });
            }
        }

        // GET: Chapter?courseId=X
        public async Task<IActionResult> Index(int courseId)
        {
            var course = await _context.Courses
                .Include(c => c.Exams)
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Questions)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
            {
                return NotFound();
            }

            course.Chapters = course.Chapters.OrderBy(ch => ch.ChapterNumber).ToList();
            return View(course);
        }

        // GET: Chapter/Create?courseId=X
        public async Task<IActionResult> Create(int courseId)
        {
            var nextChapterNumber = await _context.Chapters
                .Where(c => c.CourseId == courseId)
                .Select(c => (int?)c.ChapterNumber)
                .MaxAsync() ?? 0;

            ViewBag.CourseId = courseId;
            ViewBag.NextChapterNumber = nextChapterNumber + 1;
            return View();
        }

        // POST: Chapter/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,CourseId")] Chapter chapter, IFormFile? uploadedPdf, string? desiredFileName)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Automatische fortlaufende Nummerierung für das neue Kapitel
                    var maxChapterNumber = await _context.Chapters
                        .Where(c => c.CourseId == chapter.CourseId)
                        .Select(c => (int?)c.ChapterNumber)
                        .MaxAsync() ?? 0;
                    
                    chapter.ChapterNumber = maxChapterNumber + 1;

                    if (uploadedPdf != null && uploadedPdf.Length > 0)
                    {
                        // FIX: Einbauen der .pdf-Endung
                        string fileNameToUse = string.IsNullOrWhiteSpace(desiredFileName) 
                            ? uploadedPdf.FileName 
                            : (desiredFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? desiredFileName : desiredFileName + ".pdf");

                        string? cloudUrl = await _storageService.UploadPdfAsync(uploadedPdf, fileNameToUse);
                        chapter.PdfFilePath = cloudUrl;
                    }

                    _context.Add(chapter);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index), new { courseId = chapter.CourseId });
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("", $"Fehler bei der Speicherkonfiguration: {ex.Message}");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Ein Fehler beim PDF-Upload ist aufgetreten: {ex.Message}");
                }
            }

            var nextChapterNumber = await _context.Chapters
                .Where(c => c.CourseId == chapter.CourseId)
                .Select(c => (int?)c.ChapterNumber)
                .MaxAsync() ?? 0;

            ViewBag.CourseId = chapter.CourseId;
            ViewBag.NextChapterNumber = nextChapterNumber + 1;
            return View(chapter);
        }

        // GET: Chapter/Edit/X
        public async Task<IActionResult> Edit(int id)
        {
            var chapter = await _context.Chapters.FindAsync(id);
            if (chapter == null)
            {
                return NotFound();
            }
            return View(chapter);
        }

        // POST: Chapter/Edit/X
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,ChapterNumber,CourseId")] Chapter chapterFormData, IFormFile? uploadedPdf, bool deleteFile)
        {
            if (id != chapterFormData.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // 1. Original-Objekt aus der DB laden, um unbeabsichtigtes Überschreiben zu verhindern
                var chapterToUpdate = await _context.Chapters.FindAsync(id);
                if (chapterToUpdate == null)
                {
                    return NotFound();
                }

                // 2. Nur die erlaubten Felder aus dem Formular auf das DB-Objekt übertragen
                chapterToUpdate.Title = chapterFormData.Title;
                chapterToUpdate.ChapterNumber = chapterFormData.ChapterNumber;

                try
                {
                    // 3. PDF-Logik separat und sicher behandeln
                    if (deleteFile)
                    {
                        if (!string.IsNullOrEmpty(chapterToUpdate.PdfFilePath))
                        {
                            await _storageService.DeletePdfAsync(chapterToUpdate.PdfFilePath);
                        }
                        chapterToUpdate.PdfFilePath = null; // Explizit auf null setzen
                    }
                    else if (uploadedPdf != null && uploadedPdf.Length > 0)
                    {
                        // Wenn eine neue Datei hochgeladen wird, die alte zuerst löschen
                        if (!string.IsNullOrEmpty(chapterToUpdate.PdfFilePath))
                        {
                            await _storageService.DeletePdfAsync(chapterToUpdate.PdfFilePath);
                        }

                        string? cloudUrl = await _storageService.UploadPdfAsync(uploadedPdf);
                        chapterToUpdate.PdfFilePath = cloudUrl;
                    }
                    // WICHTIG: Wenn weder 'deleteFile' noch 'uploadedPdf' zutrifft, bleibt der alte PdfFilePath einfach erhalten.

                    _context.Update(chapterToUpdate);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index), new { courseId = chapterToUpdate.CourseId });
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("", $"Fehler bei der Speicherkonfiguration: {ex.Message}");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Ein Fehler beim PDF-Upload ist aufgetreten: {ex.Message}");
                }
            }
            // Bei einem Fehler das Formular mit den eingegebenen Daten erneut anzeigen
            return View(chapterFormData);
        }

        // POST: Chapter/Delete/X
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var chapter = await _context.Chapters.FindAsync(id);
            if (chapter == null)
            {
                return NotFound();
            }

            int courseId = chapter.CourseId;

            // Kaskadierender Schutz: PDF wird beim Löschen des Kapitels aus der Cloud entfernt
            if (!string.IsNullOrEmpty(chapter.PdfFilePath))
            {
                await _storageService.DeletePdfAsync(chapter.PdfFilePath);
            }

            _context.Chapters.Remove(chapter);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { courseId = courseId });
        }
    }
}