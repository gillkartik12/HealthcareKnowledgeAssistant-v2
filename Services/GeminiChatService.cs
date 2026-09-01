using System.Text;
using System.Text.Json;
using static HealthcareKnowledgeAssistant.Models.AskRequest;

namespace HealthcareKnowledgeAssistant.Services
{
    public class GeminiChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiChatService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"]
                ?? throw new Exception("Gemini API key missing");
        }
        public async Task<string> GenerateAnswerAsync(
    string question,
    string context)
        {
            var prompt = $"""
                            You are a document assistant.

                            Answer ONLY using the provided context.

                            Rules:
                            1. Use only the context below.
                            2. If the answer is not present, say: "I could not find that information."
                            3. Cite sources using [Source 1], [Source 2], etc.
                            4. Do not invent facts.
                            5. Keep the answer concise.

                            Context:
                            {context}

                            Question:
                            {question}
                        """;

            return await GenerateFromPromptAsync(prompt);
        }
        private async Task<string> GenerateFromPromptAsync(string prompt)
        {
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);

            var response = await _httpClient.PostAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}",
                new StringContent(json, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);

            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";
        }
        public async Task<string> RewriteQuestionAsync(
    string question,
    List<ChatMessage> chatHistory)
        {
            if (chatHistory == null || chatHistory.Count == 0)
                return question;

            var historyText = string.Join(
                "\n\n",
                chatHistory.Select(x =>
                    $"User: {x.Question}\nAssistant: {x.Answer}")
            );

            var prompt = $"""
                    Rewrite the current user question into a standalone question.

                    Use the chat history only to resolve references like:
                    - it
                    - this
                    - that
                    - they
                    - what about
                    - how about

                    Do not answer the question.
                    Return only the rewritten standalone question.

                    Chat History:
                    {historyText}

                    Current Question:
                    {question}
                    """;

            return await GenerateFromPromptAsync(prompt);
        }
        public async Task<string> GenerateHealthcareAnswerAsync(
    string question,
    string context)
        {
            var prompt = $"""
    You are a Healthcare Knowledge Assistant.

    Your job is to answer operational and knowledge-base questions using ONLY the provided healthcare document context.

    Rules:
    1. Use only the provided context.
    2. Do not use outside medical knowledge.
    3. Do not provide diagnosis, treatment, or clinical advice unless the context explicitly states it.
    4. If the answer is not in the context, say:
       "I could not find that information in the uploaded healthcare documents."
    5. Cite sources using [Source 1], [Source 2], etc.
    6. Keep the answer clear and concise.

    Context:
    {context}

    Question:
    {question}
    """;

            return await GenerateFromPromptAsync(prompt);
        }
    }
}
