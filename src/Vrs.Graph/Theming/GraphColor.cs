using System.Globalization;

namespace Vrs.Graph.Theming;

public readonly record struct GraphColor(byte R, byte G, byte B)
{
    public static GraphColor FromHex(string hex)
    {
        var normalized = hex.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            throw new FormatException($"Graph color must be a 6-digit hex value: {hex}");
        }

        return new GraphColor(
            byte.Parse(normalized[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    public static string InterpolateHex(string fromHex, string toHex, double amount)
    {
        return Interpolate(FromHex(fromHex), FromHex(toHex), amount).ToHex();
    }

    public static GraphColor Interpolate(GraphColor from, GraphColor to, double amount)
    {
        var t = Math.Clamp(amount, 0.0, 1.0);
        return new GraphColor(
            Lerp(from.R, to.R, t),
            Lerp(from.G, to.G, t),
            Lerp(from.B, to.B, t));
    }

    public GraphColor Lighten(double amount)
    {
        return Interpolate(this, new GraphColor(255, 255, 255), amount);
    }

    public string ToHex()
    {
        return FormattableString.Invariant($"#{R:X2}{G:X2}{B:X2}");
    }

    private static byte Lerp(byte from, byte to, double amount)
    {
        return (byte)Math.Round(from + ((to - from) * amount), MidpointRounding.AwayFromZero);
    }
}
