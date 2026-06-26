using Avalonia;
using Avalonia.Media;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    private void DrawGrid(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse("#243040")), 1);
        var step = 32.0 * Zoom;
        if (step < 8.0)
        {
            step = 8.0;
        }

        var offsetX = PositiveModulo(PanX, step);
        var offsetY = PositiveModulo(PanY, step);

        for (var x = offsetX; x < Bounds.Width; x += step)
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
        }

        for (var y = offsetY; y < Bounds.Height; y += step)
        {
            context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
        }
    }
}
