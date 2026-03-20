namespace LangChain.Databases.Postgres;

/// <summary>
/// Distance strategy for vector similarity search.
/// </summary>
public enum DistanceStrategy
{
    /// <summary>Euclidean (L2) distance.</summary>
    Euclidean = 0,

    /// <summary>Cosine distance.</summary>
    Cosine = 1,

    /// <summary>Inner product distance.</summary>
    InnerProduct = 2,
}
