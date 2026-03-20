using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.VectorData;

namespace LangChain.Databases.Postgres;

/// <summary>
/// Maps between MEVA-attributed <typeparamref name="TRecord"/> and <see cref="EmbeddingTableRecord"/>.
/// </summary>
[RequiresDynamicCode("Requires dynamic code.")]
[RequiresUnreferencedCode("Requires unreferenced code.")]
internal sealed class PostgresRecordMapper<TRecord> where TRecord : class
{
    private readonly PropertyInfo _keyProperty;
    private readonly PropertyInfo? _contentProperty;
    private readonly PropertyInfo? _vectorProperty;
    private readonly List<PropertyInfo> _dataProperties;
    private readonly int _vectorDimensions;

    public PostgresRecordMapper(VectorStoreCollectionDefinition? definition)
    {
        var type = typeof(TRecord);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Find key property
        _keyProperty = properties.FirstOrDefault(p => p.GetCustomAttribute<VectorStoreKeyAttribute>() != null)
            ?? throw new InvalidOperationException(
                $"Type '{type.Name}' must have a property with [VectorStoreKey] attribute.");

        // Find vector property
        _vectorProperty = properties.FirstOrDefault(p => p.GetCustomAttribute<VectorStoreVectorAttribute>() != null);

        // Priority: definition override > attribute on TRecord > default
        var fromDefinition = definition?.Properties
            .OfType<VectorStoreVectorProperty>()
            .FirstOrDefault()?.Dimensions;
        var vectorAttr = _vectorProperty?.GetCustomAttribute<VectorStoreVectorAttribute>();
        _vectorDimensions = fromDefinition is > 0
            ? fromDefinition.Value
            : vectorAttr?.Dimensions is > 0
                ? vectorAttr.Dimensions
                : 1536;

        // Find data properties (those with [VectorStoreData])
        _dataProperties = properties
            .Where(p => p.GetCustomAttribute<VectorStoreDataAttribute>() != null)
            .ToList();

        // Try to identify the "content" / "text" property among data properties
        _contentProperty = _dataProperties.FirstOrDefault(p =>
            string.Equals(p.Name, "Content", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, "Text", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the configured vector dimensions.
    /// </summary>
    public int GetVectorDimensions() => _vectorDimensions;

    /// <summary>
    /// Converts a <typeparamref name="TRecord"/> to the Postgres record tuple.
    /// </summary>
    public (string Id, string Content, Dictionary<string, object>? Metadata, ReadOnlyMemory<float>? Embedding) ToRecord(TRecord record)
    {
        var id = _keyProperty.GetValue(record)?.ToString()
            ?? throw new InvalidOperationException("Key property value is null.");

        var content = _contentProperty?.GetValue(record)?.ToString() ?? string.Empty;

        // Collect all non-key, non-vector, non-content data properties as metadata
        Dictionary<string, object>? metadata = null;
        var metadataProperties = _dataProperties.Where(p => p != _contentProperty).ToList();
        if (metadataProperties.Count > 0)
        {
            metadata = new Dictionary<string, object>();
            foreach (var prop in metadataProperties)
            {
                var value = prop.GetValue(record);
                if (value != null)
                {
                    metadata[prop.Name] = value;
                }
            }
        }

        ReadOnlyMemory<float>? embedding = null;
        if (_vectorProperty != null)
        {
            var vectorValue = _vectorProperty.GetValue(record);
            embedding = vectorValue switch
            {
                ReadOnlyMemory<float> rom => rom,
                float[] arr => arr,
                _ => null,
            };
        }

        return (id, content, metadata, embedding);
    }

    /// <summary>
    /// Converts an <see cref="EmbeddingTableRecord"/> back to <typeparamref name="TRecord"/>.
    /// </summary>
    public TRecord? FromRecord(EmbeddingTableRecord record)
    {
        var instance = Activator.CreateInstance<TRecord>();

        // Set key
        if (_keyProperty.CanWrite)
        {
            _keyProperty.SetValue(instance, record.Id);
        }

        // Set content
        if (_contentProperty?.CanWrite == true)
        {
            _contentProperty.SetValue(instance, record.Content);
        }

        // Set metadata properties
        if (record.Metadata != null)
        {
            foreach (var prop in _dataProperties.Where(p => p != _contentProperty))
            {
                if (record.Metadata.TryGetValue(prop.Name, out var value))
                {
                    try
                    {
                        if (prop.PropertyType == typeof(string))
                        {
                            prop.SetValue(instance, value?.ToString());
                        }
                        else
                        {
                            prop.SetValue(instance, Convert.ChangeType(value, prop.PropertyType));
                        }
                    }
                    catch
                    {
                        // Skip properties that can't be converted
                    }
                }
            }
        }

        // Set embedding
        if (_vectorProperty?.CanWrite == true && record.Embedding != null)
        {
            var vectorArray = record.Embedding.ToArray();
            if (_vectorProperty.PropertyType == typeof(ReadOnlyMemory<float>))
            {
                _vectorProperty.SetValue(instance, new ReadOnlyMemory<float>(vectorArray));
            }
            else if (_vectorProperty.PropertyType == typeof(float[]))
            {
                _vectorProperty.SetValue(instance, vectorArray);
            }
        }

        return instance;
    }
}
