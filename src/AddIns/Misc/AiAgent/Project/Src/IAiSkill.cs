namespace ICSharpCode.AiAgent
{
    public interface IAiSkill
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string SystemMessage { get; }
        bool RequiresEditorCode { get; }
        string BuildPrompt(string userInput, string selectedCode);
    }
}