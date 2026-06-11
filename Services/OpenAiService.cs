using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace WeProject.Services
{
    public class OpenAiService : IOpenAiService
    {
        private readonly string _apiKey;

        public OpenAiService(IConfiguration configuration)
        {
            // Holt sich den Schlüssel sicher aus der appsettings.json
            _apiKey = configuration["OpenAI:ApiKey"] 
                      ?? throw new ArgumentNullException("Der OpenAI API-Schlüssel fehlt!");
        }

        public async Task<string> GenerateQuestionsFromTextAsync(string documentText)
        {
            // Wir nutzen das extrem schnelle und günstige gpt-4o-mini Modell
            ChatClient client = new ChatClient("gpt-4o-mini", _apiKey);

            // 1. Wir sagen der KI, wer sie ist und was wir erwarten (JSON-Format für die Datenbank)
            string systemPrompt = @"Du bist ein strenger Universitätsprofessor. 
            Erstelle aus dem folgenden Text 3 Multiple-Choice-Fragen auf akademischem Niveau. 
            Gib das Ergebnis AUSSCHLIESSLICH als valides JSON-Array zurück, ohne Markdown-Formatierung. 
            Jedes Element im Array muss exakt so aufgebaut sein: 
            { ""frage"": ""..."", ""antworten"": [""A"", ""B"", ""C"", ""D""], ""korrekteAntwortIndex"": 0 }";

            // 2. Wir schicken den Prompt und den PDF-Text an OpenAI
            // FIX: Wir sagen C# ausdrücklich, dass dies ein Array vom Typ "ChatMessage" ist
            ChatCompletion completion = await client.CompleteChatAsync(new ChatMessage[]
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(documentText)
            });

            // 3. Wir geben die Antwort der KI zurück
            return completion.Content[0].Text;
        }
    }
}