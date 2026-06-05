using System.Text.Json.Nodes;

namespace HomeworkAgent;

public sealed class AgentResponse
{
    public string Summary { get; init; } = "";
    public string Answer { get; init; } = "";
    public List<string> ToolsUsed { get; init; } = new();
    public double Confidence { get; init; }

    public static JsonObject JsonSchema { get; } = new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["summary"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "One-sentence summary of what the user asked."
            },
            ["answer"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The complete final answer for the user."
            },
            ["tools_used"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "Names of every tool that was called during this run."
            },
            ["confidence"] = new JsonObject
            {
                ["type"] = "number",
                ["minimum"] = 0,
                ["maximum"] = 1,
                ["description"] = "Confidence in the answer, 0.0 to 1.0."
            }
        },
        ["required"] = new JsonArray("summary", "answer", "tools_used", "confidence")
    };
}
