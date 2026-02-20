namespace Artimora.Maia;

public readonly record struct FunctionSenderData(HandlerMetaData.Side TargetSide, Message MessageContents, int TargetClient)
{
    public readonly HandlerMetaData.Side TargetSide = TargetSide;
    public readonly Message MessageContents = MessageContents;
    public readonly int TargetClient = TargetClient;
}