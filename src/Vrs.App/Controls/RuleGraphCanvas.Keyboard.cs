using Avalonia.Input;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Keyboard and text input are palette-aware first, then fall back to canvas
    // gestures such as temporary spacebar panning and selection deletion.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (nodePaletteOpen && HandleNodePaletteKeyDown(e))
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.C)
            {
                Host?.CopyGraphSelection();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.X)
            {
                Host?.CutGraphSelection();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V)
            {
                Host?.PasteGraphClipboard(float.NaN, float.NaN);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Space)
        {
            spacePanning = true;
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Delete or Key.Back)
        {
            Host?.DeleteGraphSelection();
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            spacePanning = false;
            e.Handled = true;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!nodePaletteOpen || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (e.Text.Any(ch => !char.IsControl(ch)))
        {
            nodePaletteSearch += e.Text;
            ResetNodePaletteSelection();
            e.Handled = true;
            InvalidateVisual();
        }
    }
}
