# HomeworkAgent — ZioNet screening homework

C# / .NET 9 implementation of both tasks.

- **Root project** (`./`) — Task 1, the agent loop written by hand against the raw
  Anthropic Messages API.
- **`bonus/`** — Task 2, the same agent re-implemented with Microsoft Agent Framework (MAF)
  to show what the framework hides.

---

## Task 1 — Hand-rolled agent loop

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

### Files

- `Program.cs` — entry point, reads `ANTHROPIC_API_KEY`, kicks off the agent.
- `AgentLoop.cs` — the loop itself.
- `Tools.cs` — tool definitions (JSON Schema) and the dispatcher that executes them.
- `Schema.cs` — `AgentResponse` plus the matching JSON Schema used by `submit_final_answer`.

### Run

```powershell
$env:ANTHROPIC_API_KEY = 'sk-ant-...'
dotnet run
```

Or with a custom prompt:

```powershell
dotnet run -- "What's the weather in Paris and what is 17 * 23?"
```

### Definition of done

- [x] Multiple sequential tool calls work — the loop keeps going until `submit_final_answer`.
- [x] Final answer is valid JSON matching the schema — guaranteed because Claude returns it
      as the tool's `input` object, which the API validates against `input_schema`.
- [x] Every iteration is logged — see `AgentLoop.Log`.

---

## Task 2 (Bonus) — Same agent, with Microsoft Agent Framework

`bonus/Program.cs` is the same agent re-implemented using
[Microsoft Agent Framework (MAF)](https://github.com/microsoft/agent-framework).

Because MAF doesn't ship a native Anthropic provider, the bonus talks to Claude through
**OpenRouter**, which exposes an OpenAI-compatible API. The OpenAI SDK is pointed at
`https://openrouter.ai/api/v1`, and MAF treats Claude exactly like any other chat model.

### One line on what MAF hid

> **Loop, routing, and state** — the manual `for iteration in 1..N`, the per-iteration
> `messages.Add(...)`, and the `switch (toolName)` dispatcher from Task 1 are all gone.
> `ChatClientBuilder.UseFunctionInvocation()` adds a middleware that runs the entire
> tool-calling loop automatically; `ChatClientAgent` manages the conversation state; and
> `AIFunctionFactory.Create(MyMethod)` generates the JSON Schema from the C# signature
> instead of us writing it by hand.

Side-by-side, the equivalent of ~200 lines from Task 1 becomes ~40 lines of MAF code, and
the place where you can actually see "the loop" is gone.

### Run

```powershell
cd bonus
$env:OPENROUTER_API_KEY = 'sk-or-v1-...'
# optional — defaults to anthropic/claude-haiku-4.5
$env:OPENROUTER_MODEL = 'anthropic/claude-haiku-4.5'
dotnet run
```

To run with a free model that supports tool calling:

```powershell
$env:OPENROUTER_MODEL = 'meta-llama/llama-3.3-70b-instruct:free'
dotnet run
```
