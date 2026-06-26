using System.Globalization;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

/// <summary>
/// Keeps the UI color model aligned with Polytoria's current Color.New(r, g, b, 1)
/// export contract, where graph parameters store RGB channels as 0..1 text.
/// </summary>
internal static class PolytoriaColorParameterAdapter
{
    public static bool SetChannel(RuleParameter parameter, int byteValue)
    {
        var normalized = (Math.Clamp(byteValue, 0, 255) / 255.0).ToString("0.######", CultureInfo.InvariantCulture);
        return SetChannel(parameter, normalized);
    }

    public static bool SetChannelNormalized(RuleParameter parameter, double normalizedValue)
    {
        var normalized = Math.Clamp(normalizedValue, 0, 1).ToString("0.######", CultureInfo.InvariantCulture);
        return SetChannel(parameter, normalized);
    }

    public static double ToByte(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return 0;
        }

        return Math.Round(Math.Clamp(parsed, 0, 1) * 255.0);
    }

    public static double ToNormalized(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return 0;
        }

        return Math.Round(Math.Clamp(parsed, 0, 1), 6);
    }

    public static string ChannelText(RuleParameter parameter)
    {
        return double.TryParse(parameter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 0, 1).ToString("0.###", CultureInfo.InvariantCulture)
            : "0";
    }

    public static double NormalizeHue(double hueDegrees)
    {
        if (double.IsNaN(hueDegrees) || double.IsInfinity(hueDegrees))
        {
            return 0;
        }

        var normalized = hueDegrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    public static bool TryParseHex(string value, out int r, out int g, out int b)
    {
        r = 0;
        g = 0;
        b = 0;
        var normalized = value.Trim().TrimStart('#');
        if (normalized.Length == 3)
        {
            normalized = string.Concat(normalized.Select(ch => $"{ch}{ch}"));
        }

        if (normalized.Length != 6 ||
            !int.TryParse(normalized[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) ||
            !int.TryParse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) ||
            !int.TryParse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
        {
            return false;
        }

        return true;
    }

    private static bool SetChannel(RuleParameter parameter, string normalized)
    {
        if (parameter.Value == normalized &&
            parameter.Binding.ConstantValue == normalized &&
            parameter.Binding.SourceKind == GraphValueSourceKind.Constant)
        {
            return false;
        }

        parameter.Value = normalized;
        parameter.ValueSource = "Suggested Value";
        parameter.Binding.SourceKind = GraphValueSourceKind.Constant;
        parameter.Binding.ConstantValue = normalized;
        parameter.Binding.DisplayText = normalized;
        return true;
    }
}
