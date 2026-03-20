using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LangChain.Databases.IntegrationTests;

public partial class DatabaseTests
{
    public static Dictionary<string, float[]> Embeddings { get; } = LoadEmbeddings();

    internal static Dictionary<string, float[]> LoadEmbeddings()
    {
        var dict = new Dictionary<string, float[]>();
        foreach (var resource in new[]
        {
            H.Resources.apple_json,
            H.Resources.banana_json,
            H.Resources.computer_json,
            H.Resources.keyboard_json,
            H.Resources.laptop_json,
            H.Resources.lemon_json,
            H.Resources.mainframe_json,
            H.Resources.mouse_json,
            H.Resources.orange_json,
            H.Resources.pc_json,
            H.Resources.peach_json,
            H.Resources.tomato_json,
            H.Resources.tree_json,
        })
        {
            var json =
                JsonSerializer.Deserialize<Dictionary<string, float[]>>(resource.AsString()) ??
                throw new InvalidOperationException("json is null");
            var (key, value) = json.First();

            dict.Add(key, value);
        }

        return dict;
    }
}
