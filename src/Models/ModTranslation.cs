using System.Text.Json;
using System.Text.Json.Serialization;

namespace TushonkInfo.Models;

public class ModTranslation
{
    [JsonPropertyName("debug")]
    public ModTranslationDebug ModTranslationDebug { get; set; } = new();
	
    [JsonExtensionData]
    public Dictionary<string, JsonElement> RawLanguages { get; set; } = new();
}