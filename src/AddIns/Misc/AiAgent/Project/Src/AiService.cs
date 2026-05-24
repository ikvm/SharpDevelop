using ICSharpCode.AiAgent.LocalTools;
using ICSharpCode.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace ICSharpCode.AiAgent
{
    public class AiService : INotifyPropertyChanged
    {
        private static AiService _instance;
        private readonly OpenAiClient _openAiClient;
        private readonly SynchronizationContext _synchronizationContext;
        private CancellationTokenSource _streamCts;
        private bool _isConfigured;
        private bool _isProcessing;
        private string _statusMessage;
        private AiProvider _provider;
        private string _selectedModel;

        public static AiService Instance => _instance ?? (_instance = new AiService());

        public LocalToolExecutor ToolExecutor { get; private set; }
        public ToolCallParser ToolParser { get; private set; }

        public bool IsConfigured
        {
            get => _isConfigured;
            private set
            {
                _isConfigured = value;
                OnPropertyChanged(nameof(IsConfigured));
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            private set
            {
                _isProcessing = value;
                OnPropertyChanged(nameof(IsProcessing));
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public AiProvider Provider
        {
            get => _provider;
            private set
            {
                _provider = value;
                OnPropertyChanged(nameof(Provider));
            }
        }

        public string SelectedModel
        {
            get => _selectedModel ?? OpenAiClient.AvailableModels[_provider][0];
            set
            {
                _selectedModel = value;
                _openAiClient.SelectedModel = value;
                OnPropertyChanged(nameof(SelectedModel));
                PropertyService.Set("AiAgent.Model", value);
            }
        }

        public Dictionary<AiProvider, string[]> AvailableModels => OpenAiClient.AvailableModels;

        public event PropertyChangedEventHandler PropertyChanged;

        private AiService()
        {
            _synchronizationContext = SynchronizationContext.Current;
            _openAiClient = new OpenAiClient();
            ToolExecutor = new LocalToolExecutor();
            ToolParser = new ToolCallParser();
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                string apiKey = PropertyService.Get("AiAgent.ApiKey", string.Empty);
                string apiEndpoint = PropertyService.Get("AiAgent.ApiEndpoint", string.Empty);
                string providerStr = PropertyService.Get("AiAgent.Provider", "OpenAI");
                string savedModel = PropertyService.Get("AiAgent.Model", string.Empty);

                if (Enum.TryParse(providerStr, out AiProvider provider))
                {
                    Provider = provider;
                }
                else
                {
                    Provider = AiProvider.OpenAI;
                }

                if (!string.IsNullOrEmpty(savedModel))
                {
                    _selectedModel = savedModel;
                    _openAiClient.SelectedModel = savedModel;
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    _openAiClient.Configure(apiKey, apiEndpoint, Provider);
                    IsConfigured = true;
                    StatusMessage = $"AI Agent 已配置 ({Provider})";
                }
                else
                {
                    StatusMessage = "请先在设置中配置 API Key";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"配置错误: {ex.Message}";
                LoggingService.Error("AI Agent 配置失败", ex);
            }
        }

        public void Configure(string apiKey, string apiEndpoint = null, AiProvider provider = AiProvider.OpenAI, string model = null)
        {
            try
            {
                Provider = provider;
                _openAiClient.Configure(apiKey, apiEndpoint, provider, model);
                IsConfigured = true;

                PropertyService.Set("AiAgent.ApiKey", apiKey);
                PropertyService.Set("AiAgent.Provider", provider.ToString());
                if (!string.IsNullOrEmpty(apiEndpoint))
                    PropertyService.Set("AiAgent.ApiEndpoint", apiEndpoint);
                if (!string.IsNullOrEmpty(model))
                {
                    _selectedModel = model;
                    PropertyService.Set("AiAgent.Model", model);
                }

                StatusMessage = $"AI Agent 配置成功 ({provider})";
            }
            catch (Exception ex)
            {
                StatusMessage = $"配置失败: {ex.Message}";
                LoggingService.Error("AI Agent 配置失败", ex);
            }
        }

        public async Task StreamActionAsync(string prompt, string systemMessage, string actionName, ThinkingStreamControl outputControl)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Agent 未配置");

            CancelCurrentStream();
            _streamCts = new CancellationTokenSource();

            IsProcessing = true;
            StatusMessage = $"{actionName}...";
            outputControl.BeginStream(actionName);

            ToolParser.Reset();

            try
            {
                await _openAiClient.StreamChatMessageAsync(
                    prompt,
                    chunk =>
                    {
                        outputControl.AppendChunk(chunk);
                        var toolCalls = ToolParser.ParseChunk(chunk);
                        if (toolCalls.Count > 0)
                        {
                            ExecuteToolCallsAsync(toolCalls, outputControl).FireAndForget();
                        }
                    },
                    _streamCts.Token,
                    systemMessage);

                if (!_streamCts.IsCancellationRequested)
                {
                    var finalCalls = ToolParser.ParseFinal();
                    if (finalCalls.Count > 0 && ToolParser.ParsedCalls.Count == 0)
                    {
                        await ExecuteToolCallsAsync(finalCalls, outputControl);
                    }

                    outputControl.SetCompleted();
                    int toolCount = ToolExecutor.ExecutedCount;
                    string toolMsg = toolCount > 0 ? $" (已执行 {toolCount} 个本地操作)" : "";
                    StatusMessage = $"{actionName} 完成{toolMsg}";
                }
            }
            catch (OperationCanceledException)
            {
                outputControl.Cancel();
                StatusMessage = $"{actionName} 已取消";
            }
            catch (Exception ex)
            {
                outputControl.SetError(ex.Message);
                StatusMessage = $"错误: {ex.Message}";
                LoggingService.Error($"AI Agent {actionName} 失败", ex);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ExecuteToolCallsAsync(List<ToolCallContext> toolCalls, ThinkingStreamControl outputControl)
        {
            foreach (var call in toolCalls)
            {
                if (_streamCts != null && _streamCts.IsCancellationRequested)
                    break;

                outputControl.AppendToolStatus($"▶ 执行工具: {call.ToolName} ({call.Parameters.GetValueOrDefault2("action", "write")}) -> {call.Parameters.GetValueOrDefault2("file_path", "N/A")}");

                var result = await ToolExecutor.ExecuteToolAsync(call);
                if (result.Success)
                {
                    outputControl.AppendToolStatus($"  ✓ {result.Message}");
                }
                else
                {
                    outputControl.AppendToolStatus($"  ✗ {result.Message}");
                }
            }
        }

        public async Task<List<ToolResult>> ExecuteToolCallsAsync(List<ToolCallContext> toolCalls)
        {
            return await ToolExecutor.ExecuteBatchAsync(toolCalls);
        }

        public async Task<bool> RollbackAsync()
        {
            if (ToolExecutor.ExecutedCount == 0)
                return false;

            var results = await ToolExecutor.RollbackAllAsync();
            bool allSuccess = true;
            foreach (var r in results)
            {
                if (!r.Success)
                    allSuccess = false;
            }
            StatusMessage = allSuccess ? "已回滚所有更改" : "部分回滚失败";
            return allSuccess;
        }

        public void CancelCurrentStream()
        {
            if (_streamCts != null)
            {
                _streamCts.Cancel();
                _streamCts.Dispose();
                _streamCts = null;
            }
        }

        public async Task<string> GenerateCodeAsync(string prompt, string language = "C#")
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Agent 未配置");

            IsProcessing = true;
            StatusMessage = "正在生成代码...";

            try
            {
                string result = await _openAiClient.GenerateCodeAsync(prompt, language).ConfigureAwait(false);
                StatusMessage = "代码生成完成";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"错误: {ex.Message}";
                LoggingService.Error("AI Agent 代码生成失败", ex);
                throw;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public async Task<string> ExplainCodeAsync(string code)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Agent 未配置");

            IsProcessing = true;
            StatusMessage = "正在分析代码...";

            try
            {
                string result = await _openAiClient.ExplainCodeAsync(code).ConfigureAwait(false);
                StatusMessage = "代码分析完成";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"错误: {ex.Message}";
                LoggingService.Error("AI Agent 代码解释失败", ex);
                throw;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public async Task<string> OptimizeCodeAsync(string code)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Agent 未配置");

            IsProcessing = true;
            StatusMessage = "正在优化代码...";

            try
            {
                string result = await _openAiClient.OptimizeCodeAsync(code).ConfigureAwait(false);
                StatusMessage = "代码优化完成";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"错误: {ex.Message}";
                LoggingService.Error("AI Agent 代码优化失败", ex);
                throw;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public async Task<string> RefactorCodeAsync(string code, string goal)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Agent 未配置");

            IsProcessing = true;
            StatusMessage = "正在重构代码...";

            try
            {
                string result = await _openAiClient.RefactorCodeAsync(code, goal).ConfigureAwait(false);
                StatusMessage = "代码重构完成";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"错误: {ex.Message}";
                LoggingService.Error("AI Agent 代码重构失败", ex);
                throw;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public async Task<string> DebugCodeAsync(string code, string errorDescription)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Agent 未配置");

            IsProcessing = true;
            StatusMessage = "正在调试代码...";

            try
            {
                string result = await _openAiClient.DebugCodeAsync(code, errorDescription).ConfigureAwait(false);
                StatusMessage = "调试完成";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"错误: {ex.Message}";
                LoggingService.Error("AI Agent 调试失败", ex);
                throw;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler == null)
                return;

            if (_synchronizationContext != null && SynchronizationContext.Current != _synchronizationContext)
            {
                _synchronizationContext.Post(_ => handler(this, new PropertyChangedEventArgs(propertyName)), null);
            }
            else
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    internal static class TaskExtensions
    {
        public static async void FireAndForget(this Task task)
        {
            try
            {
                await task;
            }
            catch
            {
            }
        }
    }
}