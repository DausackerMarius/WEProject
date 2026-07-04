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
            // Best Practice: Großzügiges Timeout (60s), damit die KI bei großen PDFs nie einfriert
            if (_httpClient.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(60);
            }
            
            _apiKey = config["Gemini:ApiKey"]
                      ?? config["Gemini__ApiKey"]
                      ?? config["GeminiApiKey"]
                      ?? throw new InvalidOperationException("Gemini API Key fehlt.");
            _pdfTextExtractionService = pdfTextExtractionService;
        }

        // FEATURE 1: Dateinamen generieren
        public async Task<string> SuggestFileNameForPdfAsync(IFormFile pdfFile)
        {
            string documentText = await _pdfTextExtractionService.ExtractTextFromPdfAsync(pdfFile);
            
            // PERFORMANCE-FIX: Max 10.000 Zeichen (reicht für Titel völlig aus, schont Netzwerk)
            string truncatedText = documentText.Length > 10000 ? documentText.Substring(0, 10000) : documentText;
            
            // SAUBERE URL MIT KORREKTEM MODELL (2.5-flash)
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string promptText = $@"Du bist ein präziser Assistent für die Dateiverwaltung. 
            Analysiere den folgenden Starttext aus einem Vorlesungs-Skript und generiere einen passenden, extrem kurzen Dateinamen (maximal 3 bis 5 Wörter, keine Umlaute, keine Sonderzeichen, nur mit Bindestrichen getrennt). 
            Antworte AUSSCHLIESSLICH mit dem Dateinamen, ohne die .pdf-Endung, ohne Anführungszeichen, ohne Markdown und ohne Erklärungen.
            Beispiel: 'Einfuehrung-in-ASP-NET'
            
            Textauszug: {truncatedText}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = promptText } } } },
                generationConfig = new { maxOutputTokens = 30, temperature = 0.1 } 
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            var result = await ExecuteAiRequestAsync(url, jsonPayload);
            
            return (string.IsNullOrWhiteSpace(result) || result.Trim().Length <= 1) ? "Neues-Dokument" : result;
        }

        // FEATURE 2: Multiple-Choice Fragen generieren
        public async Task<string> GenerateQuestionsFromTextAsync(string documentText, int questionCount)
        {
            // PERFORMANCE-FIX: Max 100.000 Zeichen (ca. 40-50 Seiten) schützt vor API-Timeouts bei 32MB PDFs
            string truncatedText = documentText.Length > 100000 ? documentText.Substring(0, 100000) : documentText;

            // SAUBERE URL MIT KORREKTEM MODELL (2.5-flash)
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string systemPrompt = $@"Du bist ein strenger Universitätsprofessor. 
            Erstelle aus dem folgenden Text exakt {questionCount} Multiple-Choice-Fragen auf akademischem Niveau. 
            Gib das Ergebnis AUSSCHLIESSLICH als valides JSON-Array zurück, ohne Markdown-Formatierung. 
            Jedes Element im Array muss exakt so aufgebaut sein: 
            {{ ""frage"": ""..."", ""antworten"": [""A"", ""B"", ""C"", ""D""], ""korrekteAntwortIndex"": 0 }}";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = truncatedText } } } },
                // Google generiert hier garantiert pures JSON
                generationConfig = new { responseMimeType = "application/json", temperature = 0.3 }
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            return await ExecuteAiRequestAsync(url, jsonPayload);
        }

        // FEATURE 3: Didaktischer Gutachter (Validierung der Fragen)
        public async Task<string> ValidateQuestionAsync(string questionText, List<string> answers)
        {
            // SAUBERE URL MIT KORREKTEM MODELL (2.5-flash)
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
                generationConfig = new { maxOutputTokens = 500, temperature = 0.2 } 
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            return await ExecuteAiRequestAsync(url, jsonPayload);
        }

        // FEATURE 4: Kapitel-Titel generieren
        public async Task<string> GenerateTitleFromTextAsync(string documentText)
        {
            if (string.IsNullOrWhiteSpace(documentText)) return "Neues Kapitel (Kein Text)";

            // SAUBERE URL MIT KORREKTEM MODELL (2.5-flash)
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string truncatedText = documentText.Length > 10000 ? documentText.Substring(0, 10000) : documentText;

            string systemPrompt = "Du bist ein präziser Assistent für Universitätsprofessoren. Deine EINZIGE Aufgabe ist es, einen Titel zu finden. Antworte NIEMALS mit nur einem einzelnen Buchstaben!";

            string promptText = $@"Lies den folgenden Starttext eines Vorlesungs-Skripts. 
            Generiere einen passenden, extrem kurzen Titel (maximal 3 bis 6 Wörter) für dieses Kapitel. 
            Falls im Text eine Kapitelnummer steht (z.B. 'Kapitel 1: Grundlagen'), übernimm diese.
            Antworte AUSSCHLIESSLICH mit dem Titel, ohne Einleitung und ohne Anführungszeichen.
            
            Textauszug:
            {truncatedText}";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = promptText } } } },
                generationConfig = new { maxOutputTokens = 50, temperature = 0.1 } 
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            var result = await ExecuteAiRequestAsync(url, jsonPayload);

            if (string.IsNullOrWhiteSpace(result) || result.Trim().Length <= 1)
            {
                return "Neues Kapitel (Generiert)";
            }

            return result;
        }

        // =========================================================================
        // ADAPTERFASSADE: ORCHESTRIERUNG & JSON-PARSING
        // =========================================================================
        private async Task<string> ExecuteAiRequestAsync(string url, string jsonPayload)
        {
            int maxRetries = 3; 
            int delayMilliseconds = 2500; 

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(url, content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(responseString);
                        
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                        {
                            var firstCandidate = candidates[0];
                            if (firstCandidate.TryGetProperty("content", out var resContent) && 
                                resContent.TryGetProperty("parts", out var parts) && 
                                parts.GetArrayLength() > 0)
                            {
                                var text = parts[0].GetProperty("text").GetString() ?? "";
                                return text.Trim();
                            }
                            else if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                            {
                                throw new Exception($"Google KI hat die Antwort blockiert. Grund: {finishReason.GetString()}");
                            }
                        }
                        return "";
                    }

                    // Auto-Retry bei Überlastung (429) oder Server-Fehler (500+)
                    if (((int)response.StatusCode == 429 || (int)response.StatusCode >= 500) && i < (maxRetries - 1))
                    {
                        await Task.Delay(delayMilliseconds);
                        delayMilliseconds *= 2; 
                        continue; 
                    }

                    var errorContent = await response.Content.ReadAsStringAsync();
                    if ((int)response.StatusCode == 429)
                    {
                        throw new HttpRequestException("Die KI-Schnittstelle ist aktuell stark ausgelastet. Bitte warte einen Moment.");
                    }
                        
                    throw new HttpRequestException($"Gemini API Fehler ({(int)response.StatusCode}): {errorContent}");
                }
                catch (TaskCanceledException)
                {
                    if (i < (maxRetries - 1))
                    {
                        await Task.Delay(delayMilliseconds);
                        delayMilliseconds *= 2;
                        continue;
                    }
                    throw new HttpRequestException("Zeitüberschreitung (Timeout) bei der Anfrage. Das PDF ist möglicherweise zu groß.");
                }
            }

            return string.Empty;
        }
    }
}