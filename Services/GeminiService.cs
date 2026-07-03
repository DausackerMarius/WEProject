using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace WeProject.Services
{
    public class GeminiService : IOpenAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["Gemini:ApiKey"]
                      ?? config["Gemini__ApiKey"]
                      ?? config["GeminiApiKey"]
                      ?? throw new InvalidOperationException("Gemini API Key fehlt.");
        }

        // 1. Die alte Methode zum Generieren
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
                generationConfig = new { responseMimeType = "application/json" }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Gemini API Fehler: {response.StatusCode}");

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            
            var generatedText = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
            return generatedText.Replace("```json", "").Replace("```", "").Trim();
        }

        // 2. NEU: Die Methode zum Validieren (Prüfen)
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
                contents = new[] { new { parts = new[] { new { text = promptText } } } }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Gemini API Fehler: {response.StatusCode}");

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            
            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "Kein Feedback erhalten.";
        }
    }
}