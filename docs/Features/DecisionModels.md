# Native decision fixtures

The provider-neutral `ManagedCode.LlmTck.Decisions` contract supports predicate probabilities, opaque choice IDs and native ordinal scores. Configure a `Decision` model and add an `LlmTckDecisionScenario` through `AddDecisionScenario` (or `UseDecisionScenario` on the control client). Fixtures are repeatable; configuration and returned answers are copied. Optional `ExpectedInput` requires an exact input match; SystemOne inputs use serialized object state. Question IDs and kinds must match the fixture. OpenAI fixture question IDs use `OpenAiDecisionFixtureIds.ForQuestionIndex(index)` for each zero-based request position, so unnamed and duplicate-named native questions are preserved. Choice values must exist in the request. Auth, unknown models, mismatches, configured rate limits and content filtering remain observable failures.

Native routes:

- SystemOne Jev and Kev: `POST /systemone/v1/systemone` with string, object or array `state` and keyed `questions` (`noul`, `choice`, `score`). Model IDs are caller configured (`jev-latest`, `kev-latest`).
- Cloudflare Clef and Clef Flash: `POST /cloudflare/client/v4/accounts/{accountId}/ai/run/@cf/cloudflare/clef` or `clef-flash`. The body uses short matching model names and the SystemOne shape; responses use the Workers AI `result`, `success`, `errors`, `messages` envelope.
- OpenAI: `POST /openai/v1/decisions` with text or user-message array `input`, questions array (`predicate`, `choice`, `score`), named choice values and score level labels. Score probability entries preserve zero-based values and original labels. Usage retains cache and reasoning details.

No route calls a production provider. Models and responses are explicit fixtures, not algorithmic decisions. Scores are preserved as native values and may exceed one for more than two ordinal levels; probabilities and confidence are bounded to [0, 1].

Official contracts reviewed 2026-10-08: [TypeSafe OpenAPI](https://api.typesafe.ai/openapi.json), [Kev source](https://github.com/jaredpalmer/kev/blob/main/kev/api.py), [Clef native API](https://developers.cloudflare.com/workers-ai/models/clef/), [Workers AI REST](https://developers.cloudflare.com/workers-ai/get-started/rest-api/), [OpenAI Decisions](https://developers.openai.com/api/reference/resources/decisions/methods/create/).

Use `AddDecisionModel(id, LlmTckDecisionProvider.TypeSafe/Kev/Cloudflare/OpenAI)` to declare the native profile explicitly, or typed `AddJevLatest`, `AddKevLatest`, `AddClef`, `AddClefFlash`. The control builder mirrors this with `UseDecisionModel`. Provider modes are never inferred from model IDs. TypeSafe supports at most 10 score levels and 255 choices; Kev supports at most 255 choices and score levels. Generic SystemOne fixture models use its shared 255-level limit. Cloudflare routes enforce its 64-question limit, ASCII question ID alphabet/100-character maximum, required nonempty instructions, 2..255 choices and 2..10 score levels. Kev and Cloudflare wire responses round native probabilities and scores to four decimals. SystemOne instructions are optional, predicate criteria are optional, and state can be a string, object or array; criteria descriptions retain JSON content. OpenAI refusal fixtures use `Refused = true` to emit an actual successful HTTP response with a native refusal answer.

OpenAI choices support native strings and booleans. Neutral fixture IDs use canonical JSON literals via `OpenAiDecisionFixtureIds.ForChoiceValue`, so a string `"true"` and boolean `true` stay distinct. SystemOne fixtures keep their native opaque IDs unchanged:

```csharp
var questionId = OpenAiDecisionFixtureIds.ForQuestionIndex(0);
var choiceId = OpenAiDecisionFixtureIds.ForChoiceValue("opaque-id");
var answers = new Dictionary<string, LlmTckDecisionAnswer>
{
    [questionId] = new()
    {
        Kind = LlmTckDecisionKind.Choice,
        Choice = choiceId,
        Probabilities = new() { [choiceId] = 0.9, [OpenAiDecisionFixtureIds.ForChoiceValue(false)] = 0.1 },
    },
};
```

OpenAI input arrays contain only user messages with string content or `input_text` / `input_image` parts. Images must use embedded base64 data URLs, with at most 128 images across messages; external image URLs, audio, files, tools and other message roles are rejected.

OpenAI instructions are required strings; question names and option descriptions are optional. Empty strings are preserved. Native answers return nullable names and retain array order; fixture ordinal IDs never replace native names. Choices require 2..255 distinct typed values; native choice strings have no documented length cap. The 1,048,576-character limit applies to names, instructions, score labels and option descriptions. Score level labels retain their order, including repeated or empty labels.
