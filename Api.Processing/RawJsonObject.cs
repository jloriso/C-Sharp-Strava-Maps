using System.Text;
using System.Text.Json;

namespace Api.Processing
{
    /// <summary>Joins already-serialized JSON values into one JSON object without
    /// re-parsing them (the values can be tens of MB).</summary>
    internal static class RawJsonObject
    {
        public static string Compose(params (string Name, string Json)[] properties)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var (name, json) in properties)
                {
                    writer.WritePropertyName(name);
                    writer.WriteRawValue(json, skipInputValidation: true);
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
