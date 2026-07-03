using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig;

namespace WeProject.Services
{
    public class PdfTextExtractionService
    {
        public async Task<string> ExtractTextFromPdfAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return string.Empty;
            }

            var textBuilder = new StringBuilder();

            // Öffnet das PDF direkt aus dem Stream der hochgeladenen Datei
            await using (var stream = file.OpenReadStream())
            {
                using (var document = PdfDocument.Open(stream))
                {
                    foreach (var page in document.GetPages())
                    {
                        textBuilder.Append(page.Text);
                        textBuilder.Append(" "); // Leerzeichen zwischen den Seiten
                    }
                }
            }

            return textBuilder.ToString();
        }

        public string ExtractTextFromPdf(string filePath)
        {
            StringBuilder textBuilder = new StringBuilder();

            // Öffnet das PDF von der Festplatte
            using (PdfDocument document = PdfDocument.Open(filePath))
            {
                // Geht Seite für Seite durch und kopiert den Text
                foreach (var page in document.GetPages())
                {
                    textBuilder.Append(page.Text);
                    textBuilder.Append(" "); // Leerzeichen zwischen den Seiten
                }
            }

            return textBuilder.ToString();
        }
    }
}