using System.Threading.Tasks;

namespace ICSharpCode.AiAgent.LocalTools
{
    public interface ILocalTool
    {
        string ToolName { get; }
        string Description { get; }
        Task<ToolResult> ExecuteAsync(ToolCallContext context);
    }
}