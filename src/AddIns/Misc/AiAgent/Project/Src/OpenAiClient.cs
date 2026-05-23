using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ICSharpCode.AiAgent
{
    public class OpenAiClient
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private string _apiEndpoint = "https://api.openai.com/v1/chat/completions";

        public OpenAiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public void Configure(string apiKey, string apiEndpoint = null)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrEmpty(apiEndpoint))
            {
                _apiEndpoint = apiEndpoint;
            }
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        public async Task<string> SendChatMessageAsync(string prompt, string systemMessage = null, string model = "gpt-3.5-turbo")
        {
            if (!IsConfigured)
                throw new InvalidOperationException("OpenAI client is not configured. Please set the API key first.");

            var messages = new List<object>();
            
            if (!string.IsNullOrEmpty(systemMessage))
            {
                messages.Add(new { role = "system", content = systemMessage });
            }
            
            messages.Add(new { role = "user", content = prompt });

            var requestBody = new
            {
                model = model,
                messages = messages,
                temperature = 0.7,
                max_tokens = 4096
            };

            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(_apiEndpoint, content);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            dynamic responseObj = Newtonsoft.Json.JsonConvert.DeserializeObject(responseJson);
            
            return responseObj.choices[0].message.content.ToString();
        }

        public async Task<string> GenerateCodeAsync(string prompt, string language = "C#")
        {
            string systemMessage = $"You are an expert {language} developer. Generate clean, efficient, and well-documented code. Respond only with the code.";
            return await SendChatMessageAsync(prompt, systemMessage);
        }

        public async Task<string> ExplainCodeAsync(string code)
        {
            string systemMessage = "You are an expert software developer. Explain the provided code in detail, including its purpose, key algorithms, and potential improvements.";
            string prompt = $"Explain this code:\n\n{code}";
            return await SendChatMessageAsync(prompt, systemMessage);
        }

        public async Task<string> OptimizeCodeAsync(string code)
        {
            string systemMessage = "You are an expert software developer. Optimize the provided code for performance, readability, and best practices. Explain the changes made.";
            string prompt = $"Optimize this code and explain the changes:\n\n{code}";
            return await SendChatMessageAsync(prompt, systemMessage);
        }

        public async Task<string> RefactorCodeAsync(string code, string refactoringGoal)
        {
            string systemMessage = "You are an expert software developer. Refactor the provided code according to the specified goal. Explain the refactoring approach.";
            string prompt = $"Refactor this code to {refactoringGoal}:\n\n{code}";
            return await SendChatMessageAsync(prompt, systemMessage);
        }

        public async Task<string> DebugCodeAsync(string code, string errorDescription)
        {
            string systemMessage = "You are an expert debugger. Analyze the provided code and error description to identify and fix bugs.";
            string prompt = $"Debug this code with the following error:\n\nError: {errorDescription}\n\nCode:\n{code}";
            return await SendChatMessageAsync(prompt, systemMessage);
        }
    }
}