using System.Text.RegularExpressions;

namespace TushonkInfo;

public static partial class RegularExpressions
{
    [GeneratedRegex(@"{(\w+)}|\((\w+)\)", RegexOptions.Compiled)]
    public static partial Regex PlaceholderRegex();
}
