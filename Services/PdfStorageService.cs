using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WeProject.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace WeProject.Services
{
    public class PdfStorageService : IPdfStorageService
    {
        private readonly string _connectionString;
        private readonly string _containerName = "pdfs";

        public PdfStorageService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("AzureBlobStorage")
                ?? configuration["AzureBlobStorage"]
                ?? string.Empty;
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "document";

            // Entferne die Erweiterung vorübergehend
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            // Ersetze Umlaute und Sonderzeichen
            nameWithoutExtension = nameWithoutExtension
                .Replace("ä", "ae")
                .Replace("ö", "oe")
                .Replace("ü", "ue")
                .Replace("ß", "ss")
                .Replace("Ä", "AE")
                .Replace("Ö", "OE")
                .Replace("Ü", "UE");

            // Entferne alle Zeichen außer Buchstaben, Zahlen, Bindestrichen und Unterstrichen
            nameWithoutExtension = Regex.Replace(nameWithoutExtension, @"[^a-zA-Z0-9_-]", "-");

            // Ersetze mehrfache Bindestriche durch einen
            nameWithoutExtension = Regex.Replace(nameWithoutExtension, @"-+", "-");

            // Entferne führende/nachfolgende Bindestriche
            nameWithoutExtension = nameWithoutExtension.Trim('-');

            // Begrenze die Länge
            if (nameWithoutExtension.Length > 50)
                nameWithoutExtension = nameWithoutExtension.Substring(0, 50);

            return nameWithoutExtension + extension;
        }

        public Task<string> UploadPdfAsync(IFormFile file)
        {
            return UploadPdfAsync(file, file.FileName);
        }

        public async Task<string> UploadPdfAsync(IFormFile file, string desiredName)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentNullException(nameof(file), "Cannot upload a null or empty file.");
            }
            
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("Azure Blob Storage connection string is not configured.");
            }

            var blobServiceClient = new BlobServiceClient(_connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            
            await blobContainerClient.CreateIfNotExistsAsync();

            // Sanitize den Dateinamen und füge Timestamp hinzu
            string sanitizedName = SanitizeFileName(desiredName);
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss");
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitizedName);
            string extension = Path.GetExtension(sanitizedName);
            
            string uniqueBlobName = $"{nameWithoutExtension}_{timestamp}{extension}";
            var blobClient = blobContainerClient.GetBlobClient(uniqueBlobName);

            // Content-Type und Content-Disposition setzen, damit der Browser die PDF anzeigt statt sie herunterzuladen
            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/pdf",
                ContentDisposition = $"inline; filename=\"{uniqueBlobName}\""
            };
            var uploadOptions = new BlobUploadOptions { HttpHeaders = blobHttpHeaders };

            await using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, uploadOptions);
            }

            return blobClient.Uri.ToString();
        }

        public async Task DeletePdfAsync(string blobUrl)
        {
            if (string.IsNullOrEmpty(_connectionString) || string.IsNullOrEmpty(blobUrl))
            {
                return;
            }

            var blobUri = new Uri(blobUrl);
            var blobName = Path.GetFileName(blobUri.LocalPath);

            var blobServiceClient = new BlobServiceClient(_connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = blobContainerClient.GetBlobClient(blobName);

            await blobClient.DeleteIfExistsAsync();
        }
    }
}