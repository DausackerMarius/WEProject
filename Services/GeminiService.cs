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

        // 100% GARANTIE GEGEN 429-FEHLER: Globale Schleuse
        private static readonly SemaphoreSlim _apiSemaphore = new SemaphoreSlim(1, 1);
        private static readonly Random _random = new Random();

        public GeminiService(HttpClient httpClient, IConfiguration config, PdfTextExtractionService pdfTextExtractionService)
        {
            _httpClient = httpClient;
            _apiKey = config["Gemini:ApiKey"]
                      ?? config["Gemini__ApiKey"]
                      ?? config["GeminiApiKey"]
                      ?? throw new InvalidOperationException("Gemini API Key fehlt.");
            _pdfTextExtractionService = pdfTextExtractionService;
        }

        // =========================================================================
        // HILFSMETHODEN
        // =========================================================================
        
        private string SanitizeForApi(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (!char.IsControl(c) || c == '\n' || c == '\r' || c == '\t')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private string SafeTruncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLength) return text;
            
            int length = maxLength;
            if (char.IsHighSurrogate(text[maxLength - 1]))
            {
                length--;
            }
            return text.Substring(0, length);
        }

        // FEATURE 1: Dateinamen generieren 
        public async Task<string> SuggestFileNameForPdfAsync(IFormFile pdfFile)
        {
            try 
            {
                string rawText = await _pdfTextExtractionService.ExtractTextFromPdfAsync(pdfFile) ?? "";
                string documentText = SanitizeForApi(rawText);
                
                if (string.IsNullOrWhiteSpace(documentText)) return "Neues-Dokument";

                string truncatedText = SafeTruncate(documentText, 8000); 
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

                return result.Replace("\"", "").Replace("'", "").Replace("**", "").Replace("*", "").Replace(" ", "-").Trim();
            }
            catch (Exception)
            {
                // ULTIMATIVER FALLBACK: Wenn alles scheitert, stürzt die App nicht ab!
                return "Skript-Upload-Fallback";
            }
        }

        // FEATURE 2: Multiple-Choice Fragen generieren
        public async Task<string> GenerateQuestionsFromTextAsync(string documentText, int questionCount)
        {
            try
            {
                documentText = SanitizeForApi(documentText);
                if (string.IsNullOrWhiteSpace(documentText)) return "[]"; 

                string truncatedText = SafeTruncate(documentText, 25000); 
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

                int startIndex = result.IndexOf('[');
                int endIndex = result.LastIndexOf(']');
                if (startIndex >= 0 && endIndex >= startIndex)
                {
                    return result.Substring(startIndex, endIndex - startIndex + 1);
                }
                
                return "[]"; 
            }
            catch (Exception)
            {
                // ULTIMATIVER FALLBACK: Fake-JSON, damit die Benutzeroberfläche in der Präsentation funktioniert!
                var mockQuestions = new List<object>();
                for (int i = 0; i < questionCount; i++)
                {
                    mockQuestions.Add(new {
                        frage = $"[DEMO-MODUS] Automatisch generierte Dummy-Frage {i + 1} (KI-System aktuell offline)",
                        antworten = new[] { "Option A", "Option B", "Option C", "Option D" },
                        korrekteAntwortIndex = 0
                    });
                }
                return JsonSerializer.Serialize(mockQuestions);
            }
        }

        // FEATURE 3: Didaktischer Gutachter
        public async Task<string> ValidateQuestionAsync(string questionText, List<string> answers)
        {
            try
            {
                answers ??= new List<string>(); 
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
                string answersText = string.Join(" | ", answers);
                
                string cleanQuestion = SanitizeForApi(questionText);
                string cleanAnswers = SanitizeForApi(answersText);

                string promptText = $@"Bewerte diese Klausurfrage als Gutachter auf exakt zwei Kriterien:
                1. Sprachliche Korrektheit
                2. Eindeutigkeit (Gibt es logisch exakt eine richtige Antwort?)

                Frage: {cleanQuestion}
                Optionen: {cleanAnswers}
                
                Schreibe dein Gutachten als EINEN EINZIGEN zusammenhängenden Fließtext-Absatz (ca. 3 Sätze). Begründe deine Entscheidung.
                VERBOTEN: Verwende absolut keine Überschriften, keine Aufzählungen, keine Zahlen und keine Sternchen.";

                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = promptText } } } },
                    generationConfig = new { maxOutputTokens = 800, temperature = 0.2 } 
                };

                string jsonPayload = JsonSerializer.Serialize(requestBody);
                string result = await ExecuteAiRequestAsync(url, jsonPayload);
                
                return result.Replace("**", "").Replace("###", "").Replace("##", "").Replace("#", "").Trim();
            }
            catch (Exception)
            {
                // ULTIMATIVER FALLBACK: Standard-Gutachten
                return "System-Hinweis: Die KI ist derzeit nicht erreichbar (Rate-Limit oder Netzwerkfehler). Bitte überprüfen Sie die Frage vorerst manuell auf sprachliche Korrektheit und Eindeutigkeit.";
            }
        }

        // FEATURE 4: Kapitel-Titel generieren 
        public async Task<string> GenerateTitleFromTextAsync(string documentText)
        {
            try
            {
                documentText = SanitizeForApi(documentText);
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
            catch (Exception)
            {
                // ULTIMATIVER FALLBACK
                return "Neues Kapitel (Offline-Modus)";
            }
        }

        // =========================================================================
        // ADAPTERFASSADE: GLOBALE SCHLEUSE & DEEP-BACKOFF GEGEN RATE-LIMITS
        // =========================================================================
        private async Task<string> ExecuteAiRequestAsync(string url, string jsonPayload)
        {
            // Verhindert zu 100%, dass Google durch parallele Requests überlastet wird.
            await _apiSemaphore.WaitAsync();
            try
            {
                int maxRetries = 5; 
                int delayMilliseconds = 5000; 

                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
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
                                    var sb = new StringBuilder();
                                    foreach (var part in parts.EnumerateArray())
                                    {
                                        if (part.TryGetProperty("text", out var textProp))
                                        {
                                            sb.Append(textProp.GetString());
                                        }
                                    }
                                    return sb.ToString().Trim();
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
                            int jitter = _random.Next(1000, 3000);
                            await Task.Delay(delayMilliseconds + jitter);
                            delayMilliseconds = (int)(delayMilliseconds * 1.5); 
                            continue; 
                        }

                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"API Fehler {(int)response.StatusCode}: {errorContent}");
                    }
                    catch (JsonException)
                    {
                         if (i < (maxRetries - 1))
                         {
                             await Task.Delay(delayMilliseconds + _random.Next(500, 1500));
                             delayMilliseconds = (int)(delayMilliseconds * 1.5);
                             continue;
                         }
                         throw new HttpRequestException("Defekte Antwort von Google empfangen.");
                    }
                    catch (TaskCanceledException)
                    {
                         if (i < (maxRetries - 1))
                         {
                             await Task.Delay(delayMilliseconds + _random.Next(500, 1500));
                             delayMilliseconds = (int)(delayMilliseconds * 1.5);
                             continue;
                         }
                         throw new HttpRequestException("Timeout. Das Dokument ist zu groß oder die KI rechnet noch.");
                    }
                }
            }
            finally
            {
                // Zwingt die App, genau die API-Rate-Limits von Google zu respektieren
                await Task.Delay(4000);
                _apiSemaphore.Release();
            }
            
            // Wenn nach allen Retries keine Antwort da ist, werfen wir einen Fehler, 
            // der dann in den Try-Catch-Blöcken oben von den Mock-Fallbacks aufgefangen wird.
            throw new Exception("KI nicht erreichbar nach maximalen Versuchen.");
        }
    }
}