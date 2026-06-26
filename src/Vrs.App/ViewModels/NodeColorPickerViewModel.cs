using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

/// <summary>
/// Presents a Polytoria-friendly color editor while preserving the graph's
/// existing r/g/b parameter storage and Luau export behavior.
/// </summary>
public sealed partial class NodeColorPickerViewModel : ObservableObject
{
    private const int RecentColorLimit = 12;
    private readonly RuleParameter red;
    private readonly RuleParameter green;
    private readonly RuleParameter blue;
    private readonly Action valueChanged;
    private bool updating;

    public NodeColorPickerViewModel(
        RuleParameter red,
        RuleParameter green,
        RuleParameter blue,
        string title,
        Action valueChanged,
        ObservableCollection<ColorSwatchViewModel>? recentColors = null)
    {
        this.red = red;
        this.green = green;
        this.blue = blue;
        this.valueChanged = valueChanged;
        Title = string.IsNullOrWhiteSpace(title) ? "Polytoria Color" : title;
        RecentColors = recentColors ?? [];
    }

    public string Title { get; }

    public string Subtitle => "Color.New(red, green, blue, 1)";

    public string PreviewHex => HexColor;

    public Color SelectedColor
    {
        get => Color.FromRgb((byte)RedByte, (byte)GreenByte, (byte)BlueByte);
        set => ApplyRgb(value.R, value.G, value.B);
    }

    public HsvColor SelectedHsvColor
    {
        get => SelectedColor.ToHsv();
        set
        {
            var color = value.ToRgb();
            ApplyRgb(color.R, color.G, color.B);
        }
    }

    public ObservableCollection<ColorSwatchViewModel> RecentColors { get; }

    public bool HasRecentColors => RecentColors.Count > 0;

    public int AlphaByte => 255;

    public double AlphaLinear => 1;

    public string AlphaLockText => "Fixed at 255 / 1";

    public string AlphaTooltip => "Polytoria export currently writes Color.New(r, g, b, 1), so alpha is shown for familiarity but remains locked.";

    public string PolytoriaValueText => $"Color.New({PolytoriaColorParameterAdapter.ChannelText(red)}, {PolytoriaColorParameterAdapter.ChannelText(green)}, {PolytoriaColorParameterAdapter.ChannelText(blue)}, 1)";

    public string TooltipText => "Choose a Polytoria RGB color. The picker stores red, green, and blue as 0..1 values for Color.New(r, g, b, 1).";

    [ObservableProperty]
    private ColorPickerEditMode currentMode = ColorPickerEditMode.Rgb;

    public bool IsRgbMode => CurrentMode == ColorPickerEditMode.Rgb;
    public bool IsHsvMode => CurrentMode == ColorPickerEditMode.Hsv;
    public bool IsLinearMode => CurrentMode == ColorPickerEditMode.Linear;
    public string RgbModeBackgroundHex => IsRgbMode ? "#225f93" : "#161b22";
    public string HsvModeBackgroundHex => IsHsvMode ? "#225f93" : "#161b22";
    public string LinearModeBackgroundHex => IsLinearMode ? "#225f93" : "#161b22";
    public string RgbModeBorderHex => IsRgbMode ? "#37a7ff" : "#3d4a5c";
    public string HsvModeBorderHex => IsHsvMode ? "#37a7ff" : "#3d4a5c";
    public string LinearModeBorderHex => IsLinearMode ? "#37a7ff" : "#3d4a5c";

    public IReadOnlyList<ColorSwatchViewModel> Swatches { get; } = PolytoriaColorSwatches.Default;

    public double RedByte
    {
        get => PolytoriaColorParameterAdapter.ToByte(red.Value);
        set => SetChannel(red, value);
    }

    public double GreenByte
    {
        get => PolytoriaColorParameterAdapter.ToByte(green.Value);
        set => SetChannel(green, value);
    }

    public double BlueByte
    {
        get => PolytoriaColorParameterAdapter.ToByte(blue.Value);
        set => SetChannel(blue, value);
    }

    public double HueDegrees
    {
        get => Math.Round(SelectedHsvColor.H);
        set => ApplyHsv(value, SaturationPercent, ValuePercent);
    }

    public double SaturationPercent
    {
        get => Math.Round(SelectedHsvColor.S * 100.0);
        set => ApplyHsv(HueDegrees, value, ValuePercent);
    }

    public double ValuePercent
    {
        get => Math.Round(SelectedHsvColor.V * 100.0);
        set => ApplyHsv(HueDegrees, SaturationPercent, value);
    }

    public double LinearRed
    {
        get => PolytoriaColorParameterAdapter.ToNormalized(red.Value);
        set => SetChannelNormalized(red, value);
    }

    public double LinearGreen
    {
        get => PolytoriaColorParameterAdapter.ToNormalized(green.Value);
        set => SetChannelNormalized(green, value);
    }

    public double LinearBlue
    {
        get => PolytoriaColorParameterAdapter.ToNormalized(blue.Value);
        set => SetChannelNormalized(blue, value);
    }

