using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models; // WICHTIG: Diese Zeile ist neu für die HTTP-Header!
using WeProject.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;
using System.IO;

namespace WeProject.Services
{
    public class PdfStorageService : IPdfStorageService
    {
        private readonly string _connectionString;
        private readonly string _containerName = "pdfs";

        public PdfStorageService(IConfiguration configuration)
        {
            // Greift auf den Verbindungsstring zu und stellt sicher, dass er nicht null ist, um die Compiler-Warnung zu beheben.
            _connectionString = configuration.GetConnectionString("AzureBlobStorage")
                ?? configuration["AzureBlobStorage"]
                ?? string.Empty;
        }

        public async Task<string> UploadPdfAsync(IFormFile file)
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

            // Eindeutigen Namen für das Blob generieren, um Überschreibungen zu vermeiden
            var uniqueBlobName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var blobClient = blobContainerClient.GetBlobClient(uniqueBlobName);

            // NEU: Wir definieren den Content-Type als PDF, damit der Browser es öffnet und nicht herunterlädt
            var blobHttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" };
            var uploadOptions = new BlobUploadOptions { HttpHeaders = blobHttpHeaders };

            await using (var stream = file.OpenReadStream())
            {
                // NEU: Wir übergeben die uploadOptions an Azure
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