using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ICSharpCode.AiAgent
{
    public enum AiProvider
    {
        OpenAI,
        Anthropic
    }

    public class OpenAiClient
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private string _apiEndpoint;
        private AiProvider _provider;

        public OpenAiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            SetDefaultEndpoint(AiProvider.OpenAI);
        }

        public void Configure(string apiKey, string apiEndpoint = null, AiProvider provider = AiProvider.OpenAI)
        {
            _apiKey = apiKey;
            _provider = provider;
            
            if (!string.IsNullOrEmpty(apiEndpoint))
            {
                _apiEndpoint = apiEndpoint;
            }
            else
            {
                SetDefaultEndpoint(provider);
            }
            
            ConfigureAuthentication();
        }

        private void SetDefaultEndpoint(AiProvider provider)
        {
            switch (provider)
            {
                case AiProvider.OpenAI:
                    //_apiEndpoint = "https://api.openai.com/v1/chat/completions";
                    _apiEndpoint = "https://api.longcat.chat/v1/chat/completions";
					break;
                case AiProvider.Anthropic:
                    //_apiEndpoint = "https://api.anthropic.com/v1/messages";
                    _apiEndpoint = "https://api.longcat.chat/anthropic//v1/messages";
                    break;
            }
        }

        private void ConfigureAuthentication()
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            
            foreach (var header in _httpClient.DefaultRequestHeaders.Where(h => h.Key == "X-API-Key").ToList())
            {
                _httpClient.DefaultRequestHeaders.Remove(header.Key);
            }
            
            switch (_provider)
            {
                case AiProvider.OpenAI:
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    break;
                case AiProvider.Anthropic:
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    //_httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
                    //_httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                    break;
            }
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        public async Task<string> SendChatMessageAsync(string prompt, string systemMessage = null, string model = null)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI client is not configured. Please set the API key first.");

            string jsonContent;
            
            switch (_provider)
            {
                case AiProvider.OpenAI:
                    //jsonContent = BuildOpenAiRequestBody(prompt, systemMessage, model ?? "gpt-3.5-turbo");
                    jsonContent = BuildOpenAiRequestBody(prompt, systemMessage, model ?? "LongCat-Flash-Lite");
                    break;
                case AiProvider.Anthropic:
                    //jsonContent = BuildAnthropicRequestBody(prompt, systemMessage, model ?? "claude-3-code-3-5-sonnet-20240620");
                    jsonContent = BuildAnthropicRequestBody(prompt, systemMessage, model ?? "LongCat-Flash-Lite");
                    break;
                default:
                    throw new NotSupportedException("AI provider not supported");
            }

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(_apiEndpoint, content);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            return ParseResponse(responseJson);
        }

        private string BuildOpenAiRequestBody(string prompt, string systemMessage, string model)
        {
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

            return Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
        }

        private string BuildAnthropicRequestBody(string prompt, string systemMessage, string model)
        {
            var content = new List<object>();
            
            if (!string.IsNullOrEmpty(systemMessage))
            {
                content.Add(new { type = "text", text = systemMessage });
            }
            
            content.Add(new { type = "text", text = prompt });

            var requestBody = new
            {
                model = model,
                max_tokens = 4096,
                temperature = 0.7,
                system = systemMessage,
                messages = new[]
                {
                    new { role = "user", content = content }
                }
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
        }

        private string ParseResponse(string responseJson)
        {
            dynamic responseObj = Newtonsoft.Json.JsonConvert.DeserializeObject(responseJson);
            
            switch (_provider)
            {
                case AiProvider.OpenAI:
                    return responseObj.choices[0].message.content.ToString();
                case AiProvider.Anthropic:
                    if (responseObj.content != null && responseObj.content.Count > 0)
                    {
                        return responseObj.content[0].text.ToString();
                    }
                    return responseObj.completion?.ToString() ?? string.Empty;
                default:
                    return string.Empty;
            }
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
