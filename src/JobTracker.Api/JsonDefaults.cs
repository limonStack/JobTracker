using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobTracker.Api;

internal static class JsonDefaults
{
    /// <summary>Как у Minimal API по умолчанию, плюс enum-ы строками: "hr", "inactive".</summary>
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
