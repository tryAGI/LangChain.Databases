using System.Text.Json.Serialization;
using LangChain.Databases.JsonConverters;
using LangChain.DocumentLoaders;

namespace LangChain.Databases.Sqlite;

[JsonSourceGenerationOptions(WriteIndented = true, Converters = [typeof(ObjectAsPrimitiveConverter)])]
[JsonSerializable(typeof(Document))]
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext;