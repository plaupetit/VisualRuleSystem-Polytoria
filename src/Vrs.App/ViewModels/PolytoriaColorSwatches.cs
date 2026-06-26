namespace Vrs.App.ViewModels;

/// <summary>
/// Shared Polytoria-oriented color presets for node parameter editors.
/// Keeping them outside the ViewModel makes future palette changes data-only.
/// </summary>
internal static class PolytoriaColorSwatches
{
    public static IReadOnlyList<ColorSwatchViewModel> Default { get; } =
    [
        new("White", "#FFFFFF"),
        new("Black", "#000000"),
        new("Concrete", "#8C8F96"),
        new("Grass", "#3FA34D"),
        new("Sky", "#62B5F6"),
        new("Checkpoint", "#26D07C"),
        new("Hazard", "#FF4D4D"),
        new("Coin", "#FFD24A"),
        new("Water", "#1CA7EC"),
        new("Neon Cyan", "#00D4FF"),
        new("Purple", "#8B5CF6"),
        new("Pink", "#F472B6"),
        new("Wood", "#8B5A2B"),
        new("Lava", "#FF6A00")
    ];
}
