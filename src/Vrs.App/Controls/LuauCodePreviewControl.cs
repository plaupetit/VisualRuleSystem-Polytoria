using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vrs.Core.Export;

namespace Vrs.App.Controls;

/// <summary>
/// Lightweight read-only Luau viewer. It avoids editor template failures while
/// still sharing the same portable classifier and theme as HTML export.
/// </summary>
public sealed partial class LuauCodePreviewControl : Control
{
    public static readonly DirectProperty<LuauCodePreviewControl, string> CodeTextProperty =
        AvaloniaProperty.RegisterDirect<LuauCodePreviewControl, string>(
            nameof(CodeText),
            control => control.CodeText,
            (control, value) => control.CodeText = value ?? "");

    public static readonly DirectProperty<LuauCodePreviewControl, IReadOnlyList<int>> HighlightedLineNumbersProperty =
        AvaloniaProperty.RegisterDirect<LuauCodePreviewControl, IReadOnlyList<int>>(
            nameof(HighlightedLineNumbers),
            control => control.HighlightedLineNumbers,
            (control, value) => control.HighlightedLineNumbers = value ?? []);

    public static readonly DirectProperty<LuauCodePreviewControl, int> FocusLineNumberProperty =
        AvaloniaProperty.RegisterDirect<LuauCodePreviewControl, int>(
            nameof(FocusLineNumber),
            control => control.FocusLineNumber,
            (control, value) => control.FocusLineNumber = value);

    public static readonly DirectProperty<LuauCodePreviewControl, int> FocusRequestIdProperty =
        AvaloniaProperty.RegisterDirect<LuauCodePreviewControl, int>(
            nameof(FocusRequestId),
            control => control.FocusRequestId,
            (control, value) => control.FocusRequestId = value);

    private const double PaddingLeft = 8;
    private const double PaddingTop = 8;
    private const double LineNumberWidth = 58;
    private const double ScrollbarSize = 12;

    private readonly Typeface typeface = new("Consolas");
    private readonly LuauSyntaxTheme theme = LuauSyntaxTheme.PolytoriaLike;
    private readonly Dictionary<LuauSyntaxTokenKind, IBrush> tokenBrushes = [];
    private readonly IBrush backgroundBrush;
    private readonly IBrush lineNumberBrush = new SolidColorBrush(Color.Parse("#6f737a"));
    private readonly IBrush gutterBrush = new SolidColorBrush(Color.Parse("#171717"));
    private readonly IBrush borderBrush = new SolidColorBrush(Color.Parse("#34383f"));
    private readonly IBrush scrollbarTrackBrush = new SolidColorBrush(Color.Parse("#151515"));
    private readonly IBrush scrollbarThumbBrush = new SolidColorBrush(Color.Parse("#6f737a"));
    private readonly IBrush emptyBrush = new SolidColorBrush(Color.Parse("#8b949e"));
    private readonly IBrush changedLineBrush = new SolidColorBrush(Color.Parse("#16324c"));

    private string codeText = "";
    private string[] lines = [""];
    private IReadOnlyList<LuauSyntaxToken> tokens = [];
    private IReadOnlyList<int> highlightedLineNumbers = [];
    private HashSet<int> highlightedLineNumberSet = [];
    private int focusLineNumber;
    private int focusRequestId;
    private double horizontalOffset;
    private double verticalOffset;
    private double lineHeight = 18;
    private double charWidth = 8;
    private double maxLineWidth;

    public LuauCodePreviewControl()
    {
        Focusable = true;
        ClipToBounds = true;
        backgroundBrush = new SolidColorBrush(Color.Parse(theme.BackgroundHex));
    }

    public string CodeText
    {
        get => codeText;
        set
        {
            var normalized = value ?? "";
            if (SetAndRaise(CodeTextProperty, ref codeText, normalized))
            {
                RebuildDocument(normalized);
            }
        }
    }

    public IReadOnlyList<int> HighlightedLineNumbers
    {
        get => highlightedLineNumbers;
        set
        {
            var normalized = value ?? [];
            if (SetAndRaise(HighlightedLineNumbersProperty, ref highlightedLineNumbers, normalized))
            {
                highlightedLineNumberSet = normalized.Where(line => line > 0).ToHashSet();
                InvalidateVisual();
            }
        }
    }

    public int FocusLineNumber
    {
        get => focusLineNumber;
        set
        {
            if (SetAndRaise(FocusLineNumberProperty, ref focusLineNumber, value))
            {
                ScrollToFocusedLine();
            }
        }
    }

    public int FocusRequestId
    {
        get => focusRequestId;
        set
        {
            if (SetAndRaise(FocusRequestIdProperty, ref focusRequestId, value))
            {
                ScrollToFocusedLine();
            }
        }
    }
}
