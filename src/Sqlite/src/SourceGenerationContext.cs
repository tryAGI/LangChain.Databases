using System.Text.Json.Serialization;
using LangChain.Databases.JsonConverters;

namespace LangChain.Databases.Sqlite;

[JsonSourceGenerationOptions(WriteIndented = true, Converters = [typeof(ObjectAsPrimitiveConverter)])]
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Vector))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext;