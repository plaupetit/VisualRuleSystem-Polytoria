namespace Vrs.App.ViewModels;

internal static class PickerRowPresentation
{
    private const string KeyboardSelectedBackgroundHex = "#225f93";
    private const string CurrentValueBackgroundHex = "#263545";
    private const string DefaultBackgroundHex = "#2b2b2b";
    private const string KeyboardSelectedPrimaryForegroundHex = "#ffffff";
    private const string DefaultPrimaryForegroundHex = "#d8dde4";
    private const string KeyboardSelectedSecondaryForegroundHex = "#dcecff";
    private const string DefaultSecondaryForegroundHex = "#aeb8c4";

    public static string RowBackgroundHex(bool isKeyboardSelected, bool isCurrentValue)
    {
        // Choice and recipe pickers share selection colors so keyboard focus
        // and the committed value are visually consistent across inspectors.
        return isKeyboardSelected
            ? KeyboardSelectedBackgroundHex
            : isCurrentValue ? CurrentValueBackgroundHex : DefaultBackgroundHex;
    }

    public static string PrimaryForegroundHex(bool isKeyboardSelected)
    {
        return isKeyboardSelected ? KeyboardSelectedPrimaryForegroundHex : DefaultPrimaryForegroundHex;
    }

    public static string SecondaryForegroundHex(bool isKeyboardSelected)
    {
        return isKeyboardSelected ? KeyboardSelectedSecondaryForegroundHex : DefaultSecondaryForegroundHex;
    }
}
