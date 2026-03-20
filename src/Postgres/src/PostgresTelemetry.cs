using System.Diagnostics;

namespace LangChain.Databases.Postgres;

/// <summary>
/// Shared telemetry constants and <see cref="ActivitySource"/> for the Postgres vector store.
/// </summary>
internal static class PostgresTelemetry
{
    public const string ActivitySourceName = "LangChain.Databases.Postgres";
    public const string SystemName = "postgresql";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static void SetError(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("error.type", ex.GetType().FullName);
    }
}
