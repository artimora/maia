namespace Artimora.Maia;

// 1. run CallFunction
// 2. handler sends a message to whatever its connected to
// 3. other end sends back result
// 4. result is returned to the CallFunction call
public interface IFunctionHandler
{
    public Task<Dictionary<string, string>> CallFunction(string functionName, Dictionary<string, string>? args);
    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func);
}