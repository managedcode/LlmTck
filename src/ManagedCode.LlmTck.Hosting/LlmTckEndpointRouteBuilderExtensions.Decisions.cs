using ManagedCode.LlmTck.Cloudflare;
using ManagedCode.LlmTck.Decisions;
using ManagedCode.LlmTck.OpenAI;
using ManagedCode.LlmTck.Providers;
using ManagedCode.LlmTck.Runtime;
using ManagedCode.LlmTck.SystemOne;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ProviderRoutes = ManagedCode.LlmTck.Providers.LlmTckProviderRouteNamespaces;

namespace ManagedCode.LlmTck.Hosting;

public static partial class LlmTckEndpointRouteBuilderExtensions
{
    private const string _clefModel = "clef";
    private const string _clefFlashModel = "clef-flash";
    private const string _systemOnePath = "/v1/systemone";
    private const string _decisionsPath = "/v1/decisions";
    private const string _clefPath = "/client/v4/accounts/{accountId}/ai/run/@cf/cloudflare/clef";
    private const string _clefFlashPath = "/client/v4/accounts/{accountId}/ai/run/@cf/cloudflare/clef-flash";

    private static void MapDecisionEndpoints(RouteGroupBuilder endpoints)
    {
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.SystemOne, _systemOnePath), CreateSystemOneDecisionAsync)
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.SystemOne.DecisionsCreate);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.OpenAI, _decisionsPath), CreateOpenAiDecisionAsync)
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.OpenAI.DecisionsCreate);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Cloudflare, _clefPath), CreateCloudflareDecisionAsync)
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Cloudflare.ClefDecisionsCreate);
        endpoints.MapPost(ProviderRoutes.ForProvider(ProviderRoutes.Cloudflare, _clefFlashPath), CreateCloudflareDecisionAsync)
            .WithLlmTckProviderOperation(LlmTckProviderOperationIds.Cloudflare.ClefFlashDecisionsCreate);
    }

    private static async Task<IResult> CreateOpenAiDecisionAsync(HttpContext context, ILlmTckRuntime runtime, CancellationToken cancellationToken)
    {
        var read = await ReadJsonAsync<OpenAiDecisionRequest>(context, cancellationToken, OpenAiDecisionMapper.Validate).ConfigureAwait(false);
        if (read.Error is not null) { return read.Error; }
        if (read.Value is null) { return InvalidRequest("Missing decision body."); }
        var result = await runtime.DecideAsync(OpenAiDecisionMapper.ToRequest(read.Value), ReadAccessToken(context), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Results.Json(OpenAiDecisionMapper.ToResponse(read.Value, result))
            : Results.Json(OpenAiWireMapper.ToError(result.ErrorCode!, result.ErrorMessage!), statusCode: result.StatusCode);
    }

    private static Task<IResult> CreateSystemOneDecisionAsync(HttpContext context, ILlmTckRuntime runtime, CancellationToken cancellationToken)
    {
        return CreateSystemOneDecisionCoreAsync(context, runtime, false, cancellationToken);
    }

    private static Task<IResult> CreateCloudflareDecisionAsync(HttpContext context, ILlmTckRuntime runtime, CancellationToken cancellationToken)
    {
        return CreateSystemOneDecisionCoreAsync(context, runtime, true, cancellationToken);
    }

    private static async Task<IResult> CreateSystemOneDecisionCoreAsync(HttpContext context, ILlmTckRuntime runtime, bool cloudflare, CancellationToken cancellationToken)
    {
        var read = await ReadJsonAsync<SystemOneDecisionRequest>(context, message => DecisionError(cloudflare, StatusCodes.Status400BadRequest, message),
            cancellationToken, cloudflare ? CloudflareDecisionValidation.Validate : SystemOneDecisionMapper.Validate).ConfigureAwait(false);
        if (read.Error is not null) { return read.Error; }
        if (read.Value is null) { return DecisionError(cloudflare, StatusCodes.Status400BadRequest, "Missing decision body."); }
        if (cloudflare)
        {
            var expected = context.Request.Path.Value!.EndsWith(_clefFlashModel, StringComparison.Ordinal) ? _clefFlashModel : _clefModel;
            if (read.Value.Model != expected)
            { return DecisionError(true, StatusCodes.Status400BadRequest, "Body model must match the Workers AI route."); }
        }
        var model = runtime.GetModels().FirstOrDefault(item => item.Id == read.Value.Model);
        if (!cloudflare && model?.DecisionProvider == LlmTckDecisionProvider.TypeSafe
            && read.Value.Questions.Values.Any(question => question.Type == SystemOneDecisionTypes.Score && question.Criteria.GetArrayLength() > SystemOneDecisionMapper.JevMaxScoreLevels))
        { return DecisionError(false, StatusCodes.Status400BadRequest, "TypeSafe Jev supports at most 10 score levels."); }
        var result = await runtime.DecideAsync(SystemOneDecisionMapper.ToRequest(read.Value), ReadAccessToken(context), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess) { return DecisionError(cloudflare, result.StatusCode, result.ErrorMessage!); }
        if (result.Answers.Values.Any(answer => answer.Refused)) { return DecisionError(cloudflare, StatusCodes.Status409Conflict, "SystemOne does not support refusal answer fixtures."); }
        var response = SystemOneDecisionMapper.ToResponse(read.Value, result, cloudflare || model?.DecisionProvider == LlmTckDecisionProvider.Kev);
        return cloudflare ? Results.Json(new CloudflareDecisionResponse { Success = true, Result = response }) : Results.Json(response);
    }

    private static IResult DecisionError(bool cloudflare, int status, string message)
    {
        return cloudflare
        ? Results.Json(new CloudflareDecisionResponse { Errors = [new() { Code = status, Message = message }] }, statusCode: status)
        : Results.Json(new { detail = message }, statusCode: status);
    }
}
