using Newtonsoft.Json;
using SmartTalk.Messages.Dto.EasyPos;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneCallOrderItemModifiers
{
    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("ModifierId")]
    public long ModifierId { get; set; }

    [JsonProperty("localizations")]
    public List<EasyPosResponseLocalization> Localizations { get; set; }

    [JsonProperty("modifierProductId")]
    public long ModifierProductId { get; set; }

    [JsonProperty("ModifierLocalizations")]
    public List<EasyPosResponseLocalization> ModifierLocalizations { get; set; }
}