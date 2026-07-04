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

        // FEATURE 1: Dateinamen generieren (Optionale Extra-Funktion)
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
                generationConfig = new { maxOutputTokens = 25, temperature = 0.1 } 
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            return await ExecuteAiRequestAsync(url, jsonPayload, false);
        }

        // FEATURE 2: Multiple-Choice Fragen generieren
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
                generationConfig = new { responseMimeType = "application/json", temperature = 0.3 }
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            return await ExecuteAiRequestAsync(url, jsonPayload, true);
        }

        // FEATURE 3: Didaktischer Gutachter (Validierung der Fragen) - PERFEKTIONIERTER PROMPT
        public async Task<string> ValidateQuestionAsync(string questionText, List<string> answers)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string answersText = string.Join("\n- ", answers);
            
            string promptText = $@"Du hast die Aufgabe, diese Prüfungsfrage als Gutachter zu bewerten. 
            Prüfe die Frage und die Antworten strikt auf diese zwei Kriterien:
            1. Sind die Frage und alle Antworten sprachlich korrekt formuliert?
            2. Ist die Frage eindeutig beantwortbar (d.h., gibt es logisch betrachtet exakt EINE korrekte Antwort)?

            Frage: 
            {questionText}
            
            Optionen:
            - {answersText}
            
            Gib ein kompaktes, professionelles Gutachten ab. Gehe direkt auf die Sprache und die Eindeutigkeit ein.";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = "Du bist ein strenger und präziser Hochschul-Gutachter für Klausurfragen." } } },
                contents = new[] { new { parts = new[] { new { text = promptText } } } },
                // LÖSUNG FÜR DEN ABBRUCH: Ausreichend Token (500) zur Verfügung gestellt, damit das Gutachten komplett durchläuft!
                generationConfig = new { maxOutputTokens = 500, temperature = 0.2 } 
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            return await ExecuteAiRequestAsync(url, jsonPayload, false);
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
                generationConfig = new { maxOutputTokens = 25, temperature = 0.1 } 
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            return await ExecuteAiRequestAsync(url, jsonPayload, false);
        }

        // =========================================================================
        // ADAPTERFASSADE: ORCHESTRIERUNG & EXPONENTIAL BACKOFF RETRY
        // =========================================================================
        private async Task<string> ExecuteAiRequestAsync(string url, string jsonPayload, bool cleanJson)
        {
            int maxRetries = 3; 
            int delayMilliseconds = 2500; 

            for (int i = 0; i < maxRetries; i++)
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    
                    var text = doc.RootElement.GetProperty("candidates")[0]
                        .GetProperty("content").GetProperty("parts")[0]
                        .GetProperty("text").GetString() ?? "";
                    
                    if (cleanJson)
                    {
                        text = text.Replace("```json", "").Replace("```", "");
                    }
                    return text.Trim();
                }

                // Retry bei Überlastung (429 Too Many Requests) oder Server-Fehler (503 Service Unavailable)
                if (((int)response.StatusCode == 429 || (int)response.StatusCode == 503) && i < (maxRetries - 1))
                {
                    await Task.Delay(delayMilliseconds);
                    delayMilliseconds *= 2; 
                    continue; 
                }

                // Fehler werfen, falls das Limit endgültig erreicht ist oder ein anderer Fehler vorliegt
                var errorContent = await response.Content.ReadAsStringAsync();
                
                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException("Die KI-Schnittstelle ist aktuell stark ausgelastet. Bitte warte einen Moment.");
                }
                    
                throw new HttpRequestException($"Gemini API Fehler ({response.StatusCode}): {errorContent}");
            }

            return string.Empty;
        }
    }
}