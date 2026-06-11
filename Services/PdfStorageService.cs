using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WeProject.Services
{
    // Das Interface (Der Bauplan für den Service)
    public interface IPdfStorageService
    {
        // Das Fragezeichen macht den String "nullable", also tolerant für leere Rückgaben
        Task<string?> UploadPdfAsync(IFormFile file);
        Task DeletePdfAsync(string? fileUrl);
    }

    // Die eigentliche Logik
    public class PdfStorageService : IPdfStorageService
    {
        private readonly string _connectionString;
        private readonly string _containerName = "vorlesungsfolien"; 

        public PdfStorageService(IConfiguration configuration)
        {
            // Das ?? "" verhindert Abstürze, falls der Schlüssel in der appsettings.json noch fehlt
            _connectionString = configuration.GetConnectionString("AzureBlobStorage") ?? "";
        }

        public async Task<string?> UploadPdfAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            string uniqueName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var blobClient = containerClient.GetBlobClient(uniqueName);

            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
            }

            return blobClient.Uri.ToString();
        }

        public async Task DeletePdfAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return;

            try
            {
                var uri = new Uri(fileUrl);
                string blobName = Path.GetFileName(uri.LocalPath);

                var blobServiceClient = new BlobServiceClient(_connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                await blobClient.DeleteIfExistsAsync();
            }
            catch 
            { 
                // Fehler abfangen, falls die Datei in der Cloud nicht existiert
            }
        }
    }
}