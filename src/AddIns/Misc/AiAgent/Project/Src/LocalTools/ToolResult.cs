using System.Collections.Generic;

namespace ICSharpCode.AiAgent.LocalTools
{
    public class ToolResult
    {
        public bool Success { get; set; }
        public string ToolName { get; set; }
        public string Message { get; set; }
        public string FilePath { get; set; }
        public string BackupPath { get; set; }
        public List<string> Details { get; set; } = new List<string>();

        public static ToolResult Ok(string toolName, string message, string filePath = null)
        {
            return new ToolResult
            {
                Success = true,
                ToolName = toolName,
                Message = message,
                FilePath = filePath
            };
        }

        public static ToolResult Fail(string toolName, string message, string filePath = null)
        {
            return new ToolResult
            {
                Success = false,
                ToolName = toolName,
                Message = message,
                FilePath = filePath
            };
        }
    }

    public class ToolCallContext
    {
        public string ToolName { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public string RawCallText { get; set; }
        public string WorkspaceRoot { get; set; }
    }
}