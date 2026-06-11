using System.Threading.Tasks;

namespace WeProject.Services
{
    public interface IOpenAiService
    {
        // Die Methode nimmt später einfach nur den rohen Text aus dem PDF 
        // und gibt einen fertigen JSON-String mit den Multiple-Choice-Fragen zurück.
        Task<string> GenerateQuestionsFromTextAsync(string documentText);
    }
}