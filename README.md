# HomeworkAgent — Tool calling loop + structured output

Task 1 of the ZioNet screening homework, in C# / .NET 9.

## What it does

A console app that calls Claude in a loop until it produces a structured answer:

1. Defines three mocked tools: `get_weather(city)`, `calculator(expression)`, `get_current_time()`.
2. Defines a fourth tool, `submit_final_answer`, whose `input_schema` IS the response schema
   (`AgentResponse`). This is the recommended pattern for getting reliable structured output
   from Claude.
3. Runs the agent loop: send messages to Claude → execute every `tool_use` block → append
   `tool_result` blocks → repeat. The loop ends when the model calls `submit_final_answer`,
   and the tool's input is returned as the typed `AgentResponse`.
4. Logs every iteration, every tool call, and every tool result to stdout.

The HTTP call to `https://api.anthropic.com/v1/messages` is hand-rolled with `HttpClient` so
the request / response shape is completely visible — no SDK abstractions.

## Files

- `Program.cs` — entry point, reads `ANTHROPIC_API_KEY`, kicks off the agent.
- `AgentLoop.cs` — the loop itself.
- `Tools.cs` — tool definitions (JSON Schema) and the dispatcher that executes them.
- `Schema.cs` — `AgentResponse` plus the matching JSON Schema used by `submit_final_answer`.

## Run

```powershell
$env:ANTHROPIC_API_KEY = 'sk-ant-...'
dotnet run
```

Or with a custom prompt:

```powershell
dotnet run -- "What's the weather in Paris and what is 17 * 23?"
```

## Definition of done (per the homework)

- [x] Multiple sequential tool calls work — the loop keeps going until `submit_final_answer`.
- [x] Final answer is valid JSON matching the schema — guaranteed because Claude returns it
      as the tool's `input` object, which the API validates against `input_schema`.
- [x] Every iteration is logged — see `AgentLoop.Log`.
