using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ICSharpCode.AiAgent.LocalTools
{
    public class ToolCallParser
    {
        private readonly string _workspaceRoot;
        private readonly List<ToolCallContext> _parsedCalls;
        private string _accumulatedText;

        private static readonly Regex ToolCallRegex = new Regex(
            @"<TOOL_CALL>\s*(\{.*?\})\s*</TOOL_CALL>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex InlineToolRegex = new Regex(
            @"```tool_call\s*\n(\{.*?\})\n```",
            RegexOptions.Singleline | RegexOptions.Compiled);

        public ToolCallParser(string workspaceRoot = null)
        {
            _workspaceRoot = workspaceRoot;
            _parsedCalls = new List<ToolCallContext>();
            _accumulatedText = string.Empty;
        }

        public IReadOnlyList<ToolCallContext> ParsedCalls => _parsedCalls.AsReadOnly();

        public string AccumulatedText => _accumulatedText;

        public void Reset()
        {
            _parsedCalls.Clear();
            _accumulatedText = string.Empty;
        }

        public List<ToolCallContext> ParseChunk(string chunk)
        {
            var foundCalls = new List<ToolCallContext>();
            _accumulatedText += chunk;

            foreach (Match match in ToolCallRegex.Matches(_accumulatedText))
            {
                string json = match.Groups[1].Value;
                try
                {
                    var call = ParseToolCallJson(json);
                    if (call != null)
                    {
                        call.WorkspaceRoot = _workspaceRoot;
                        call.RawCallText = match.Value;
                        if (!_parsedCalls.Contains(call))
                        {
                            _parsedCalls.Add(call);
                            foundCalls.Add(call);
                        }
                    }
                }
                catch
                {
                }
            }

            foreach (Match match in InlineToolRegex.Matches(_accumulatedText))
            {
                string json = match.Groups[1].Value;
                try
                {
                    var call = ParseToolCallJson(json);
                    if (call != null)
                    {
                        call.WorkspaceRoot = _workspaceRoot;
                        call.RawCallText = match.Value;

                        if (!_parsedCalls.Contains(call))
                        {
                            _parsedCalls.Add(call);
                            foundCalls.Add(call);
                        }
                    }
                }
                catch
                {
                }
            }

            return foundCalls;
        }

        public List<ToolCallContext> ParseFinal()
        {
            var remaining = new List<ToolCallContext>();
            foreach (var call in _parsedCalls)
            {
                if (!remaining.Exists(c => c.ToolName == call.ToolName
                    && c.Parameters.GetValueOrDefault2("file_path", "") == call.Parameters.GetValueOrDefault2("file_path", "")))
                {
                    remaining.Add(call);
                }
            }
            return remaining;
        }

        private ToolCallContext ParseToolCallJson(string json)
        {
            var dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (dict == null)
                return null;

            var context = new ToolCallContext();

            if (dict.TryGetValue("tool", out var toolObj))
                context.ToolName = toolObj?.ToString() ?? "file_editor";

            if (dict.TryGetValue("params", out var paramsObj) && paramsObj != null)
            {
                string paramsJson = paramsObj.ToString();
                var parameters = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(paramsJson);
                if (parameters != null)
                {
                    foreach (var kvp in parameters)
                    {
                        context.Parameters[kvp.Key] = kvp.Value?.ToString() ?? "";
                    }
                }
            }

            if (dict.TryGetValue("file_path", out var fpObj))
                context.Parameters["file_path"] = fpObj?.ToString() ?? "";

            if (dict.TryGetValue("content", out var cObj))
                context.Parameters["content"] = cObj?.ToString() ?? "";

            if (dict.TryGetValue("action", out var aObj))
                context.Parameters["action"] = aObj?.ToString() ?? "write";

            if (dict.TryGetValue("old_string", out var osObj))
                context.Parameters["old_string"] = osObj?.ToString() ?? "";

            if (dict.TryGetValue("new_string", out var nsObj))
                context.Parameters["new_string"] = nsObj?.ToString() ?? "";

            return context;
        }
    }
}