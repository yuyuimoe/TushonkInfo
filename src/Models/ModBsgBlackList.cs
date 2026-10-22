using System.Text.Json.Serialization;

namespace TushonkInfo.Models;

public class ModBsgBlackList
{
    [JsonPropertyName("bsgBlackList")]
    public List<string> BsgBlackList { get; set; } = [];
}