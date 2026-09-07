using System.Text.Json;
using ManagedCode.LlmTck.Gemini;
using ManagedCode.LlmTck.Scenarios;

namespace ManagedCode.LlmTck.Tests.Regression;

public sealed class FollowUpSchemaTests
{
    [Test]
    [Arguments("minItems", "[]", "[1]", "ARRAY")]
    [Arguments("maxItems", "[1,2]", "[1]", "ARRAY")]
    [Arguments("minProperties", "{}", "{\"x\":1}", "OBJECT")]
    [Arguments("maxProperties", "{\"x\":1,\"y\":2}", "{\"x\":1}", "OBJECT")]
    [Arguments("minLength", "\"\"", "\"x\"", "STRING")]
    [Arguments("maxLength", "\"xx\"", "\"x\"", "STRING")]
    public async Task GeminiStringLimits_RejectOutOfRangeFixturesAsync(string keyword, string invalid, string valid, string type)
    {
        var schema = GeminiSchemaMapper.ToJsonSchema(JsonSerializer.Deserialize<JsonElement>($$"""{"type":"{{type}}","{{keyword}}":"1"}"""))!;
        await Assert.That(LlmTckJsonSchema.Matches(invalid, schema)).IsFalse();
        await Assert.That(LlmTckJsonSchema.Matches(valid, schema)).IsTrue();
    }

    [Test]
    public async Task GeminiNullable_HandlesEnumsAlternativesAndLeavesExampleDataUntouchedAsync()
    {
        var source = JsonSerializer.Deserialize<JsonElement>("""{"type":"STRING","nullable":true,"enum":["Paris"],"default":{"type":"OBJECT","minItems":"1"},"example":{"type":"STRING"}}""");
        var mapped = GeminiSchemaMapper.ToJsonSchema(source)!;
        await Assert.That(LlmTckJsonSchema.Matches("null", mapped)).IsTrue();
        await Assert.That(LlmTckJsonSchema.Matches("\"Paris\"", mapped)).IsTrue();
        await Assert.That(LlmTckJsonSchema.Matches("\"London\"", mapped)).IsFalse();
        var document = JsonSerializer.Deserialize<JsonElement>(mapped);
        await Assert.That(document.GetProperty("anyOf")[0].GetProperty("default").GetProperty("type").GetString()).IsEqualTo("OBJECT");
        await Assert.That(document.GetProperty("anyOf")[0].GetProperty("default").GetProperty("minItems").GetString()).IsEqualTo("1");
        var alternatives = GeminiSchemaMapper.ToJsonSchema(JsonSerializer.Deserialize<JsonElement>("""{"nullable":true,"anyOf":[{"type":"INTEGER","minimum":1,"maximum":2},{"type":"STRING","enum":["Paris"]}]}"""))!;
        foreach (var valid in new[] { "null", "1", "\"Paris\"" }) { await Assert.That(LlmTckJsonSchema.Matches(valid, alternatives)).IsTrue(); }
        foreach (var invalid in new[] { "3", "true", "\"London\"" }) { await Assert.That(LlmTckJsonSchema.Matches(invalid, alternatives)).IsFalse(); }
        await Assert.That(GeminiSchemaMapper.ToJsonSchema(null)).IsNull();
    }

    [Test]
    [Arguments("true")]
    [Arguments("{\"type\":\"ARRAY\",\"minItems\":\"-1\"}")]
    [Arguments("{\"type\":\"ARRAY\",\"minItems\":\"9223372036854775808\"}")]
    public async Task GeminiInvalidSchema_ReturnsValidationFailureBeforeMappingAsync(string schema)
    {
        var request = JsonSerializer.Deserialize<JsonElement>("{\"generationConfig\":{\"responseSchema\":" + schema + "}}");
        var validation = GeminiRequestValidation.Validate(request);
        await Assert.That(validation.IsValid).IsFalse();
        await Assert.That(validation.Error).IsNotNull();
    }

    [Test]
    public async Task GeminiJsonSchema_IsNotConvertedAsOpenApiSchemaAsync()
    {
        var request = JsonSerializer.Deserialize<GeminiGenerateContentRequest>("""{"generationConfig":{"responseJsonSchema":{"type":["string","null"],"minLength":2}},"tools":[{"functionDeclarations":[{"name":"weather","parameters":{"type":"OBJECT","properties":{"city":{"type":"STRING","nullable":true}}}}]}]}""")!;
        var mapped = GeminiWireMapper.ToRuntimeRequest("gpt-4.1-mini", request, false);
        await Assert.That(mapped.ResponseSchemaJson).IsEqualTo("{\"type\":[\"string\",\"null\"],\"minLength\":2}");
        await Assert.That(LlmTckJsonSchema.Matches("{\"city\":null}", mapped.Tools.Single().ParametersJson)).IsTrue();
        await Assert.That(LlmTckJsonSchema.Matches("{\"city\":42}", mapped.Tools.Single().ParametersJson)).IsFalse();
    }
}
