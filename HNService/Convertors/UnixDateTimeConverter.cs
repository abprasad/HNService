using System.Text.Json.Serialization;
using System.Text.Json;

namespace HNService.Convertors
{    
    public class UnixDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            long timestamp = reader.GetInt64();

            return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            long timestamp = new DateTimeOffset(value).ToUnixTimeSeconds();

            writer.WriteNumberValue(timestamp);
        }
    }
}
