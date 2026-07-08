using ManagedCode.LlmTck.Control;
using ManagedCode.LlmTck.Hosting;
using ManagedCode.LlmTck.Models;

var builder = WebApplication.CreateBuilder(args);

var aspirePort = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(aspirePort)
    && string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"])
    && string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    if (!int.TryParse(aspirePort, out var port))
    {
        throw new InvalidOperationException("PORT must be a valid integer when supplied.");
    }

    builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(port));
}

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
                admin = LlmTckControlRoutes.Admin,
            }
        )
);
app.MapLlmTck();
app.Run();
