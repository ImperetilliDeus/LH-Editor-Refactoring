using System.Globalization;

public static class UnitDisplayUtility
{
    public static string FormatMillimetersWithConversions(float millimeters)
    {
        float cm = millimeters / 10f;
        float m = millimeters / 1000f;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.##}mm ({1:0.##}cm, {2:0.###}m)",
            millimeters,
            cm,
            m);
    }

    public static bool TryParseMillimeters(string inputText, out float millimeters)
    {
        millimeters = 0f;
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return false;
        }

        string trimmed = inputText.Trim();
        int length = trimmed.Length;
        int index = 0;

        if (index < length && (trimmed[index] == '+' || trimmed[index] == '-'))
        {
            index++;
        }

        bool hasDigit = false;
        while (index < length)
        {
            char c = trimmed[index];
            if (c >= '0' && c <= '9')
            {
                hasDigit = true;
                index++;
                continue;
            }

            if (c == '.')
            {
                index++;
                continue;
            }

            break;
        }

        if (!hasDigit)
        {
            return false;
        }

        string numberPart = trimmed.Substring(0, index);
        return float.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out millimeters);
    }
}
