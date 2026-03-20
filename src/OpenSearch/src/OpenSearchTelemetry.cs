using System.Diagnostics;

namespace LangChain.Databases.OpenSearch;

/// <summary>
/// Shared telemetry constants and <see cref="ActivitySource"/> for the OpenSearch vector store.
/// </summary>
internal static class OpenSearchTelemetry
{
    public const string ActivitySourceName = "LangChain.Databases.OpenSearch";
    public const string SystemName = "opensearch";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static void SetError(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("error.type", ex.GetType().FullName);
    }
}
