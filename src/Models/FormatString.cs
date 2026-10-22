using System.Collections.Frozen;
using System.Text;
using TushonkInfo.Constants;
using TushonkInfo.Services;
using TushonkInfo.Utils;
using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;

namespace TushonkInfo.Models;

public class FormatString(string template, LocalizationService localizationService)
{
    public static string QuickFormat(
        string template,
        string dataKey,
        object dataValue,
        in LocalizationService localizationService
    )
    {
        var instance = new FormatString(template, localizationService);
        instance.AddData(dataKey, dataValue);
        return instance.ToString();
    }

    private Dictionary<string, object> _data = new();

    public bool AddData(string name, object value)
    {
        return _data.TryAdd(name, value);
    }

    /// <summary>
    /// Consumes the data inside. Returning it and emptying the class.
    /// </summary>
    /// <returns> The data object inside the class </returns>
    public FrozenDictionary<string, object> ExtractData()
    {
        var returnData = _data.ToFrozenDictionary();
        _data = new Dictionary<string, object>();

        return returnData;
    }

    public override string ToString()
    {
        return RegularExpressions
            .PlaceholderRegex()
            .Replace(
                template,
                match =>
                {
                    var key = match.Groups[1].Value;
                    var dataKey = match.Groups[2].Value;

                    if (localizationService.TryGetKey(key, out var localized))
                    {
                        return localized;
                    }

                    if (_data.TryGetValue(dataKey, out var value))
                    {
                        return value.ToString() ?? string.Empty;
                    }

                    return string.IsNullOrWhiteSpace(key) ? string.Empty : key;
                }
            );
    }
}
