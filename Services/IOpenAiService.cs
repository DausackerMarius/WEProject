using System.Collections.Generic;
using System.Threading.Tasks;

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
    }
}