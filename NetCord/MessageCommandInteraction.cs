using NetCord.Gateway;
using NetCord.Rest;

namespace NetCord;

public class MessageCommandInteraction(JsonModels.JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client) : ApplicationCommandInteraction(jsonModel, guild, sendResponseAsync, client)
{
    public MessageCommandInteractionData Data3 { get; } = new(jsonModel.Data!, client);
    
    public sealed override ApplicationCommandInteractionData Data2 => Data3;
}

public class MessageCommandInteractionData(JsonModels.JsonInteractionData jsonModel, RestClient client) : ApplicationCommandInteractionData(jsonModel)
{
    public RestMessage TargetMessage { get; } = new(jsonModel.ResolvedData!.Messages![jsonModel.TargetId.GetValueOrDefault()], client);
}
