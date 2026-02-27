namespace Artimora.Maia;

/// <summary>
/// Encapsulates a function-related message that should be dispatched to a specific side/client.
/// </summary>
/// <param name="TargetSide">The side that should receive the message.</param>
/// <param name="MessageContents">The message payload to dispatch.</param>
/// <param name="TargetClient">
/// The target client id when sending to a client; ignored when sending to the server side.
/// </param>
public readonly record struct FunctionSenderData(HandlerMetaData.Side TargetSide, Message MessageContents, int TargetClient)
{
    /// <summary>
    /// The side that should receive this function message.
    /// </summary>
    public readonly HandlerMetaData.Side TargetSide = TargetSide;

    /// <summary>
    /// The function message to send.
    /// </summary>
    public readonly Message MessageContents = MessageContents;

    /// <summary>
    /// The intended target client id when routing to the client side.
    /// </summary>
    public readonly int TargetClient = TargetClient;
}
