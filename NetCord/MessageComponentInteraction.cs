using NetCord.Gateway;
using NetCord.JsonModels;
using NetCord.Rest;

namespace NetCord;

public abstract class MessageComponentInteraction : ComponentInteraction
{
    private protected MessageComponentInteraction(JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client) : base(jsonModel, guild, sendResponseAsync, client)
    {
        var message = jsonModel.Message!;
        message.GuildId = jsonModel.GuildId;
        Message = new(message, guild, Channel, client);
    }

    public Message Message { get; }

    public abstract MessageComponentInteractionData Data3 { get; }

    public sealed override ComponentInteractionData Data2 => Data3;
}

public abstract class MessageComponentInteractionData : ComponentInteractionData
{
    private protected MessageComponentInteractionData(JsonInteractionData jsonModel) : base(jsonModel)
    {
    }

    public int Id => (int)_jsonModel.Id.GetValueOrDefault();

    public ComponentType ComponentType => _jsonModel.ComponentType.GetValueOrDefault();
}
