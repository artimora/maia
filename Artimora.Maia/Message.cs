namespace Artimora.Maia;

public class Message(string id)
{
    private readonly string id = id;

    private readonly Dictionary<string, string> values = [];

    public string this[string key]
    {
        get => values[key];
        set => values[key] = value;
    }
}