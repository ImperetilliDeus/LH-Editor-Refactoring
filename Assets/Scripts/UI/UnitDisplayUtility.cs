using System.Globalization;

public static class MeasurementUnits
{
    public const float MillimetersPerCentimeter = 10f;
    public const float MillimetersPerMeter = 1000f;
    public const float MillimetersPerUnit = 100f;

    public static float MillimetersToCentimeters(float millimeters)
    {
        return millimeters / MillimetersPerCentimeter;
    }

    public static float CentimetersToMillimeters(float centimeters)
    {
        return centimeters * MillimetersPerCentimeter;
    }

    public static float MillimetersToMeters(float millimeters)
    {
        return millimeters / MillimetersPerMeter;
    }

    public static float MetersToMillimeters(float meters)
    {
        return meters * MillimetersPerMeter;
    }

    public static float MillimetersToUnits(float millimeters)
    {
        return millimeters / MillimetersPerUnit;
    }

    public static float UnitsToMillimeters(float units)
    {
        return units * MillimetersPerUnit;
    }
}

public static class UnitDisplayUtility
{
    public static string FormatMillimetersWithConversions(float millimeters)
    {
        float cm = MeasurementUnits.MillimetersToCentimeters(millimeters);
        float m = MeasurementUnits.MillimetersToMeters(millimeters);
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