    public string HexColor
    {
        get => $"#{(int)RedByte:X2}{(int)GreenByte:X2}{(int)BlueByte:X2}";
        set
        {
            if (!PolytoriaColorParameterAdapter.TryParseHex(value, out var r, out var g, out var b))
            {
                return;
            }

            ApplyRgb(r, g, b);
        }
    }

    [RelayCommand]
    private void ApplySwatch(ColorSwatchViewModel? swatch)
    {
        if (swatch is null)
        {
            return;
        }

        HexColor = swatch.Hex;
    }

    [RelayCommand]
    private void ApplyRecentColor(ColorSwatchViewModel? swatch)
    {
        if (swatch is null)
        {
            return;
        }

        HexColor = swatch.Hex;
    }

    [RelayCommand]
    private void SetColorMode(string? mode)
    {
        if (!Enum.TryParse<ColorPickerEditMode>(mode, ignoreCase: true, out var parsed))
        {
            return;
        }

        CurrentMode = parsed;
    }

    partial void OnCurrentModeChanged(ColorPickerEditMode value)
    {
        NotifyModePresentationChanged();
    }

    private void ApplyRgb(int r, int g, int b)
    {
        if (updating)
        {
            return;
        }

        updating = true;
        try
        {
            var changed = PolytoriaColorParameterAdapter.SetChannel(red, r) |
                PolytoriaColorParameterAdapter.SetChannel(green, g) |
                PolytoriaColorParameterAdapter.SetChannel(blue, b);
            NotifyAllChannelsChanged();
            if (changed)
            {
                AddRecentColor(HexColor);
                valueChanged();
            }
        }
        finally
        {
            updating = false;
        }
    }

    private void SetChannel(RuleParameter parameter, double byteValue)
    {
        if (updating)
        {
            return;
        }

        updating = true;
        try
        {
            var changed = PolytoriaColorParameterAdapter.SetChannel(parameter, (int)Math.Round(Math.Clamp(byteValue, 0, 255)));
            NotifyAllChannelsChanged();
            if (changed)
            {
                AddRecentColor(HexColor);
                valueChanged();
            }
        }
        finally
        {
            updating = false;
        }
    }

    private void SetChannelNormalized(RuleParameter parameter, double normalizedValue)
    {
        if (updating)
        {
            return;
        }

        updating = true;
        try
        {
            var changed = PolytoriaColorParameterAdapter.SetChannelNormalized(parameter, normalizedValue);
            NotifyAllChannelsChanged();
            if (changed)
            {
                AddRecentColor(HexColor);
                valueChanged();
            }
        }
        finally
        {
            updating = false;
        }
    }

    private void ApplyHsv(double hueDegrees, double saturationPercent, double valuePercent)
    {
        var hsv = new HsvColor(
            1,
            PolytoriaColorParameterAdapter.NormalizeHue(hueDegrees),
            Math.Clamp(saturationPercent, 0, 100) / 100.0,
            Math.Clamp(valuePercent, 0, 100) / 100.0);
        var color = hsv.ToRgb();
        ApplyRgb(color.R, color.G, color.B);
    }

    private void AddRecentColor(string hex)
    {
        var existing = RecentColors.FirstOrDefault(item => item.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentColors.Remove(existing);
        }

        RecentColors.Insert(0, new ColorSwatchViewModel("Recent", hex.ToUpperInvariant()));
        while (RecentColors.Count > RecentColorLimit)
        {
            RecentColors.RemoveAt(RecentColors.Count - 1);
        }

        OnPropertyChanged(nameof(HasRecentColors));
    }

    private void NotifyAllChannelsChanged()
    {
        OnPropertyChanged(nameof(RedByte));
        OnPropertyChanged(nameof(GreenByte));
        OnPropertyChanged(nameof(BlueByte));
        OnPropertyChanged(nameof(HueDegrees));
        OnPropertyChanged(nameof(SaturationPercent));
        OnPropertyChanged(nameof(ValuePercent));
        OnPropertyChanged(nameof(LinearRed));
        OnPropertyChanged(nameof(LinearGreen));
        OnPropertyChanged(nameof(LinearBlue));
        OnPropertyChanged(nameof(SelectedColor));
        OnPropertyChanged(nameof(SelectedHsvColor));
        OnPropertyChanged(nameof(HexColor));
        OnPropertyChanged(nameof(PreviewHex));
        OnPropertyChanged(nameof(PolytoriaValueText));
    }

    private void NotifyModePresentationChanged()
    {
        OnPropertyChanged(nameof(IsRgbMode));
        OnPropertyChanged(nameof(IsHsvMode));
        OnPropertyChanged(nameof(IsLinearMode));
        OnPropertyChanged(nameof(RgbModeBackgroundHex));
        OnPropertyChanged(nameof(HsvModeBackgroundHex));
        OnPropertyChanged(nameof(LinearModeBackgroundHex));
        OnPropertyChanged(nameof(RgbModeBorderHex));
        OnPropertyChanged(nameof(HsvModeBorderHex));
        OnPropertyChanged(nameof(LinearModeBorderHex));
    }

}

public sealed record ColorSwatchViewModel(string Name, string Hex)
{
    public string Tooltip => $"{Name} {Hex}";
}

public enum ColorPickerEditMode
{
    Rgb,
    Hsv,
    Linear
}
