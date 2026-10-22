using System.Globalization;

namespace TushonkInfo.Extensions;

public static class DoubleExtensions
{
    extension(double value)
    {
        public string FormatToPrice(bool noCents = true)
        {
            return noCents
                ? value.ToString("N0", CultureInfo.GetCultureInfo("en-US"))
                : value.ToString(CultureInfo.GetCultureInfo("en-US"));
        }
    }
}
