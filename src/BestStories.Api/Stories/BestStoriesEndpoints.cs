using System.ComponentModel.DataAnnotations;
using BestStories.Api.Hosting;
using BestStories.Core.Stories;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace BestStories.Api.Stories;

internal static class BestStoriesEndpoints
{
    public static IEndpointRouteBuilder MapBestStoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var stories = app.MapGroup("/api/v1/stories")
            .WithTags("Stories")
            .RequireRateLimiting(RateLimitingExtensions.PolicyName);

        stories.MapGet("/best", GetBestStoriesAsync)
            .WithName("GetBestStories")
            .WithSummary("Returns the best n Hacker News stories ordered by score (descending).")
            .WithDescription(
                "Served from an in-memory snapshot that is refreshed in the background, so the number of calls to Hacker News " +
                "does not grow with the number of callers. Supports conditional requests through ETag / If-None-Match.")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    internal static async Task<Results<Ok<BestStoryResponse[]>, StatusCodeHttpResult, ProblemHttpResult>> GetBestStoriesAsync(
        [Range(BestStoriesLimits.MinCount, BestStoriesLimits.MaxCount)] int count,
        IBestStoriesService bestStoriesService,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await bestStoriesService.GetBestStoriesAsync(count, cancellationToken);

        if (result is null)
        {
            httpContext.Response.Headers.RetryAfter = "5";
            return TypedResults.Problem(
                title: "Best stories are not available yet",
                detail: "The first snapshot from Hacker News is still being loaded. Please retry shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var etag = new EntityTagHeaderValue($"\"{result.ContentHash}-{result.Stories.Length}\"");
        var maxAge = result.NextRefreshAt - timeProvider.GetUtcNow();

        var responseHeaders = httpContext.Response.GetTypedHeaders();
        responseHeaders.ETag = etag;
        responseHeaders.CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = maxAge > TimeSpan.Zero ? TimeSpan.FromSeconds(Math.Ceiling(maxAge.TotalSeconds)) : TimeSpan.Zero,
        };

        var ifNoneMatch = httpContext.Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch.Any(candidate => candidate.Compare(etag, useStrongComparison: false)))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        return TypedResults.Ok(BestStoryResponse.From(result.Stories));
    }
}
