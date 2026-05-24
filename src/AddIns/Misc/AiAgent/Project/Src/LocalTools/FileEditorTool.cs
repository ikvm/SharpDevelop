using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ICSharpCode.AiAgent.LocalTools
{
    public class FileEditorTool : ILocalTool
    {
        public string ToolName => "file_editor";
        public string Description => "读取、写入、修改、创建代码文件";

        public async Task<ToolResult> ExecuteAsync(ToolCallContext context)
        {
            string action = context.Parameters.GetValueOrDefault2("action", "write");
            string filePath = context.Parameters.GetValueOrDefault2("file_path", "");

            if (string.IsNullOrEmpty(filePath))
                return ToolResult.Fail(ToolName, "缺少 file_path 参数");

            try
            {
                string resolvedPath = ResolvePath(filePath, context.WorkspaceRoot);

                switch (action.ToLower())
                {
                    case "read":
                        return await ReadFileAsync(resolvedPath);
                    case "write":
                        return await WriteFileAsync(resolvedPath, context);
                    case "edit":
                        return await EditFileAsync(resolvedPath, context);
                    case "create":
                        return await CreateFileAsync(resolvedPath, context);
                    case "insert":
                        return await InsertIntoFileAsync(resolvedPath, context);
                    case "delete":
                        return await DeleteFileAsync(resolvedPath);
                    default:
                        return ToolResult.Fail(ToolName, $"不支持的操作: {action}");
                }
            }
            catch (Exception ex)
            {
                return ToolResult.Fail(ToolName, $"执行失败: {ex.Message}", filePath);
            }
        }

        private string ResolvePath(string filePath, string workspaceRoot)
        {
            if (Path.IsPathRooted(filePath))
                return filePath;

            if (!string.IsNullOrEmpty(workspaceRoot))
                return Path.GetFullPath(Path.Combine(workspaceRoot, filePath));

            return Path.GetFullPath(filePath);
        }

        private async Task<ToolResult> ReadFileAsync(string resolvedPath)
        {
            if (!File.Exists(resolvedPath))
                return ToolResult.Fail(ToolName, $"文件不存在: {resolvedPath}", resolvedPath);

            string content = await Task.Run(() => File.ReadAllText(resolvedPath));
            return new ToolResult
            {
                Success = true,
                ToolName = ToolName,
                Message = $"成功读取文件 ({content.Length} 字符)",
                FilePath = resolvedPath,
                Details = new List<string> { content }
            };
        }

        private async Task<ToolResult> WriteFileAsync(string resolvedPath, ToolCallContext context)
        {
            string content = context.Parameters.GetValueOrDefault2("content", "");

            string backupPath = null;
            if (File.Exists(resolvedPath))
            {
                backupPath = resolvedPath + ".bak";
                File.Copy(resolvedPath, backupPath, true);
            }

            string dir = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await Task.Run(() => File.WriteAllText(resolvedPath, content, Encoding.UTF8));

            return new ToolResult
            {
                Success = true,
                ToolName = ToolName,
                Message = $"已写入文件: {resolvedPath} ({content.Length} 字符)",
                FilePath = resolvedPath,
                BackupPath = backupPath
            };
        }

        private async Task<ToolResult> CreateFileAsync(string resolvedPath, ToolCallContext context)
        {
            string content = context.Parameters.GetValueOrDefault2("content", "");

            string dir = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await Task.Run(() => File.WriteAllText(resolvedPath, content, Encoding.UTF8));

            return ToolResult.Ok(ToolName, $"已创建文件: {resolvedPath} ({content.Length} 字符)", resolvedPath);
        }

        private async Task<ToolResult> EditFileAsync(string resolvedPath, ToolCallContext context)
        {
            if (!File.Exists(resolvedPath))
                return ToolResult.Fail(ToolName, $"文件不存在，无法编辑: {resolvedPath}", resolvedPath);

            string oldString = context.Parameters.GetValueOrDefault2("old_string", "");
            string newString = context.Parameters.GetValueOrDefault2("new_string", "");

            if (string.IsNullOrEmpty(oldString))
                return ToolResult.Fail(ToolName, "edit 操作需要 old_string 参数", resolvedPath);

            string content = await Task.Run(() => File.ReadAllText(resolvedPath));

            if (!content.Contains(oldString))
                return ToolResult.Fail(ToolName, $"在文件中未找到匹配的文本:\n期望: {oldString.Substring(0, Math.Min(50, oldString.Length))}...", resolvedPath);

            string backupPath = resolvedPath + ".bak";
            File.Copy(resolvedPath, backupPath, true);

            string newContent = content.Replace(oldString, newString);
            await Task.Run(() => File.WriteAllText(resolvedPath, newContent, Encoding.UTF8));

            return new ToolResult
            {
                Success = true,
                ToolName = ToolName,
                Message = $"已编辑文件: {resolvedPath} (替换了 {oldString.Length} 字符)",
                FilePath = resolvedPath,
                BackupPath = backupPath
            };
        }

        private async Task<ToolResult> InsertIntoFileAsync(string resolvedPath, ToolCallContext context)
        {
            if (!File.Exists(resolvedPath))
                return ToolResult.Fail(ToolName, $"文件不存在，无法插入: {resolvedPath}", resolvedPath);

            string position = context.Parameters.GetValueOrDefault2("position", "end");
            string code = context.Parameters.GetValueOrDefault2("code", "");

            string backupPath = resolvedPath + ".bak";
            File.Copy(resolvedPath, backupPath, true);

            string content = await Task.Run(() => File.ReadAllText(resolvedPath));

            string newContent;
            if (position.StartsWith("after:"))
            {
                string anchor = position.Substring(6);
                int idx = content.LastIndexOf(anchor);
                if (idx < 0)
                    return ToolResult.Fail(ToolName, $"未找到插入锚点: {anchor}", resolvedPath);
                newContent = content.Insert(idx + anchor.Length, code);
            }
            else if (position.StartsWith("before:"))
            {
                string anchor = position.Substring(7);
                int idx = content.IndexOf(anchor);
                if (idx < 0)
                    return ToolResult.Fail(ToolName, $"未找到插入锚点: {anchor}", resolvedPath);
                newContent = content.Insert(idx, code);
            }
            else
            {
                newContent = content + code;
            }

            await Task.Run(() => File.WriteAllText(resolvedPath, newContent, Encoding.UTF8));

            return new ToolResult
            {
                Success = true,
                ToolName = ToolName,
                Message = $"已插入代码到: {resolvedPath}",
                FilePath = resolvedPath,
                BackupPath = backupPath
            };
        }

        private Task<ToolResult> DeleteFileAsync(string resolvedPath)
        {
            if (!File.Exists(resolvedPath))
                return Task.FromResult(ToolResult.Fail(ToolName, $"文件不存在: {resolvedPath}", resolvedPath));

            string backupPath = resolvedPath + ".bak";
            File.Copy(resolvedPath, backupPath, true);
            File.Delete(resolvedPath);

            return Task.FromResult(new ToolResult
            {
                Success = true,
                ToolName = ToolName,
                Message = $"已删除文件: {resolvedPath}",
                FilePath = resolvedPath,
                BackupPath = backupPath
            });
        }
    }

    internal static class DictionaryExtensions
    {
        public static string GetValueOrDefault2(this Dictionary<string, string> dict, string key, string defaultValue)
        {
            string value;
            if (dict.TryGetValue(key, out value))
                return value;
            return defaultValue;
        }
    }
}