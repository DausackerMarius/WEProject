using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WeProject.Services
{
    public class LocalPdfStorageService : IPdfStorageService
    {
        private readonly IWebHostEnvironment _environment;

        public LocalPdfStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string?> UploadPdfAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            // Legt einen Ordner "uploads" im wwwroot-Verzeichnis an
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueName);

            // Speichert die Datei lokal
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Gibt den lokalen relativen Pfad zurück
            return "/uploads/" + uniqueName;
        }

        public async Task DeletePdfAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return;

            string filePath = Path.Combine(_environment.WebRootPath, fileUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            
            await Task.CompletedTask;
        }
    }
}