namespace Artimora.Maia;

/// <summary>
/// Defines the contract used to register and invoke RPC-like functions over Maia messages.
/// </summary>
public interface IFunctionHandler
{
    /// <summary>
    /// Applies initialization options used by the handler runtime, including function timeout behavior.
    /// </summary>
    /// <param name="newOptions">The options from the active client or server instance.</param>
    public void SetOptions(BaseInitializationOptions newOptions);
    
    /// <summary>
    /// Handles inbound function call and function result messages.
    /// </summary>
    /// <param name="client">
    /// The source client identifier for server-side handlers; ignored for client-side handlers.
    /// </param>
    /// <param name="message">The message to process.</param>
    public void OnMessage(int client, Message message);

    /// <summary>
    /// Registers the transport callback used by the handler to emit function messages.
    /// </summary>
    /// <param name="sender">Callback that delivers outbound function payloads.</param>
    public void RegisterMessageSender(Action<FunctionSenderData> sender);
    
    /// <summary>
    /// Calls a function on the opposite side of the connection and waits for its result.
    /// </summary>
    /// <param name="functionName">The function name to invoke.</param>
    /// <param name="args">
    /// Arguments sent to the function. Implementations may add reserved
    /// <c>artimora:*</c> metadata entries while dispatching the call.
    /// </param>
    /// <returns>
    /// A dictionary containing the returned values and an <c>artimora:error</c> status entry.
    /// </returns>
    /// <remarks>
    /// If <c>artimora:error</c> is anything other than <c>none</c>, function result entries may be missing.
    /// </remarks>
    public Task<Dictionary<string, string>> CallFunction(string functionName, Dictionary<string, string> args);

    /// <summary>
    /// Registers a local function that can be invoked remotely.
    /// </summary>
    /// <param name="functionName">The function identifier exposed to remote callers.</param>
    /// <param name="func">The function implementation.</param>
    /// <param name="forceSet">
    /// If <see langword="true"/>, replaces an existing function with the same name.
    /// </param>
    public void RegisterFunction(string functionName, Func<Dictionary<string, string>, Dictionary<string, string>> func, bool forceSet = false);
}
