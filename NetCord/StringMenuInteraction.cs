using NetCord.Gateway;
using NetCord.Rest;

namespace NetCord;

public class StringMenuInteraction(JsonModels.JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client) : MessageComponentInteraction(jsonModel, guild, sendResponseAsync, client)
{
    public StringMenuInteractionData Data4 { get; } = new(jsonModel.Data!);

    public sealed override MessageComponentInteractionData Data3 => Data4;
}

public class StringMenuInteractionData(JsonModels.JsonInteractionData jsonModel) : MessageComponentInteractionData(jsonModel)
{
    public IReadOnlyList<string> SelectedValues => _jsonModel.SelectedValues!;
}
