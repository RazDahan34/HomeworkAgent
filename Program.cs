using System.Text.Json;
using HomeworkAgent;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("ERROR: set the ANTHROPIC_API_KEY environment variable before running.");
    Console.Error.WriteLine("PowerShell:  $env:ANTHROPIC_API_KEY = 'sk-ant-...'");
    return 1;
}

var userPrompt = args.Length > 0
    ? string.Join(' ', args)
    : "What's the weather in Tel Aviv right now, what time is it, and what is (12 + 8) * 3? " +
      "Give me a single combined answer.";

const string systemPrompt =
    "You are a helpful assistant with access to the tools: get_weather, calculator, get_current_time. " +
    "Use tools whenever the user asks something that requires fresh or computed data. " +
    "When you have everything you need, you MUST call the 'submit_final_answer' tool exactly once " +
    "to deliver your final response. The 'tools_used' field must list the names of every tool " +
    "you called during this run, in the order you called them. Never produce a plain-text final answer.";

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL");
var agent = string.IsNullOrWhiteSpace(model)
    ? new AgentLoop(http, apiKey)
    : new AgentLoop(http, apiKey, model);

Console.WriteLine($"User: {userPrompt}");
Console.WriteLine();

try
{
    var result = await agent.RunAsync(systemPrompt, userPrompt);

    Console.WriteLine();
    Console.WriteLine("=== Final structured response ===");
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAILED: {ex.Message}");
    return 1;
}
