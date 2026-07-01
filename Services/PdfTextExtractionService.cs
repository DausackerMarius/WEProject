using System.Text;
using UglyToad.PdfPig;

namespace WeProject.Services
{
    public class PdfTextExtractionService
    {
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