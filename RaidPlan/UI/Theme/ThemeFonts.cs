using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace RaidPlan.UI.Theme;

/// <summary>
/// Type for the theme, taken from the game's own fonts rather than shipped with the plugin.
/// TrumpGothic is the condensed face the game uses for headings; using it is what stops the
/// window reading as a generic ImGui panel, and it costs nothing to license or download.
/// </summary>
public sealed class ThemeFonts : IDisposable
{
    private IFontHandle? heading;
    private IFontHandle? headingLarge;

    public ThemeFonts()
    {
        try
        {
            var atlas = Plugin.PluginInterface.UiBuilder.FontAtlas;
            heading = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.TrumpGothic184));
            headingLarge = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.TrumpGothic23));
        }
        catch (Exception ex)
        {
            // Losing the game fonts costs us the headings, not the plugin.
            Plugin.Log.Warning(ex, "RaidPlan could not build its font handles; falling back to the default font.");
            heading = null;
            headingLarge = null;
        }
    }

    private static bool Usable(IFontHandle? handle) =>
        handle is { Available: true } && Plugin.Config.ThemeEnabled;

    /// <summary>Section headings. Falls through to the default font when the handle isn't ready.</summary>
    public IDisposable? PushHeading() => Usable(heading) ? heading!.Push() : null;

    public IDisposable? PushTitle() => Usable(headingLarge) ? headingLarge!.Push() : null;

    /// <summary>Dalamud's bundled FontAwesome, for icons on buttons.</summary>
    public static IDisposable? PushIcons()
    {
        var handle = Plugin.PluginInterface.UiBuilder.IconFontHandle;
        return handle is { Available: true } ? handle.Push() : null;
    }

    /// <summary>
    /// An icon button, when the icon font is there. Returns false from <c>available</c> if it is
    /// not, so the caller can fall back to a text button rather than drawing mojibake.
    /// </summary>
    /// <remarks>
    /// The font is popped before the caller gets a chance to add a tooltip on purpose: tooltip
    /// text drawn while the icon font is pushed comes out as a row of unrelated glyphs.
    /// </remarks>
    public static bool TryIconButton(FontAwesomeIcon icon, string id, System.Numerics.Vector2 size, out bool pressed)
    {
        pressed = false;

        var pushed = PushIcons();
        if (pushed == null)
            return false;

        try
        {
            pressed = ImGui.Button(FontAwesomeExtensions.ToIconString(icon) + "##" + id, size);
        }
        finally
        {
            pushed.Dispose();
        }

        return true;
    }

    /// <summary>Draws one FontAwesome glyph as text.</summary>
    public static void Icon(FontAwesomeIconGlyph glyph)
    {
        using var pushed = PushIcons();
        if (pushed == null)
            return;

        ImGui.TextUnformatted(glyph.Text);
    }

    public void Dispose()
    {
        heading?.Dispose();
        headingLarge?.Dispose();
        heading = null;
        headingLarge = null;
    }
}

/// <summary>
/// A FontAwesome codepoint plus the string ImGui needs to render it. Wrapped so call sites read
/// as names rather than as escape sequences.
/// </summary>
public readonly struct FontAwesomeIconGlyph
{
    public FontAwesomeIconGlyph(FontAwesomeIcon icon)
    {
        Icon = icon;
        Text = FontAwesomeExtensions.ToIconString(icon);
    }

    public FontAwesomeIcon Icon { get; }

    public string Text { get; }
}
