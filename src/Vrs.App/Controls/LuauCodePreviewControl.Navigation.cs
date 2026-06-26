using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Vrs.App.Controls;

public sealed partial class LuauCodePreviewControl
{
    // Keeps preview navigation local to the control. ViewModels only request a
    // focus line; this control owns the actual viewport offsets.
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if ((e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
        {
            SetHorizontalOffset(horizontalOffset - e.Delta.Y * 48);
        }
        else
        {
            SetVerticalOffset(verticalOffset - e.Delta.Y * lineHeight * 3);
        }

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Down:
                SetVerticalOffset(verticalOffset + lineHeight);
                e.Handled = true;
                break;
            case Key.Up:
                SetVerticalOffset(verticalOffset - lineHeight);
                e.Handled = true;
                break;
            case Key.PageDown:
                SetVerticalOffset(verticalOffset + Math.Max(lineHeight, Bounds.Height - ScrollbarSize - lineHeight));
                e.Handled = true;
                break;
            case Key.PageUp:
                SetVerticalOffset(verticalOffset - Math.Max(lineHeight, Bounds.Height - ScrollbarSize - lineHeight));
                e.Handled = true;
                break;
            case Key.Right:
                SetHorizontalOffset(horizontalOffset + charWidth * 4);
                e.Handled = true;
                break;
            case Key.Left:
                SetHorizontalOffset(horizontalOffset - charWidth * 4);
                e.Handled = true;
                break;
            case Key.Home:
                SetHorizontalOffset(0);
                e.Handled = true;
                break;
            case Key.End:
                SetHorizontalOffset(maxLineWidth);
                e.Handled = true;
                break;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ClampOffsets();
        InvalidateVisual();
    }

    private void SetHorizontalOffset(double value)
    {
        horizontalOffset = Math.Clamp(value, 0, MaxHorizontalOffset());
        InvalidateVisual();
    }

    private void SetVerticalOffset(double value)
    {
        verticalOffset = Math.Clamp(value, 0, MaxVerticalOffset());
        InvalidateVisual();
    }

    private void ClampOffsets()
    {
        horizontalOffset = Math.Clamp(horizontalOffset, 0, MaxHorizontalOffset());
        verticalOffset = Math.Clamp(verticalOffset, 0, MaxVerticalOffset());
    }

    private void ScrollToFocusedLine()
    {
        if (focusLineNumber <= 0 || lines.Length == 0)
        {
            return;
        }

        var lineIndex = Math.Clamp(focusLineNumber - 1, 0, lines.Length - 1);
        SetVerticalOffset(Math.Max(0, (lineIndex - 2) * lineHeight));
    }

    private double MaxHorizontalOffset()
    {
        var viewportWidth = Math.Max(1, Bounds.Width - LineNumberWidth - ScrollbarSize);
        return Math.Max(0, maxLineWidth + PaddingLeft * 2 - viewportWidth);
    }

    private double MaxVerticalOffset()
    {
        var viewportHeight = Math.Max(1, Bounds.Height - ScrollbarSize);
        return Math.Max(0, lines.Length * lineHeight + PaddingTop * 2 - viewportHeight);
    }
}
