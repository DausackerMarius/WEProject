using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
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
            // Der API Key wird wie gewohnt sicher aus der Konfiguration geladen
            _apiKey = config["Gemini:ApiKey"]
                      ?? config["Gemini__ApiKey"]
                      ?? config["GeminiApiKey"]
                      ?? throw new InvalidOperationException("Gemini API Key fehlt.");
            _pdfTextExtractionService = pdfTextExtractionService;
        }

        // =========================================================================
        // HILFSMETHODE: 100% sicheres Abschneiden von riesigen PDFs (Anti-Absturz)
        // =========================================================================
        private string SafeTruncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLength) return text;
            
            int length = maxLength;
            // Verhindert, dass Sonderzeichen in der Mitte durchtrennt werden und 400-Fehler auslösen
            if (char.IsHighSurrogate(text[maxLength - 1]))
            {
                length--;
            }
            return text.Substring(0, length);
        }

        // FEATURE 1: Dateinamen generieren 
        public async Task<string> SuggestFileNameForPdfAsync(IFormFile pdfFile)
        {
            string documentText = await _pdfTextExtractionService.ExtractTextFromPdfAsync(pdfFile) ?? "";
            if (string.IsNullOrWhiteSpace(documentText)) return "Neues-Dokument";

            string truncatedText = SafeTruncate(documentText, 10000);
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string promptText = $@"Analysiere den Starttext dieses Vorlesungs-Skripts und generiere einen extrem kurzen Dateinamen (max 3-5 Wörter). 
            Antworte AUSSCHLIESSLICH mit dem Dateinamen, ohne die .pdf-Endung.
            Beispiel: Einfuehrung-in-ASP-NET
            Textauszug: {truncatedText}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = promptText } } } },
                generationConfig = new { maxOutputTokens = 30, temperature = 0.1 } 
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            var result = await ExecuteAiRequestAsync(url, jsonPayload);
            
            if (string.IsNullOrWhiteSpace(result) || result.Trim().Length <= 1) return "Neues-Dokument";

            // C# erzwingt die Namenskonvention des Lastenhefts
            return result.Replace("\"", "").Replace("'", "").Replace("**", "").Replace("*", "").Replace(" ", "-").Trim();
        }

        // FEATURE 2: Multiple-Choice Fragen generieren (Lastenheft-konform)
        public async Task<string> GenerateQuestionsFromTextAsync(string documentText, int questionCount)
        {
            documentText ??= "";
            if (string.IsNullOrWhiteSpace(documentText)) return "[]"; 

            // Text wird auf 100.000 Zeichen gekürzt, damit die API nicht überlastet
            string truncatedText = SafeTruncate(documentText, 100000);
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            string systemPrompt = $@"Du bist ein strenger Universitätsprofessor. Erstelle exakt {questionCount} Multiple-Choice-Fragen. 
            Gib das Ergebnis AUSSCHLIESSLICH als valides JSON-Array zurück. 
            Aufbau: {{ ""frage"": ""..."", ""antworten"": [""A"", ""B"", ""C"", ""D""], ""korrekteAntwortIndex"": 0 }}";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = truncatedText } } } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.3, maxOutputTokens = 8192 }
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            string result = await ExecuteAiRequestAsync(url, jsonPayload);

            // GARANTIE: Zieht zielsicher nur das Array heraus, selbst wenn Markdown-Reste existieren
            int startIndex = result.IndexOf('[');
            int endIndex = result.LastIndexOf(']');
            if (startIndex >= 0 && endIndex >= startIndex)
            {
                return result.Substring(startIndex, endIndex - startIndex + 1);
            }
            
            return "[]"; 
        }

        // FEATURE 3: Didaktischer Gutachter (Exakt nach Lastenheft)
        public async Task<string> ValidateQuestionAsync(string questionText, List<string> answers)
        {
            answers ??= new List<string>(); 
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            string answersText = string.Join(" | ", answers);
            
            string promptText = $@"Bewerte diese Klausurfrage als Gutachter auf exakt zwei Kriterien:
            1. Sprachliche Korrektheit
            2. Eindeutigkeit (Gibt es logisch exakt eine richtige Antwort?)

            Frage: {questionText}
            Optionen: {answersText}
            
            Schreibe dein Gutachten als EINEN EINZIGEN zusammenhängenden Fließtext-Absatz (ca. 3-4 Sätze). Begründe deine Entscheidung.
            VERBOTEN: Verwende absolut keine Überschriften, keine Aufzählungen, keine Zahlen und keine Sternchen.";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = promptText } } } },
                generationConfig = new { maxOutputTokens = 800, temperature = 0.2 } 
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            string result = await ExecuteAiRequestAsync(url, jsonPayload);
            
            // Säubert hartnäckige KI-Formatierungen (Sternchen, Rauten), falls sie doch auftauchen
            return result.Replace("**", "").Replace("###", "").Replace("##", "").Replace("#", "").Trim();
        }

        // FEATURE 4: Kapitel-Titel generieren 
        public async Task<string> GenerateTitleFromTextAsync(string documentText)
        {
            documentText ??= "";
            if (string.IsNullOrWhiteSpace(documentText)) return "Neues Kapitel (Kein Text)";

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            string truncatedText = SafeTruncate(documentText, 10000);

            string promptText = $@"Generiere einen extrem kurzen Titel (max 3-6 Wörter) für diesen Skript-Auszug. 
            Übernehme eine eventuelle Kapitelnummer. Antworte nur mit dem Titel.
            Textauszug: {truncatedText}";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = "Du bist ein präziser Assistent. Antworte niemals mit nur einem Buchstaben!" } } },
                contents = new[] { new { parts = new[] { new { text = promptText } } } },
                generationConfig = new { maxOutputTokens = 50, temperature = 0.1 } 
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            var result = await ExecuteAiRequestAsync(url, jsonPayload);

            if (string.IsNullOrWhiteSpace(result) || result.Trim().Length <= 1) return "Neues Kapitel (Generiert)";

            return result.Replace("\"", "").Replace("**", "").Replace("*", "").Replace("\n", "").Replace("\r", "").Trim();
        }

        // =========================================================================
        // ADAPTERFASSADE: ORCHESTRIERUNG, GC-CLEANUP & ISOLIERTES TIMEOUT
        // =========================================================================
        private async Task<string> ExecuteAiRequestAsync(string url, string jsonPayload)
        {
            int maxRetries = 4; 
            int delayMilliseconds = 3000; 

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    
                    // Isoliertes Timeout verhindert C#-Abstürze bei Dependency Injection
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
                    
                    using var response = await _httpClient.PostAsync(url, content, cts.Token);
                    
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
                                return parts[0].GetProperty("text").GetString() ?? "";
                            }
                            else if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                            {
                                throw new Exception($"KI hat geantwortet, aber Text blockiert: {finishReason.GetString()}");
                            }
                        }
                        return "";
                    }

                    if (((int)response.StatusCode == 429 || (int)response.StatusCode >= 500) && i < (maxRetries - 1))
                    {
                        await Task.Delay(delayMilliseconds);
                        delayMilliseconds = (int)(delayMilliseconds * 1.5); 
                        continue; 
                    }

                    var errorContent = await response.Content.ReadAsStringAsync();
                    if ((int)response.StatusCode == 429) throw new HttpRequestException("KI überlastet. Bitte kurz warten.");
                    throw new HttpRequestException($"API Fehler {(int)response.StatusCode}: {errorContent}");
                }
                catch (JsonException)
                {
                     if (i < (maxRetries - 1))
                     {
                         await Task.Delay(delayMilliseconds);
                         delayMilliseconds = (int)(delayMilliseconds * 1.5);
                         continue;
                     }
                     throw new HttpRequestException("Defekte Antwort von Google empfangen.");
                }
                catch (TaskCanceledException)
                {
                    if (i < (maxRetries - 1))
                    {
                        await Task.Delay(delayMilliseconds);
                        delayMilliseconds = (int)(delayMilliseconds * 1.5);
                        continue;
                    }
                    throw new HttpRequestException("Timeout. Das Dokument ist zu groß oder die KI rechnet noch.");
                }
            }
            return string.Empty;
        }
    }
}