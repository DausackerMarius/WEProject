using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WeProject.Services
{
    public interface IOpenAiService
    {
        // Bisherige Methode
        Task<string> GenerateQuestionsFromTextAsync(string documentText, int questionCount);
        
        // Die Methode zum Prüfen einer bestehenden Frage
        Task<string> ValidateQuestionAsync(string questionText, List<string> answers);

        // NEU: Die Methode zum Generieren des Kapitel-Titels
        Task<string> GenerateTitleFromTextAsync(string documentText);

        // NEU: Die Methode zum Vorschlagen eines Dateinamens
        Task<string> SuggestFileNameForPdfAsync(IFormFile pdfFile);
    }
}