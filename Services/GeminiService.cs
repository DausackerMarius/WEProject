using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace WeProject.Services
{
    public class GeminiService : IOpenAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly PdfTextExtractionService _pdfTextExtractionService;

        public GeminiService(HttpClient httpClient, IConfiguration config, PdfTextExtractionService pdfTextExtractionService)
        {
            _httpClient = httpClient;
            _apiKey = config["Gemini:ApiKey"]
                      ?? config["Gemini__ApiKey"]
                      ?? config["GeminiApiKey"]
                      ?? throw new InvalidOperationException("Gemini API Key fehlt.");
            _pdfTextExtractionService = pdfTextExtractionService;
        }

        // FEATURE 1 (Von deiner Partnerin): Dateinamen generieren
        public async Task<string> SuggestFileNameForPdfAsync(IFormFile pdfFile)
        {
            string documentText = await _pdfTextExtractionService.ExtractTextFromPdfAsync(pdfFile);
            
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string promptText = $@"Du bist ein präziser Assistent für die Dateiverwaltung. 
            Analysiere den folgenden Text aus einem Vorlesungs-Skript und generiere einen passenden, extrem kurzen Dateinamen (maximal 3 bis 5 Wörter, keine Umlaute, keine Sonderzeichen, nur mit Bindestrichen getrennt). 
            Antworte AUSSCHLIESSLICH mit dem Dateinamen, ohne die .pdf-Endung, ohne Anführungszeichen, ohne Markdown und ohne Erklärungen.
            Beispiel: 'Einfuehrung-in-ASP-NET'
            
            Text: {documentText}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = promptText } } } },
                // PERFORMANCE-UPGRADE: Strikte Token-Begrenzung für Millisekunden-Latenz
                generationConfig = new { maxOutputTokens = 25, temperature = 0.1 } 
            };

            return await ExecuteAiRequestAsync(url, requestBody, false);
        }

        // FEATURE 2: Fragen generieren
        public async Task<string> GenerateQuestionsFromTextAsync(string documentText, int questionCount)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string systemPrompt = $@"Du bist ein strenger Universitätsprofessor. 
            Erstelle aus dem folgenden Text exakt {questionCount} Multiple-Choice-Fragen auf akademischem Niveau. 
            Gib das Ergebnis AUSSCHLIESSLICH als valides JSON-Array zurück, ohne Markdown-Formatierung. 
            Jedes Element im Array muss exakt so aufgebaut sein: 
            {{ ""frage"": ""..."", ""antworten"": [""A"", ""B"", ""C"", ""D""], ""korrekteAntwortIndex"": 0 }}";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = documentText } } } },
                // PERFORMANCE-UPGRADE: Sicheres JSON
                generationConfig = new { responseMimeType = "application/json", temperature = 0.3 }
            };

            return await ExecuteAiRequestAsync(url, requestBody, true);
        }

        // FEATURE 3: Fragen validieren (Gutachter)
        public async Task<string> ValidateQuestionAsync(string questionText, List<string> answers)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string answersText = string.Join(" | ", answers);
            string promptText = $@"Prüfe die folgende Multiple-Choice-Frage:
            Frage: {questionText}
            Antwortoptionen: {answersText}
            
            Ist die Frage sprachlich korrekt formuliert? Gibt es bei diesen Optionen exakt EINE eindeutig richtige Antwort, oder sind mehrere richtig/falsch? 
            Antworte als Gutachter kurz, präzise und in maximal 2-3 Sätzen.";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = "Du bist ein Universitätsprofessor und Experte für Didaktik." } } },
                contents = new[] { new { parts = new[] { new { text = promptText } } } },
                // PERFORMANCE-UPGRADE: Begrenzung auf 150 Token hält Ladezeit kurz
                generationConfig = new { maxOutputTokens = 150, temperature = 0.2 } 
            };

            return await ExecuteAiRequestAsync(url, requestBody, false);
        }

        // FEATURE 4: Kapitel-Titel generieren
        public async Task<string> GenerateTitleFromTextAsync(string documentText)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string promptText = $@"Du bist ein präziser Assistent für Universitätsprofessoren. 
            Analysiere den folgenden Text eines Vorlesungs-Skripts und generiere einen passenden, extrem kurzen Titel (maximal 3 bis 6 Wörter) für dieses Kapitel. 
            Antworte AUSSCHLIESSLICH mit dem Titel, ohne Anführungszeichen, ohne Markdown und ohne Erklärungen.
            
            Text: {documentText}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = promptText } } } },
                // PERFORMANCE-UPGRADE: 25 Token genügen für Titel
                generationConfig = new { maxOutputTokens = 25, temperature = 0.1 } 
            };

            return await ExecuteAiRequestAsync(url, requestBody, false);
        }

        // =========================================================================
        // ZENTRALES PERFORMANCE- & FEHLER-MANAGEMENT
        // =========================================================================
        private async Task<string> ExecuteAiRequestAsync(string url, object body, bool cleanJson)
        {
            var jsonPayload = JsonSerializer.Serialize(body);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content);
            
            // Fängt das "TooManyRequests" ab, das deine Partnerin gepostet hat
            if ((int)response.StatusCode == 429)
            {
                throw new HttpRequestException("TooManyRequests");
            }
            
            // Falls ein anderer Fehler auftritt, leeren String zurückgeben (sicheres Fallback)
            if (!response.IsSuccessStatusCode) return ""; 

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            
            var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
            
            if (cleanJson)
            {
                text = text.Replace("```json", "").Replace("```", "");
            }
            return text.Trim();
        }
    }
}