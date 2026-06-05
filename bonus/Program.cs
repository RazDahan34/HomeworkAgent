using System.ClientModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

// --- 1. Configuration -------------------------------------------------------

var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("ERROR: set the OPENROUTER_API_KEY environment variable.");
    Console.Error.WriteLine("PowerShell:  $env:OPENROUTER_API_KEY = 'sk-or-v1-...'");
    return 1;
}

var model = Environment.GetEnvironmentVariable("OPENROUTER_MODEL") ?? "anthropic/claude-haiku-4.5";
var userPrompt = args.Length > 0
    ? string.Join(' ', args)
    : "What's the weather in Tel Aviv right now, what time is it, and what is (12 + 8) * 3? " +
      "Give me a single combined answer.";

// --- 2. Build an IChatClient that talks to OpenRouter -----------------------
// OpenRouter exposes an OpenAI-compatible API at https://openrouter.ai/api/v1
// so we use the OpenAI SDK with a custom endpoint.

var openAIClient = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri("https://openrouter.ai/api/v1") });

// .AsIChatClient() bridges the OpenAI SDK's ChatClient into the
// Microsoft.Extensions.AI abstraction that MAF agents consume.
// .UseFunctionInvocation() adds the middleware that auto-runs the tool loop
//   - this is the boilerplate we wrote by hand in Task 1, now in one line.
IChatClient chatClient = new ChatClientBuilder(
        openAIClient.GetChatClient(model).AsIChatClient())
    .UseFunctionInvocation()
    .Build();

// --- 3. Tools as plain C# methods ------------------------------------------
// MAF auto-generates the JSON Schema from parameter names, types, and
// [Description] attributes — no hand-rolled schemas like we had in Task 1.

[Description("Get the current weather for a city. Returns mocked temperature and conditions.")]
static string GetWeather([Description("City name, e.g. 'Tel Aviv'")] string city)
{
    Console.WriteLine($"  [tool] get_weather(city='{city}')");
    var temp = 15 + (Math.Abs(city.GetHashCode()) % 20);
    var conditions = (city.Length % 3) switch
    {
        0 => "sunny",
        1 => "cloudy",
        _ => "light rain"
    };
    return $"{{\"city\":\"{city}\",\"temperature_c\":{temp},\"conditions\":\"{conditions}\"}}";
}

[Description("Evaluate a basic arithmetic expression with + - * / and parentheses.")]
static string Calculator([Description("Arithmetic expression, e.g. '(3 + 4) * 2'")] string expression)
{
    Console.WriteLine($"  [tool] calculator(expression='{expression}')");
    try
    {
        var result = new System.Data.DataTable().Compute(expression, null);
        return $"{{\"expression\":\"{expression}\",\"result\":{Convert.ToDouble(result, CultureInfo.InvariantCulture)}}}";
    }
    catch (Exception ex)
    {
        return $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
    }
}

[Description("Get the current UTC date and time in ISO-8601 format.")]
static string GetCurrentTime()
{
    Console.WriteLine($"  [tool] get_current_time()");
    return $"{{\"utc\":\"{DateTime.UtcNow:O}\"}}";
}

// --- 4. Build the agent ----------------------------------------------------

const string systemPrompt =
    "You are a helpful assistant with access to tools: get_weather, calculator, get_current_time. " +
    "Use the tools whenever the user asks for fresh or computed data. " +
    "When you have everything you need, respond with ONLY a JSON object matching exactly this schema, " +
    "and nothing else (no markdown, no commentary):\n" +
    "{\n" +
    "  \"summary\": string,     // one-sentence summary of the request\n" +
    "  \"answer\": string,      // the complete final answer for the user\n" +
    "  \"tools_used\": string[],// names of every tool you called, in order\n" +
    "  \"confidence\": number   // 0.0 to 1.0\n" +
    "}";

AIAgent agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
    Name = "homework-agent",
    ChatOptions = new ChatOptions
    {
        Instructions = systemPrompt,
        Tools = [
            AIFunctionFactory.Create(GetWeather,      name: "get_weather"),
            AIFunctionFactory.Create(Calculator,      name: "calculator"),
            AIFunctionFactory.Create(GetCurrentTime,  name: "get_current_time")
        ],
        ResponseFormat = ChatResponseFormat.Json
    }
});

// --- 5. Run ---------------------------------------------------------------

Console.WriteLine($"Model: {model}");
Console.WriteLine($"User:  {userPrompt}");
Console.WriteLine();
Console.WriteLine("Running agent (MAF handles the tool loop internally)...");
Console.WriteLine();

try
{
    var response = await agent.RunAsync(userPrompt);

    Console.WriteLine();
    Console.WriteLine("=== Raw model output ===");
    Console.WriteLine(response.Text);

    Console.WriteLine();
    Console.WriteLine("=== Parsed structured response ===");

    // Some models (Claude, Llama) like to wrap JSON in markdown ```json ... ``` fences
    // even when told not to. Strip them defensively before parsing.
    var json = ExtractJson(response.Text!);
    var parsed = JsonSerializer.Deserialize<AgentResponse>(
        json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    Console.WriteLine(JsonSerializer.Serialize(parsed, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }));

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAILED: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

// --- 6. Helpers ------------------------------------------------------------

static string ExtractJson(string text)
{
    text = text.Trim();
    if (text.StartsWith("```"))
    {
        // strip the opening fence line, e.g. ```json
        var firstNewline = text.IndexOf('\n');
        if (firstNewline > 0) text = text[(firstNewline + 1)..];
        // strip the closing fence
        if (text.EndsWith("```")) text = text[..^3];
        text = text.Trim();
    }
    return text;
}

// --- 7. Response schema ----------------------------------------------------
// Same shape as Task 1's AgentResponse — kept in this file for simplicity.

internal sealed class AgentResponse
{
    public string Summary { get; set; } = "";
    public string Answer { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("tools_used")]
    public List<string> ToolsUsed { get; set; } = new();

    public double Confidence { get; set; }
}
