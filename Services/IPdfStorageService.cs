using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace WeProject.Services
{
    public interface IPdfStorageService
    {
        Task<string> UploadPdfAsync(IFormFile file);
        Task<string> UploadPdfAsync(IFormFile file, string desiredName);
        Task DeletePdfAsync(string blobUrl);
    }
}