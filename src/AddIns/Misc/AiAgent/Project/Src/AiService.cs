using ICSharpCode.Core;
using System;
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
        private bool _isConfigured;
        private bool _isProcessing;
        private string _statusMessage;
        private AiProvider _provider;

        public static AiService Instance => _instance ?? (_instance = new AiService());

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

        public event PropertyChangedEventHandler PropertyChanged;

        private AiService()
        {
            _synchronizationContext = SynchronizationContext.Current;
            _openAiClient = new OpenAiClient();
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                string apiKey = PropertyService.Get("AiAgent.ApiKey", string.Empty);
                string apiEndpoint = PropertyService.Get("AiAgent.ApiEndpoint", string.Empty);
                string providerStr = PropertyService.Get("AiAgent.Provider", "OpenAI");
                
                if (Enum.TryParse(providerStr, out AiProvider provider))
                {
                    Provider = provider;
                }
                else
                {
                    Provider = AiProvider.OpenAI;
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    _openAiClient.Configure(apiKey, apiEndpoint, Provider);
                    IsConfigured = true;
                    StatusMessage = $"AI Agent configured ({Provider})";
                }
                else
                {
                    StatusMessage = "Please configure API key in Tools > Options";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Configuration error: {ex.Message}";
                LoggingService.Error("AI Agent configuration failed", ex);
            }
        }

        public void Configure(string apiKey, string apiEndpoint = null, AiProvider provider = AiProvider.OpenAI)
        {
            try
            {
                Provider = provider;
                _openAiClient.Configure(apiKey, apiEndpoint, provider);
                IsConfigured = true;
                
                PropertyService.Set("AiAgent.ApiKey", apiKey);
                PropertyService.Set("AiAgent.Provider", provider.ToString());
                if (!string.IsNullOrEmpty(apiEndpoint))
                {
                    PropertyService.Set("AiAgent.ApiEndpoint", apiEndpoint);
                }
                
                StatusMessage = $"AI Agent configured successfully ({provider})";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Configuration failed: {ex.Message}";
                LoggingService.Error("AI Agent configuration failed", ex);
            }
        }

        public async Task<string> GenerateCodeAsync(string prompt, string language = "C#")
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI Agent is not configured");

            IsProcessing = true;
            StatusMessage = "Generating code...";

            try
            {
                string result = await _openAiClient.GenerateCodeAsync(prompt, language).ConfigureAwait(false);
                StatusMessage = "Code generation completed";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                LoggingService.Error("AI Agent code generation failed", ex);
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
                throw new InvalidOperationException("AI Agent is not configured");

            IsProcessing = true;
            StatusMessage = "Analyzing code...";

            try
            {
                string result = await _openAiClient.ExplainCodeAsync(code).ConfigureAwait(false);
                StatusMessage = "Code analysis completed";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                LoggingService.Error("AI Agent code explanation failed", ex);
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
                throw new InvalidOperationException("AI Agent is not configured");

            IsProcessing = true;
            StatusMessage = "Optimizing code...";

            try
            {
                string result = await _openAiClient.OptimizeCodeAsync(code).ConfigureAwait(false);
                StatusMessage = "Code optimization completed";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                LoggingService.Error("AI Agent code optimization failed", ex);
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
                throw new InvalidOperationException("AI Agent is not configured");

            IsProcessing = true;
            StatusMessage = "Refactoring code...";

            try
            {
                string result = await _openAiClient.RefactorCodeAsync(code, goal).ConfigureAwait(false);
                StatusMessage = "Code refactoring completed";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                LoggingService.Error("AI Agent code refactoring failed", ex);
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
                throw new InvalidOperationException("AI Agent is not configured");

            IsProcessing = true;
            StatusMessage = "Debugging code...";

            try
            {
                string result = await _openAiClient.DebugCodeAsync(code, errorDescription).ConfigureAwait(false);
                StatusMessage = "Debugging completed";
                return result;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                LoggingService.Error("AI Agent debugging failed", ex);
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
}
