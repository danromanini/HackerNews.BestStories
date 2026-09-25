using System.Text.Json;
using System.Text.Json.Serialization;
using BestStories.Api.Stories;

namespace BestStories.Api.Hosting;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(BestStoryResponse[]))]
[JsonSerializable(typeof(BestStoriesUpdate))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;
