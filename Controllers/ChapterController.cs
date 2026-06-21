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

        public ChapterController(AppDbContext context, IPdfStorageService storageService)
        {
            _context = context;
            _storageService = storageService;
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
        public IActionResult Create(int courseId)
        {
            ViewBag.CourseId = courseId;
            return View();
        }

        // POST: Chapter/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,CourseId")] Chapter chapter, IFormFile? uploadedPdf) // ChapterNumber entfernt aus Bind
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
                        string? cloudUrl = await _storageService.UploadPdfAsync(uploadedPdf);
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

            ViewBag.CourseId = chapter.CourseId;
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,ChapterNumber,CourseId,PdfFilePath")] Chapter chapter, IFormFile? uploadedPdf, bool deleteFile)
        {
            if (id != chapter.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (deleteFile)
                    {
                        if (!string.IsNullOrEmpty(chapter.PdfFilePath))
                        {
                            await _storageService.DeletePdfAsync(chapter.PdfFilePath);
                        }
                        chapter.PdfFilePath = null;
                    }
                    else if (uploadedPdf != null && uploadedPdf.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(chapter.PdfFilePath))
                        {
                            await _storageService.DeletePdfAsync(chapter.PdfFilePath);
                        }

                        string? cloudUrl = await _storageService.UploadPdfAsync(uploadedPdf);
                        chapter.PdfFilePath = cloudUrl;
                    }

                    _context.Update(chapter);
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
            return View(chapter);
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