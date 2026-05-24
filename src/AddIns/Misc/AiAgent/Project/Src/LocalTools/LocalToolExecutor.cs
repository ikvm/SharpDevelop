using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ICSharpCode.AiAgent.LocalTools
{
    public class LocalToolExecutor
    {
        private readonly Dictionary<string, ILocalTool> _tools;
        private readonly Stack<RollbackEntry> _rollbackStack;
        private bool _enableRollback;

        public LocalToolExecutor()
        {
            _tools = new Dictionary<string, ILocalTool>();
            _rollbackStack = new Stack<RollbackEntry>();
            _enableRollback = true;

            RegisterTool(new FileEditorTool());
        }

        public bool EnableRollback
        {
            get => _enableRollback;
            set => _enableRollback = value;
        }

        public int ExecutedCount => _rollbackStack.Count;

        public void RegisterTool(ILocalTool tool)
        {
            _tools[tool.ToolName] = tool;
        }

        public async Task<ToolResult> ExecuteToolAsync(ToolCallContext context)
        {
            if (!_tools.TryGetValue(context.ToolName, out var tool))
            {
                tool = _tools.Values.FirstOrDefault();
                if (tool == null)
                    return ToolResult.Fail(context.ToolName, $"未注册的工具: {context.ToolName}");
            }

            var result = await tool.ExecuteAsync(context);

            if (_enableRollback && result.Success && !string.IsNullOrEmpty(result.BackupPath))
            {
                _rollbackStack.Push(new RollbackEntry
                {
                    FilePath = result.FilePath,
                    BackupPath = result.BackupPath,
                    ToolName = result.ToolName,
                    Action = result.Message
                });
            }

            return result;
        }

        public async Task<List<ToolResult>> ExecuteBatchAsync(List<ToolCallContext> contexts)
        {
            var results = new List<ToolResult>();
            foreach (var context in contexts)
            {
                var result = await ExecuteToolAsync(context);
                results.Add(result);

                if (!result.Success)
                    break;
            }
            return results;
        }

        public async Task<List<ToolResult>> RollbackAllAsync()
        {
            var results = new List<ToolResult>();

            while (_rollbackStack.Count > 0)
            {
                var entry = _rollbackStack.Pop();
                try
                {
                    if (File.Exists(entry.BackupPath))
                    {
                        if (File.Exists(entry.FilePath))
                            File.Delete(entry.FilePath);
                        File.Move(entry.BackupPath, entry.FilePath);

                        results.Add(ToolResult.Ok("rollback", $"已回滚: {entry.FilePath}", entry.FilePath));
                    }
                    else
                    {
                        results.Add(ToolResult.Fail("rollback", $"备份文件不存在: {entry.BackupPath}", entry.FilePath));
                    }
                }
                catch (Exception ex)
                {
                    results.Add(ToolResult.Fail("rollback", $"回滚失败: {ex.Message}", entry.FilePath));
                }
            }

            return results;
        }

        public void ClearRollbackStack()
        {
            _rollbackStack.Clear();
        }

        public void CleanupBackups()
        {
            foreach (var entry in _rollbackStack)
            {
                try
                {
                    if (File.Exists(entry.BackupPath))
                        File.Delete(entry.BackupPath);
                }
                catch
                {
                }
            }
            _rollbackStack.Clear();
        }

        public class RollbackEntry
        {
            public string FilePath { get; set; }
            public string BackupPath { get; set; }
            public string ToolName { get; set; }
            public string Action { get; set; }
        }
    }
}