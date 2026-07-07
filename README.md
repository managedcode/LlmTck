# LLM TCK

`ManagedCode.LlmTck` is a deterministic Technology Compatibility Kit for LLM APIs. It gives tests a local provider-compatible server that can be hosted by Aspire, scripted with explicit scenarios, and called through regular HTTP or `Microsoft.Extensions.AI`.

The implemented OpenAI-compatible surface covers chat, streaming chat, models, embeddings, image generation, audio speech fixtures, bearer-token failures, scenario misses, and assertion summaries.
When a bearer token is configured, both provider endpoints and control endpoints require it.

## Packages

| Package | NuGet | Description |
| --- | --- | --- |
| [`ManagedCode.LlmTck`](https://www.nuget.org/packages/ManagedCode.LlmTck) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck) | Provider-neutral scenario runtime and assertion state. |
| [`ManagedCode.LlmTck.OpenAI`](https://www.nuget.org/packages/ManagedCode.LlmTck.OpenAI) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.OpenAI.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.OpenAI) | OpenAI-compatible wire contracts. |
| [`ManagedCode.LlmTck.AzureOpenAI`](https://www.nuget.org/packages/ManagedCode.LlmTck.AzureOpenAI) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.AzureOpenAI.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.AzureOpenAI) | Azure OpenAI compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Foundry`](https://www.nuget.org/packages/ManagedCode.LlmTck.Foundry) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Foundry.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Foundry) | Microsoft Foundry compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Anthropic`](https://www.nuget.org/packages/ManagedCode.LlmTck.Anthropic) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Anthropic.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Anthropic) | Anthropic Messages compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Gemini`](https://www.nuget.org/packages/ManagedCode.LlmTck.Gemini) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Gemini.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Gemini) | Gemini compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Groq`](https://www.nuget.org/packages/ManagedCode.LlmTck.Groq) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Groq.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Groq) | Groq compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Mistral`](https://www.nuget.org/packages/ManagedCode.LlmTck.Mistral) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Mistral.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Mistral) | Mistral compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Ollama`](https://www.nuget.org/packages/ManagedCode.LlmTck.Ollama) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Ollama.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Ollama) | Ollama compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Cohere`](https://www.nuget.org/packages/ManagedCode.LlmTck.Cohere) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Cohere.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Cohere) | Cohere compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Bedrock`](https://www.nuget.org/packages/ManagedCode.LlmTck.Bedrock) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Bedrock.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Bedrock) | Amazon Bedrock compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.OpenRouter`](https://www.nuget.org/packages/ManagedCode.LlmTck.OpenRouter) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.OpenRouter.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.OpenRouter) | OpenRouter compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.DeepSeek`](https://www.nuget.org/packages/ManagedCode.LlmTck.DeepSeek) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.DeepSeek.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.DeepSeek) | DeepSeek compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Perplexity`](https://www.nuget.org/packages/ManagedCode.LlmTck.Perplexity) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Perplexity.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Perplexity) | Perplexity compatibility profile and future wire contracts. |
| [`ManagedCode.LlmTck.Hosting`](https://www.nuget.org/packages/ManagedCode.LlmTck.Hosting) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Hosting.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Hosting) | ASP.NET Core endpoint mapping. |
| [`ManagedCode.LlmTck.Client`](https://www.nuget.org/packages/ManagedCode.LlmTck.Client) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Client.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Client) | Control client plus `IChatClient`, `IEmbeddingGenerator<string, Embedding<float>>`, and `IImageGenerator` implementations. |
| [`ManagedCode.LlmTck.Aspire`](https://www.nuget.org/packages/ManagedCode.LlmTck.Aspire) | [![NuGet](https://img.shields.io/nuget/v/ManagedCode.LlmTck.Aspire.svg)](https://www.nuget.org/packages/ManagedCode.LlmTck.Aspire) | Aspire AppHost extension methods. |

## Provider Packages

Provider packages keep vendor-specific protocol and compatibility metadata out of the provider-neutral runtime. The package surface exists now so applications can select a provider family explicitly; provider-specific DTOs and endpoint mappings are added inside each package as support grows.

| Package | Provider ID | Protocol family | Default endpoint shape |
| --- | --- | --- | --- |
| `ManagedCode.LlmTck.OpenAI` | `openai` | OpenAI | `/v1` |
| `ManagedCode.LlmTck.AzureOpenAI` | `azure-openai` | Azure OpenAI | `/openai/deployments/{deployment}/chat/completions` |
| `ManagedCode.LlmTck.Foundry` | `microsoft-foundry` | Microsoft Foundry | `/models/chat/completions` |
| `ManagedCode.LlmTck.Anthropic` | `anthropic` | Anthropic Messages | `/v1/messages` |
| `ManagedCode.LlmTck.Gemini` | `gemini` | Gemini | `/v1beta/models/{model}:generateContent` |
| `ManagedCode.LlmTck.Groq` | `groq` | Groq | `/openai/v1/chat/completions` |
| `ManagedCode.LlmTck.Mistral` | `mistral` | Mistral | `/v1/chat/completions` |
| `ManagedCode.LlmTck.Ollama` | `ollama` | Ollama | `/api/chat` |
| `ManagedCode.LlmTck.Cohere` | `cohere` | Cohere | `/v2/chat` |
| `ManagedCode.LlmTck.Bedrock` | `bedrock` | Amazon Bedrock | `/model/{modelId}/converse` |
| `ManagedCode.LlmTck.OpenRouter` | `openrouter` | OpenRouter | `/api/v1/chat/completions` |
| `ManagedCode.LlmTck.DeepSeek` | `deepseek` | DeepSeek | `/v1/chat/completions` |
| `ManagedCode.LlmTck.Perplexity` | `perplexity` | Perplexity | `/chat/completions` |

Use a provider profile when code needs a stable package-level declaration without starting a host:

```csharp
using ManagedCode.LlmTck.AzureOpenAI;

var profile = AzureOpenAiCompatibility.Profile;
Console.WriteLine(profile.Id); // azure-openai
```

## Azure OpenAI And Foundry SDKs

Azure OpenAI deployment routes use the deployment name as the TCK model id. API keys sent through the official SDK's `api-key` header are accepted anywhere a bearer token would be accepted.

```csharp
using Azure.AI.OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.ClientModel;

var azure = new AzureOpenAIClient(
    new Uri("http://localhost:5000"),
    new ApiKeyCredential("test-key"));

ChatClient chat = azure.GetChatClient("gpt-4.1-mini");
EmbeddingClient embeddings = azure.GetEmbeddingClient("text-embedding-3-small");

var answer = await chat.CompleteChatAsync(
[
    new UserChatMessage("What is the invoice total?"),
]);

var vector = await embeddings.GenerateEmbeddingAsync("invoice");
```

Microsoft Foundry / Azure AI Inference clients call the root `/chat/completions` and `/embeddings` routes. Set `Model` to the model id configured in LLM TCK.

```csharp
using Azure;
using Azure.AI.Inference;

var endpoint = new Uri("http://localhost:5000");
var credential = new AzureKeyCredential("test-key");

var chat = new ChatCompletionsClient(endpoint, credential);
var embeddings = new EmbeddingsClient(endpoint, credential);

var completion = await chat.CompleteAsync(
    new ChatCompletionsOptions(
    [
        new ChatRequestUserMessage("What is the invoice total?"),
    ])
    {
        Model = "gpt-4.1-mini",
    });

var embedding = await embeddings.EmbedAsync(
    new EmbeddingsOptions(["invoice"])
    {
        Model = "text-embedding-3-small",
    });
```

## Configuration Model

LLM TCK is configured with one `LlmTckConfiguration`.

You can apply that configuration in two places:

- at host startup through `builder.Services.AddLlmTck(options => ...)`
- at test runtime through `LlmTckClient.ConfigureAsync(...)` or `POST /__llm-tck/configure`

Runtime configuration is a replacement, not a merge. Applying a new configuration resets scenario positions and assertion events. The runtime snapshots the configuration, so later mutations to your builder/list objects do not change the running provider.

The default configuration advertises four model IDs:

| Model ID | Kind |
| --- | --- |
| `llm-tck-chat` | Chat |
| `llm-tck-embedding` | Embedding |
| `llm-tck-image` | Image |
| `llm-tck-audio` | Audio |

Model IDs and model kinds are enforced. A chat request to an embedding model, or an embedding request to an unknown model, returns `404` with `llm_tck_unknown_model`.

## ASP.NET Core Startup

The simplest host config is one chat scenario:

```csharp
using ManagedCode.LlmTck.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLlmTck(options =>
{
    options.AddChatScenario(
        "blue-whale",
        scenario => scenario
            .ForModel("llm-tck-chat")
            .WhenUserContains("largest animal")
            .Responds("blue whale", "blue ", "whale"));
});

var app = builder.Build();
app.MapLlmTck();
app.Run();
```

A fuller config can advertise your application's real model names while still serving deterministic fixtures:

```csharp
using ManagedCode.LlmTck.Hosting;
using ManagedCode.LlmTck.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLlmTck(options =>
{
    options.RequireBearerToken("test-key");

    options.AddModel("gpt-4.1-mini", LlmTckModelKind.Chat);
    options.AddModel("text-embedding-3-small", LlmTckModelKind.Embedding);
    options.AddModel("gpt-image-1", LlmTckModelKind.Image);
    options.AddModel("gpt-4o-mini-tts", LlmTckModelKind.Audio);

    options.WithDefaultEmbeddingVector(0.125f, 0.25f, 0.5f, 1.0f);
    options.WithDefaultAudio(File.ReadAllBytes("fixtures/ok.wav"), "audio/wav");

    options.AddChatScenario(
        "invoice-total",
        scenario => scenario
            .ForModel("gpt-4.1-mini")
            .WhenUserContains("invoice total")
            .Responds(
                "{\"total\":42.50}",
                "{\"total\":",
                "42.50",
                "}"));

    options.AddChatScenario(
        "provider-rate-limit",
        scenario => scenario
            .ForModel("gpt-4.1-mini")
            .WhenUserContains("rate limit")
            .Fails(429, "rate_limit_exceeded", "Scripted rate limit from LLM TCK."));

    options.AddChatScenario(
        "slow-response",
        scenario => scenario
            .ForModel("gpt-4.1-mini")
            .WhenUserContains("slow")
            .Responds("done")
            .DelaysBy(250));
});

var app = builder.Build();
app.MapLlmTck();
app.Run();
```

## Aspire AppHost

Install the Aspire integration package in the AppHost:

```bash
dotnet add package ManagedCode.LlmTck.Aspire --version 0.0.5
```

Then add the package-owned TCK resource directly:

```csharp
using ManagedCode.LlmTck.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddLlmTck()
    .WithEndpoint("https://api.example.com/v1")
    .WithOpenAICompatibility()
    .WithApiKey("test-key");

builder.Build().Run();
```

`AddLlmTck()` creates a `LlmTckResource` backed by the versioned container image `ghcr.io/managedcode/llm-tck:0.0.5` and exposes its `http` endpoint. `.WithEndpoint(...)` records the target provider base URL as `LlmTck:Endpoint`, and `.WithApiKey("test-key")` sets `LlmTck:RequiredBearerToken` so both `/v1/*` provider endpoints and `/__llm-tck/*` control endpoints require the same bearer token.

Provider compatibility flags are explicit:

```csharp
builder
    .AddLlmTck("azure-openai")
    .WithEndpoint("https://contoso.openai.azure.com")
    .WithAzureOpenAICompatibility()
    .WithApiKey(apiKey);

builder
    .AddLlmTck("anthropic")
    .WithEndpoint("https://api.anthropic.com")
    .WithAnthropicCompatibility()
    .WithApiKey(apiKey);

builder
    .AddLlmTck("gemini")
    .WithEndpoint("https://generativelanguage.googleapis.com")
    .WithGeminiCompatibility()
    .WithApiKey(apiKey);
```

Other selectors follow the same pattern: `.WithFoundryCompatibility()`, `.WithGroqCompatibility()`, `.WithMistralCompatibility()`, `.WithOllamaCompatibility()`, `.WithCohereCompatibility()`, `.WithBedrockCompatibility()`, `.WithOpenRouterCompatibility()`, `.WithDeepSeekCompatibility()`, and `.WithPerplexityCompatibility()`.

## Chat Scenarios

### Contains Match

`WhenUserContains(...)` adds a user-message contains matcher. All configured match messages must be present somewhere in the actual request.

```csharp
options.AddChatScenario(
    "support-refund",
    scenario => scenario
        .ForModel("gpt-4.1-mini")
        .WhenUserContains("refund")
        .Responds("I can help with a refund."));
```

### Exact Match

Use exact matching when a test must prove the complete prompt contract:

```csharp
using ManagedCode.LlmTck.Scenarios;

options.AddChatScenario(
    "exact-system-contract",
    scenario => scenario
        .ForModel("gpt-4.1-mini")
        .WithExactMatch(
            new LlmTckMessage { Role = "system", Content = "Return JSON only." },
            new LlmTckMessage { Role = "user", Content = "Give me the invoice total." })
        .Responds("{\"total\":42.50}"));
```

### Queued Responses

Each matched request consumes the next response. This makes multi-turn flows deterministic:

```csharp
options.AddChatScenario(
    "two-turn-plan",
    scenario => scenario
        .ForModel("gpt-4.1-mini")
        .WhenUserContains("make a plan")
        .Responds("First draft")
        .Responds("Revised draft"));
```

After the queue is exhausted, the provider returns `409` with `llm_tck_scenario_exhausted`.

### Streaming Chunks

The first `Responds` argument is the non-streaming response. The remaining arguments are the streaming chunks:

```csharp
options.AddChatScenario(
    "streaming-answer",
    scenario => scenario
        .ForModel("gpt-4.1-mini")
        .WhenUserContains("stream this")
        .Responds("blue whale", "blue ", "whale"));
```

For `stream: true`, LLM TCK emits SSE `data:` frames and a final `data: [DONE]` marker. All chunks in one response share the same response id.

### Scripted Errors

Use `Fails(...)` to test client error handling without waiting for a real provider to fail:

```csharp
options.AddChatScenario(
    "content-policy-error",
    scenario => scenario
        .ForModel("gpt-4.1-mini")
        .WhenUserContains("blocked fixture")
        .Fails(400, "content_filter", "The scripted request was rejected."));
```

### Delays And Cancellation

Use `DelaysBy(...)` to test timeout and cancellation behavior:

```csharp
options.AddChatScenario(
    "timeout-path",
    scenario => scenario
        .ForModel("gpt-4.1-mini")
        .WhenUserContains("slow path")
        .Responds("eventual answer")
        .DelaysBy(1_500));
```

If the caller cancels during the delay, the queued response is not consumed.

### Scenario-Specific Token

A scenario can require its own bearer token:

```csharp
options.AddChatScenario(
    "tenant-a-only",
    scenario => scenario
        .ForModel("gpt-4.1-mini")
        .RequireBearerToken("tenant-a-key")
        .WhenUserContains("tenant secret")
        .Responds("tenant-a response"));
```

Use either a global token or per-scenario tokens. If both are configured for a scenario, the same request must satisfy both checks, so different global and scenario tokens intentionally make that scenario unreachable.

## Modalities

### Embeddings

Configure a fixed embedding vector. Every input value receives the same deterministic vector:

```csharp
options.AddModel("text-embedding-3-small", LlmTckModelKind.Embedding);
options.WithDefaultEmbeddingVector(0.01f, 0.02f, 0.03f, 0.04f);
```

```csharp
using ManagedCode.LlmTck.Client;
using Microsoft.Extensions.AI;

using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
using IEmbeddingGenerator<string, Embedding<float>> embeddings =
    new LlmTckEmbeddingGenerator(httpClient, "text-embedding-3-small");

var result = await embeddings.GenerateAsync(["alpha", "beta"]);
```

### Images

Image generation returns a deterministic base64 PNG by default:

```csharp
options.AddModel("gpt-image-1", LlmTckModelKind.Image);
```

```csharp
using ManagedCode.LlmTck.Client;
using Microsoft.Extensions.AI;

using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
using IImageGenerator images = new LlmTckImageGenerator(httpClient, "gpt-image-1");

var image = await images.GenerateAsync(
    new ImageGenerationRequest { Prompt = "compatibility marker" });
```

### Audio

Audio speech returns deterministic bytes with a matching media type. The default fixture is a minimal WAV payload:

```csharp
options.AddModel("gpt-4o-mini-tts", LlmTckModelKind.Audio);
options.WithDefaultAudio(File.ReadAllBytes("fixtures/speech.wav"), "audio/wav");
```

```csharp
var audio = await httpClient.PostAsJsonAsync(
    "/v1/audio/speech",
    new { model = "gpt-4o-mini-tts", input = "hello", voice = "alloy" });

audio.EnsureSuccessStatusCode();
var bytes = await audio.Content.ReadAsByteArrayAsync();
```

## Runtime Reconfiguration

Use runtime reconfiguration when each test needs a different provider script.

### Configure Before A Test

Use `ManagedCode.LlmTck.Client` as the universal test control surface. A test can start or discover the hosted TCK, create one `LlmTckClient`, load all required models, datasets, scripted responses, errors, embeddings, images, and audio fixtures, and then call the provider through regular clients.

```csharp
using ManagedCode.LlmTck.Client;
using Microsoft.Extensions.AI;

using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
var tck = LlmTckClient.Create(httpClient, bearerToken: "test-key");

await tck.ConfigureAsync(
    config => config
        .RequireBearerToken("test-key")
        .UseChatModel("test-chat")
        .UseEmbeddingModel("test-embedding")
        .UseImageModel("test-image")
        .UseAudioModel("test-audio")
        .UseEmbeddingVector(0.9f, 0.8f)
        .UseImageDataUri("data:image/png;base64,Zm9v")
        .UseAudio([1, 2, 3, 4], "audio/test")
        .UseDataset(
            "checkout-flow",
            dataset => dataset
                .AddChatScenario(
                    "invoice-total",
                    scenario => scenario
                        .ForModel("test-chat")
                        .WhenUserContains("invoice total")
                        .Responds("{\"total\":42.50}", "{\"total\":", "42.50", "}"))
                .AddChatScenario(
                    "provider-rate-limit",
                    scenario => scenario
                        .ForModel("test-chat")
                        .WhenUserContains("rate limit")
                        .Fails(429, "rate_limit_exceeded", "Scripted rate limit."))),
    resetFirst: true);

using IChatClient chat = tck.CreateChatClient("test-chat");
using IEmbeddingGenerator<string, Embedding<float>> embeddings =
    tck.CreateEmbeddingGenerator("test-embedding");
using IImageGenerator images = tck.CreateImageGenerator("test-image");

var response = await chat.GetResponseAsync(
[
    new ChatMessage(ChatRole.User, "What is the invoice total?"),
]);

var vector = await embeddings.GenerateAsync(["invoice"]);
var image = await images.GenerateAsync(new ImageGenerationRequest { Prompt = "fixture" });
var audio = await tck.GenerateAudioAsync("test-audio", "speak this");
var assertions = await tck.GetAssertionsAsync();
```

`resetFirst: true` clears previous assertion events and scenario positions before the new configuration is applied. Named datasets are active after configuration; they group scenarios so a test can load a whole conversation set without flattening everything by hand.

### Configure With `LlmTckClient`

```csharp
using ManagedCode.LlmTck.Client;
using ManagedCode.LlmTck.Configuration;
using ManagedCode.LlmTck.Models;

using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
httpClient.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-key");

var control = new LlmTckClient(httpClient);

var configuration = new LlmTckConfigurationBuilder()
    .RequireBearerToken("test-key")
    .AddModel("docs-chat", LlmTckModelKind.Chat)
    .AddModel("docs-embedding", LlmTckModelKind.Embedding)
    .AddChatScenario(
        "docs-blue-whale",
        scenario => scenario
            .ForModel("docs-chat")
            .WhenUserContains("largest animal")
            .Responds("blue whale", "blue ", "whale"))
    .WithDefaultEmbeddingVector(0.125f, 0.25f)
    .Build();

await control.ConfigureAsync(configuration);
```

If the current runtime already has a bearer token, the configure request must use that current token. If the new configuration sets a different token, future provider and control requests must use the new token.

### Configure With Raw JSON

`POST /__llm-tck/configure` accepts readable enum values such as `"chat"` and `"contains"`:

```bash
curl -X POST http://localhost:5000/__llm-tck/configure \
  -H "content-type: application/json" \
  -H "authorization: Bearer test-key" \
  -d '{
    "requiredBearerToken": "test-key",
    "models": [
      { "id": "docs-chat", "kind": "chat" },
      { "id": "docs-embedding", "kind": "embedding" },
      { "id": "docs-image", "kind": "image" },
      { "id": "docs-audio", "kind": "audio" }
    ],
    "chatScenarios": [
      {
        "id": "docs-blue-whale",
        "modelId": "docs-chat",
        "match": {
          "mode": "contains",
          "messages": [
            { "role": "user", "content": "largest animal" }
          ]
        },
        "responses": [
          {
            "content": "blue whale",
            "streamChunks": [ "blue ", "whale" ]
          }
        ]
      }
    ],
    "defaultEmbeddingVector": [ 0.125, 0.25 ],
    "defaultAudioMediaType": "audio/wav"
  }'
```

Then call the configured model:

```bash
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "content-type: application/json" \
  -H "authorization: Bearer test-key" \
  -d '{
    "model": "docs-chat",
    "messages": [
      { "role": "user", "content": "What is the largest animal?" }
    ]
  }'
```

## Microsoft.Extensions.AI

Use the client package when you want tests to call the fake provider through normal `Microsoft.Extensions.AI` abstractions:

```csharp
using ManagedCode.LlmTck.Client;
using Microsoft.Extensions.AI;

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5000"),
};
httpClient.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-key");

using IChatClient chatClient = new LlmTckChatClient(httpClient);

var response = await chatClient.GetResponseAsync(
[
    new ChatMessage(ChatRole.User, "What color is the largest animal?"),
]);

Console.WriteLine(response.Text);
```

When a bearer token is required, prefer creating the modality clients from the control client so the same token is applied consistently:

```csharp
var tck = LlmTckClient.Create(httpClient, bearerToken: "test-key");

using IChatClient chatClient = tck.CreateChatClient("docs-chat");
using IEmbeddingGenerator<string, Embedding<float>> embeddings =
    tck.CreateEmbeddingGenerator("docs-embedding");
using IImageGenerator images = tck.CreateImageGenerator("docs-image");
```

Streaming uses the same client:

```csharp
await foreach (var update in chatClient.GetStreamingResponseAsync(
    [new ChatMessage(ChatRole.User, "stream this")]))
{
    Console.Write(update.Text);
}
```

## Assertions And Reset

The assertion summary is intentionally simple: it tells the test whether requests matched configured scenarios, missed, failed auth, used unknown models, exhausted a response queue, or returned scripted errors.

```csharp
var control = new LlmTckClient(httpClient);
var assertions = await control.GetAssertionsAsync();

if (assertions.Unmatched > 0 || assertions.ModelNotFound > 0)
{
    throw new InvalidOperationException("The application called an unexpected LLM path.");
}
```

Reset clears assertion events and scenario response positions without replacing the current configuration:

```csharp
await control.ResetAsync();
```

## Endpoints

- `GET /v1/models`
- `POST /v1/chat/completions`
- `POST /v1/embeddings`
- `POST /v1/images/generations`
- `POST /v1/audio/speech`
- `GET /__llm-tck/models`
- `GET /__llm-tck/assertions`
- `POST /__llm-tck/configure`
- `POST /__llm-tck/reset`

Audio speech returns a deterministic WAV fixture by default.

When `requiredBearerToken` is configured, all endpoints above require `Authorization: Bearer <token>`.

## Common Test Shapes

### Replace Provider Calls In Integration Tests

1. Start the LLM TCK service in Aspire or an ASP.NET Core test host.
2. Configure the expected scenarios for the test.
3. Point the application under test at the LLM TCK base URL.
4. Run the application flow.
5. Assert `GetAssertionsAsync()` has no unmatched, unknown-model, auth-failed, or exhausted events.

### Test Retry Logic

```csharp
options.AddChatScenario(
    "retry-then-success",
    scenario => scenario
        .ForModel("gpt-4.1-mini")
        .WhenUserContains("retry")
        .Fails(429, "rate_limit_exceeded", "Try again.")
        .Responds("success after retry"));
```

### Test Prompt Contract Drift

```csharp
options.AddChatScenario(
    "exact-contract",
    scenario => scenario
        .ForModel("gpt-4.1-mini")
        .WithExactMatch(
            new LlmTckMessage { Role = "system", Content = "Return JSON only." },
            new LlmTckMessage { Role = "user", Content = "Summarize invoice INV-42." })
        .Responds("{\"summary\":\"paid\"}"));
```

If the application changes the prompt, the request becomes unmatched and the test can fail on the assertion summary.

### Test Wrong Model Wiring

Configure only the models your application is allowed to call:

```csharp
options.AddModel("approved-chat-model", LlmTckModelKind.Chat);
```

If the app calls `old-chat-model` or calls `approved-chat-model` through the embeddings endpoint, LLM TCK returns `llm_tck_unknown_model`.

## Build And Test

```bash
dotnet restore ManagedCode.LlmTck.slnx
dotnet build ManagedCode.LlmTck.slnx --configuration Release --no-restore
dotnet test tests/ManagedCode.LlmTck.Tests/ManagedCode.LlmTck.Tests.csproj --configuration Release --no-build --verbosity normal
for project in src/*/*.csproj; do dotnet pack "$project" --configuration Release --no-build --output artifacts/packages; done
```

`global.json` opts `dotnet test` into `Microsoft.Testing.Platform` for .NET 10.
