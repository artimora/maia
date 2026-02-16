using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Artimora.Maia;

public class Message(string id)
{
    public readonly string id = id;
    
    private Dictionary<string, string> values = [];

    public string this[string key]
    {
        get => values[key];
        set => values[key] = value;
    }

    private class SerializedMessage(string id, Dictionary<string, string> values)
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = id;

        [JsonPropertyName("values")]
        public Dictionary<string, string> Values { get; set; } = values;
    }

    public byte[] Serialize() => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new SerializedMessage(id, values)));

    public static byte[] Serialize(Message message) => message.Serialize();

    public static Message Deserialize(byte[] raw)
    {
        var data = Encoding.UTF8.GetString(raw);
        var deserialized = JsonSerializer.Deserialize<SerializedMessage>(data);
        var message = new Message(deserialized!.Id)
        {
            values = deserialized.Values
        };
        return message;
    }
}