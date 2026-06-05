using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HomeworkAgent;

public sealed class AgentLoop
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly int _maxIterations;

    public AgentLoop(HttpClient http, string apiKey, string model = "claude-haiku-4-5-20251001", int maxIterations = 10)
    {
        _http = http;
        _model = model;
        _maxIterations = maxIterations;

        _http.DefaultRequestHeaders.Remove("x-api-key");
        _http.DefaultRequestHeaders.Remove("anthropic-version");
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
    }

    public async Task<AgentResponse> RunAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = userPrompt
            }
        };

        var tools = Tools.BuildDefinitions();

        for (var iteration = 1; iteration <= _maxIterations; iteration++)
        {
            Log($"--- Iteration {iteration} ---");

            var request = new JsonObject
            {
                ["model"] = _model,
                ["max_tokens"] = 1024,
                ["system"] = systemPrompt,
                ["tools"] = tools.DeepClone(),
                ["messages"] = messages.DeepClone()
            };

            var response = await SendAsync(request, ct);
            var stopReason = response["stop_reason"]?.GetValue<string>() ?? "";
            var contentBlocks = response["content"]?.AsArray() ?? new JsonArray();

            Log($"stop_reason={stopReason}, content_blocks={contentBlocks.Count}");

            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = contentBlocks.DeepClone()
            });

            var toolUses = contentBlocks
                .OfType<JsonObject>()
                .Where(b => b["type"]?.GetValue<string>() == "tool_use")
                .ToList();

            if (toolUses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Model stopped without calling '{Tools.FinalAnswerToolName}'. stop_reason={stopReason}. " +
                    "Strengthen the system prompt or raise max_tokens.");
            }

            var toolResults = new JsonArray();
            foreach (var toolUse in toolUses)
            {
                var toolName = toolUse["name"]!.GetValue<string>();
                var toolUseId = toolUse["id"]!.GetValue<string>();
                var toolInput = toolUse["input"];

                if (toolName == Tools.FinalAnswerToolName)
                {
                    Log($"final_answer called: id={toolUseId}");
                    return ParseFinalAnswer(toolInput);
                }

                Log($"tool_call: {toolName}({CompactJson(toolInput)})");
                var result = Tools.Execute(toolName, toolInput);
                Log($"tool_result: {result}");

                toolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolUseId,
                    ["content"] = result
                });
            }

            messages.Add(new JsonObject
            {
                ["role"] = "user",
                ["content"] = toolResults
            });
        }

        throw new InvalidOperationException(
            $"Exceeded max_iterations={_maxIterations} without a final answer.");
    }

    private async Task<JsonObject> SendAsync(JsonObject request, CancellationToken ct)
    {
        using var httpResponse = await _http.PostAsJsonAsync(ApiUrl, request, ct);
        var body = await httpResponse.Content.ReadAsStringAsync(ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Anthropic API error {(int)httpResponse.StatusCode}: {body}");
        }

        return JsonNode.Parse(body)!.AsObject();
    }

    private static AgentResponse ParseFinalAnswer(JsonNode? input)
    {
        if (input is not JsonObject obj)
        {
            throw new InvalidOperationException("submit_final_answer was called without a JSON object input.");
        }

        return new AgentResponse
        {
            Summary = obj["summary"]?.GetValue<string>() ?? "",
            Answer = obj["answer"]?.GetValue<string>() ?? "",
            ToolsUsed = obj["tools_used"]?.AsArray()
                .Select(n => n!.GetValue<string>())
                .ToList() ?? new List<string>(),
            Confidence = obj["confidence"]?.GetValue<double>() ?? 0
        };
    }

    private static string CompactJson(JsonNode? node) =>
        node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "null";

    private static void Log(string message) =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
}
