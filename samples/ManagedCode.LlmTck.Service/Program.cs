using ManagedCode.LlmTck.Hosting;
using ManagedCode.LlmTck.Models;

var builder = WebApplication.CreateBuilder(args);

var endpoint = builder.Configuration["LlmTck:Endpoint"];
var compatibility = new
{
    openAi = builder.Configuration["LLM_TCK_OPENAI_COMPATIBILITY"],
    azureOpenAi = builder.Configuration["LLM_TCK_AZURE_OPENAI_COMPATIBILITY"],
    microsoftFoundry = builder.Configuration["LLM_TCK_MICROSOFT_FOUNDRY_COMPATIBILITY"],
    anthropic = builder.Configuration["LLM_TCK_ANTHROPIC_COMPATIBILITY"],
    gemini = builder.Configuration["LLM_TCK_GEMINI_COMPATIBILITY"],
    groq = builder.Configuration["LLM_TCK_GROQ_COMPATIBILITY"],
    mistral = builder.Configuration["LLM_TCK_MISTRAL_COMPATIBILITY"],
    ollama = builder.Configuration["LLM_TCK_OLLAMA_COMPATIBILITY"],
    cohere = builder.Configuration["LLM_TCK_COHERE_COMPATIBILITY"],
    bedrock = builder.Configuration["LLM_TCK_BEDROCK_COMPATIBILITY"],
    openRouter = builder.Configuration["LLM_TCK_OPENROUTER_COMPATIBILITY"],
    deepSeek = builder.Configuration["LLM_TCK_DEEPSEEK_COMPATIBILITY"],
    perplexity = builder.Configuration["LLM_TCK_PERPLEXITY_COMPATIBILITY"],
};
var requiredBearerToken = builder.Configuration["LlmTck:RequiredBearerToken"];
builder.Services.AddLlmTck(options =>
{
    options.AddModel("llm-tck-chat", LlmTckModelKind.Chat);
    options.AddModel("llm-tck-embedding", LlmTckModelKind.Embedding);
    options.AddModel("llm-tck-image", LlmTckModelKind.Image);
    options.AddModel("llm-tck-audio", LlmTckModelKind.Audio);

    if (!string.IsNullOrWhiteSpace(requiredBearerToken))
    {
        options.RequireBearerToken(requiredBearerToken);
    }

    options.AddChatScenario(
        "default-blue-whale",
        scenario => scenario
            .ForModel("llm-tck-chat")
            .WhenUserContains("color")
            .Responds("blue whale", "blue ", "whale")
            .Responds("blue whale", "blue ", "whale")
    );
});

var app = builder.Build();

app.MapGet(
    "/",
    () =>
        Results.Ok(
            new
            {
                name = "LLM TCK",
                status = "ready",
                endpoint,
                compatibility,
            }
        )
);
app.MapLlmTck();
app.Run();
