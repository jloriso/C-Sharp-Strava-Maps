using System.Text;
using System.Text.Json;

namespace GpxWorldMap.Rendering
{
    /// <summary>
    /// Builds the JSON object injected into a page's
    /// <c>&lt;script id="page-data" type="application/json"&gt;{{PAGE_DATA_JSON}}&lt;/script&gt;</c>
    /// block, which the page's TypeScript reads via readPageData(). Property names must
    /// match the page's TypeScript data interface.
    /// </summary>
    public sealed class PageDataBuilder
    {
        public const string Placeholder = "{{PAGE_DATA_JSON}}";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly List<(string Name, string Json)> _properties = new();

        /// <summary>Adds a value serialized with camelCase property names.</summary>
        public PageDataBuilder Add<T>(string name, T value) =>
            AddRaw(name, JsonSerializer.Serialize(value, SerializerOptions));

        /// <summary>Adds an already-serialized JSON value (e.g. large precomputed data).</summary>
        public PageDataBuilder AddRaw(string name, string json)
        {
            _properties.Add((name, json));
            return this;
        }

        public string ToJson()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var (name, json) in _properties)
                {
                    writer.WritePropertyName(name);
                    writer.WriteRawValue(json, skipInputValidation: true);
                }
                writer.WriteEndObject();
            }

            // '<' can only appear inside JSON strings, so escaping it is always valid JSON
            // and guarantees the data can't close the surrounding <script> element.
            return Encoding.UTF8.GetString(stream.ToArray()).Replace("<", "\\u003c");
        }

        /// <summary>Replaces the page template's data placeholder with this data.</summary>
        public string ApplyTo(string html)
        {
            if (!html.Contains(Placeholder))
                throw new InvalidOperationException($"Page template has no {Placeholder} placeholder.");
            return html.Replace(Placeholder, ToJson());
        }
    }
}
