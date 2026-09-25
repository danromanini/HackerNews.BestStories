using BestStories.Api.Hosting;
using BestStories.Api.Stories;
using BestStories.Core.Abstractions;
using BestStories.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

builder.Services.AddBestStories();
builder.Services.AddSingleton<IStorySnapshotObserver, SignalRSnapshotBroadcaster>();
builder.Services.AddApiRateLimiting();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));
builder.Services.AddSignalR()
    .AddJsonProtocol(json => json.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));
builder.Services.AddOpenApi(openApi => openApi.AddDocumentTransformer((document, _, _) =>
{
    document.Info = new OpenApiInfo
    {
        Title = "Hacker News Best Stories API",
        Version = "v1",
        Description = "Returns the best n Hacker News stories ordered by score, without overloading the Hacker News API.",
    };
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference(scalar => scalar.WithTitle("Best Stories API"));

app.MapBestStoriesEndpoints();
app.MapHub<BestStoriesHub>(BestStoriesHub.Path);

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(DependencyInjection.ReadinessTag) });

app.MapGet("/", () => TypedResults.Redirect("/scalar")).ExcludeFromDescription();

await app.RunAsync();
