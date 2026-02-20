namespace Artimora.Maia;

public class GenericFunctionHandler : IFunctionHandler
{
    public Task<Dictionary<string, string>> CallFunction(string functionName, Dictionary<string, string>? args)
    {
        throw new NotImplementedException();
    }

    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func)
    {
        throw new NotImplementedException();
    }
}