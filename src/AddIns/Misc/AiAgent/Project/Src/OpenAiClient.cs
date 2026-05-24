using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
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
        private string _selectedModel;

        public static readonly Dictionary<AiProvider, string[]> AvailableModels = new()
        {
            { AiProvider.OpenAI, new[] { "LongCat-Flash-Thinking-2601", "gpt-4o", "gpt-4o-mini" } },
            { AiProvider.Anthropic, new[] { "LongCat-Flash-Thinking-2601", "claude-3-5-sonnet-20240620", "claude-3-haiku-20240307" } }
        };

        public string SelectedModel
        {
            get => _selectedModel ?? AvailableModels[_provider][0];
            set => _selectedModel = value;
        }

        public OpenAiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            SetDefaultEndpoint(AiProvider.OpenAI);
        }

        public void Configure(string apiKey, string apiEndpoint = null, AiProvider provider = AiProvider.OpenAI, string model = null)
        {
            _apiKey = apiKey;
            _provider = provider;
            _selectedModel = model;

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
                    _apiEndpoint = "https://api.longcat.chat/v1/chat/completions";
                    break;
                case AiProvider.Anthropic:
                    _apiEndpoint = "https://api.longcat.chat/anthropic/v1/messages";
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
                    break;
            }
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        public async Task<string> SendChatMessageAsync(string prompt, string systemMessage = null, string model = null)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI client is not configured. Please set the API key first.");

            var content = BuildRequestContent(prompt, systemMessage, model, false);
            HttpResponseMessage response = await _httpClient.PostAsync(_apiEndpoint, content);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            return ParseResponse(responseJson);
        }

        public async Task StreamChatMessageAsync(string prompt, Action<string> onChunk, CancellationToken cancellationToken = default, string systemMessage = null, string model = null)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI client is not configured. Please set the API key first.");

            var content = BuildRequestContent(prompt, systemMessage, model, true);
            using var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint)
            {
                Content = content
            };

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

			while (!reader.EndOfStream)
			{
				string? line = await reader.ReadLineAsync();    // ����ÿһ��...}
				Console.WriteLine(@"AI stream line:{0}", line);

				switch (_provider)
				{
					case AiProvider.OpenAI:
						await StreamOpenAiResponseAsync(reader, onChunk, cancellationToken);
						break;
					case AiProvider.Anthropic:
						await StreamAnthropicResponseAsync(line, onChunk, cancellationToken);
						break;
				}
			}
		}

        private async Task StreamOpenAiResponseAsync(StreamReader reader, Action<string> onChunk, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string line = await reader.ReadLineAsync();
                if (line == null)
                    break;

                if (!line.StartsWith("data: "))
                    continue;

                string data = line.Substring(6);
                if (data == "[DONE]")
                    break;

                try
                {
                    dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(data);
                    if (json?.choices != null && json.choices.Count > 0)
                    {
                        string content = json.choices[0].delta?.content;
                        if (!string.IsNullOrEmpty(content))
                        {
                            onChunk(content);
                        }
                    }
                }
                catch
                {
                }
            }
        }

		private async Task<bool> StreamAnthropicResponseAsync(string line, Action<string> onChunk, CancellationToken cancellationToken)
		{
			if (line == null) return false;
			if (!line.StartsWith("data: ")) return false;

			string data = line.Substring(6);

			try
			{
				dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(data);
				string type = json?.type?.ToString();

				if (type == "content_block_delta" && json?.delta?.text != null)
				{
					onChunk(json.delta.text.ToString());
				}
				else if (type == "message_stop")
				{
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		private StringContent BuildRequestContent(string prompt, string systemMessage, string model, bool stream)
        {
            string jsonContent;
            string actualModel = _selectedModel ?? AvailableModels[_provider][0];

            switch (_provider)
            {
                case AiProvider.OpenAI:
                    jsonContent = BuildOpenAiRequestBody(prompt, systemMessage, actualModel, stream);
                    break;
                case AiProvider.Anthropic:
                    jsonContent = BuildAnthropicRequestBody(prompt, systemMessage, actualModel, stream);
                    break;
                default:
                    throw new NotSupportedException("AI provider not supported");
            }

            return new StringContent(jsonContent, Encoding.UTF8, "application/json");
        }

        private string BuildOpenAiRequestBody(string prompt, string systemMessage, string model, bool stream)
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
                max_tokens = 4096,
                stream = stream
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
        }

        private string BuildAnthropicRequestBody(string prompt, string systemMessage, string model, bool stream)
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
                max_tokens = 6000,
                temperature = 0.7,
                system = systemMessage,
                stream = stream,
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
            string systemMessage = $"你是一位精通{language}的中文专家级开发者。任务是生成简洁高效且文档完善的代码，请直接输出代码，不要额外解释。";
            return await SendChatMessageAsync(prompt, systemMessage);
        }

        public async Task<string> ExplainCodeAsync(string code)
        {
            string systemMessage = "你是一位资深的中文软件开发者。请详细解释所提供的代码，包括其功能、核心算法以及潜在的优化空间。";
            string prompt = $"请解释以下代码：\n\n{code}";
            return await SendChatMessageAsync(prompt, systemMessage);
        }

        public async Task<string> OptimizeCodeAsync(string code)
        {
            string systemMessage = "你是一位资深的中文软件开发者。请对所提供的代码进行性能、可读性和最佳实践方面的优化，并说明所做的改进。";
            string prompt = $"请优化以下代码并说明改动：\n\n{code}";
            return await SendChatMessageAsync(prompt, systemMessage);
        }

        public async Task<string> RefactorCodeAsync(string code, string refactoringGoal)
        {
            string systemMessage = "你是一位资深的中文软件开发者。请根据指定的目标对代码进行重构，并说明重构的思路。";
            string prompt = $"请重构以下代码，目标是：{refactoringGoal}\n\n{code}";
            return await SendChatMessageAsync(prompt, systemMessage);
        }

        public async Task<string> DebugCodeAsync(string code, string errorDescription)
        {
            string systemMessage = "你是一位资深的中文调试专家。请分析所提供的代码和错误描述，找出并修复缺陷。";
            string prompt = $"请调试以下代码，错误描述为：\n{errorDescription}\n\n代码：\n{code}";
            return await SendChatMessageAsync(prompt, systemMessage);
        }
    }
}