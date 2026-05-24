namespace ICSharpCode.AiAgent
{
    public class CodeGenerationSkill : IAiSkill
    {
        public string Id => "generate";
        public string Name => "生成代码";
        public string Description => "根据描述生成代码";
        public string SystemMessage => "你是一位资深的软件开发者。请生成简洁高效且文档完善的代码，直接输出请求的代码即可。如果用户指定了文件名，请使用 <TOOL_CALL> 格式输出工具调用以创建文件。\n\n工具调用格式：\n<TOOL_CALL>\n{\"tool\": \"file_editor\", \"params\": {\"action\": \"create\", \"file_path\": \"路径/文件名.cs\", \"content\": \"代码内容\"}}\n</TOOL_CALL>";
        public bool RequiresEditorCode => false;
        public string BuildPrompt(string userInput, string selectedCode) => userInput;
    }

    public class ExplainCodeSkill : IAiSkill
    {
        public string Id => "explain";
        public string Name => "解释代码";
        public string Description => "解释选中代码的功能和原理";
        public string SystemMessage => "你是一位资深的软件开发者。请详细解释所提供的代码，包括其功能、核心算法以及潜在的优化空间。";
        public bool RequiresEditorCode => true;
        public string BuildPrompt(string userInput, string selectedCode) => $"请解释以下代码：\n\n{selectedCode}";
    }

    public class OptimizeCodeSkill : IAiSkill
    {
        public string Id => "optimize";
        public string Name => "优化代码";
        public string Description => "优化代码性能、可读性和最佳实践";
        public string SystemMessage => "你是一位资深的软件开发者。请对所提供的代码进行性能、可读性和最佳实践方面的优化。请直接输出优化后的完整代码文件，并使用 <TOOL_CALL> 格式输出工具调用以更新文件。\n\n工具调用格式：\n<TOOL_CALL>\n{\"tool\": \"file_editor\", \"params\": {\"action\": \"write\", \"file_path\": \"路径/文件名.cs\", \"content\": \"优化后的完整代码\"}}\n</TOOL_CALL>";
        public bool RequiresEditorCode => true;
        public string BuildPrompt(string userInput, string selectedCode) => $"请优化以下代码，直接输出优化后的完整代码并通过工具调用应用到文件：\n\n{selectedCode}";
    }

    public class RefactorCodeSkill : IAiSkill
    {
        public string Id => "refactor";
        public string Name => "重构代码";
        public string Description => "根据目标重构代码结构";
        public string SystemMessage => "你是一位资深的软件开发者。请根据指定的目标对代码进行重构。请直接输出重构后的完整代码，并使用 <TOOL_CALL> 格式输出工具调用以更新文件。如果重构涉及多个文件，请为每个文件分别输出工具调用。\n\n工具调用格式：\n<TOOL_CALL>\n{\"tool\": \"file_editor\", \"params\": {\"action\": \"write\", \"file_path\": \"路径/文件名.cs\", \"content\": \"重构后的完整代码\"}}\n</TOOL_CALL>";
        public bool RequiresEditorCode => true;
        public string BuildPrompt(string userInput, string selectedCode) => $"请重构以下代码，目标：{userInput}\n\n代码：\n{selectedCode}";
    }

    public class DebugCodeSkill : IAiSkill
    {
        public string Id => "debug";
        public string Name => "调试代码";
        public string Description => "分析代码错误并给出修复方案";
        public string SystemMessage => "你是一位资深的调试专家。请分析所提供的代码和错误描述，找出并修复缺陷。请使用 <TOOL_CALL> 格式输出工具调用以修复代码中的问题。\n\n工具调用格式：\n<TOOL_CALL>\n{\"tool\": \"file_editor\", \"params\": {\"action\": \"edit\", \"file_path\": \"路径/文件名.cs\", \"old_string\": \"有问题的代码\", \"new_string\": \"修复后的代码\"}}\n</TOOL_CALL>";
        public bool RequiresEditorCode => true;
        public string BuildPrompt(string userInput, string selectedCode) => $"请调试以下代码，错误描述：{userInput}\n\n代码：\n{selectedCode}";
    }

    public class LocalToolSkill : IAiSkill
    {
        public string Id => "tool_ops";
        public string Name => "本地代码操作";
        public string Description => "直接在本地文件系统中执行代码读写、修改、创建等操作";
        public string SystemMessage => "你是一位可以直接操作本地文件系统的 AI 编程助手。你具有直接读写、编辑、创建和删除代码文件的能力。\n\n可用工具：\n1. 创建文件：{\"tool\": \"file_editor\", \"params\": {\"action\": \"create\", \"file_path\": \"相对路径/文件名.cs\", \"content\": \"文件内容\"}}\n2. 写入文件：{\"tool\": \"file_editor\", \"params\": {\"action\": \"write\", \"file_path\": \"相对路径/文件名.cs\", \"content\": \"文件内容\"}}\n3. 编辑文件（替换文本）：{\"tool\": \"file_editor\", \"params\": {\"action\": \"edit\", \"file_path\": \"相对路径/文件名.cs\", \"old_string\": \"原文本\", \"new_string\": \"新文本\"}}\n4. 读取文件：{\"tool\": \"file_editor\", \"params\": {\"action\": \"read\", \"file_path\": \"相对路径/文件名.cs\"}}\n\n每次需要操作文件时，请在回复中包含工具调用标记：\n<TOOL_CALL>\n工具 JSON\n</TOOL_CALL>\n\n请根据用户的需求主动进行文件操作。对于代码修改，优先使用 edit 操作进行精确替换；对于新文件，使用 create 操作。";
        public bool RequiresEditorCode => false;
        public string BuildPrompt(string userInput, string selectedCode)
        {
            if (!string.IsNullOrEmpty(selectedCode))
                return $"用户需求：{userInput}\n\n当前选中的代码：\n{selectedCode}\n\n请根据用户需求操作本地文件。如果需要修改当前文件，请使用 edit 操作。";
            return $"用户需求：{userInput}\n\n请根据用户需求操作本地文件系统。";
        }
    }
}