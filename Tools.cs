using System.Globalization;
using System.Text.Json.Nodes;

namespace HomeworkAgent;

public static class Tools
{
    public const string FinalAnswerToolName = "submit_final_answer";

    public static JsonArray BuildDefinitions()
    {
        return new JsonArray
        {
            Definition(
                name: "get_weather",
                description: "Get the current weather for a city. Returns a mocked temperature and conditions.",
                inputSchema: new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["city"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "City name, e.g. 'Tel Aviv' or 'Paris'."
                        }
                    },
                    ["required"] = new JsonArray("city")
                }),

            Definition(
                name: "calculator",
                description: "Evaluate a basic arithmetic expression with + - * / and parentheses.",
                inputSchema: new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["expression"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Arithmetic expression, e.g. '(3 + 4) * 2'."
                        }
                    },
                    ["required"] = new JsonArray("expression")
                }),

            Definition(
                name: "get_current_time",
                description: "Get the current UTC date and time in ISO-8601 format.",
                inputSchema: new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject(),
                    ["required"] = new JsonArray()
                }),

            Definition(
                name: FinalAnswerToolName,
                description: "Call this tool exactly once when you have a complete answer. Its input IS the structured response returned to the caller. Do not call any other tools after this.",
                inputSchema: AgentResponse.JsonSchema)
        };
    }

    public static string Execute(string name, JsonNode? input)
    {
        return name switch
        {
            "get_weather" => GetWeather(input?["city"]?.GetValue<string>() ?? ""),
            "calculator" => Calculator(input?["expression"]?.GetValue<string>() ?? ""),
            "get_current_time" => GetCurrentTime(),
            _ => $"Error: unknown tool '{name}'."
        };
    }

    private static JsonObject Definition(string name, string description, JsonObject inputSchema) => new()
    {
        ["name"] = name,
        ["description"] = description,
        ["input_schema"] = inputSchema
    };

    private static string GetWeather(string city)
    {
        // Mocked deterministic "weather" based on the city name length.
        var temp = 15 + (Math.Abs(city.GetHashCode()) % 20);
        var conditions = (city.Length % 3) switch
        {
            0 => "sunny",
            1 => "cloudy",
            _ => "light rain"
        };
        return $"{{\"city\":\"{city}\",\"temperature_c\":{temp},\"conditions\":\"{conditions}\"}}";
    }

    private static string Calculator(string expression)
    {
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

    private static string GetCurrentTime()
    {
        return $"{{\"utc\":\"{DateTime.UtcNow:O}\"}}";
    }
}
