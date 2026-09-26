using BestStories.Api.Hosting;
using BestStories.Api.Stories;
using BestStories.Core.Abstractions;
using BestStories.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger => swagger.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "Hacker News Best Stories API",
    Version = "v1",
    Description = "Returns the best n Hacker News stories ordered by score, without overloading the Hacker News API.",
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(swagger => swagger.SwaggerEndpoint("/swagger/v1/swagger.json", "Best Stories API v1"));

app.MapBestStoriesEndpoints();
app.MapHub<BestStoriesHub>(BestStoriesHub.Path);

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(DependencyInjection.ReadinessTag) });

app.MapGet("/", () => TypedResults.Redirect("/swagger")).ExcludeFromDescription();

await app.RunAsync();
