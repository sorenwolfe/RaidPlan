using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace Shikari.UI.Theme;

/// <summary>
/// Type for the theme, taken from the game's own fonts rather than shipped with the plugin.
/// TrumpGothic is the condensed face the game uses for headings; using it is what stops the
/// window reading as a generic ImGui panel, and it costs nothing to license or download.
/// </summary>
public sealed class ThemeFonts : IDisposable
{
    private IFontHandle? heading;
    private IFontHandle? headingLarge;
    private IFontHandle? mixed;

    public ThemeFonts()
    {
        try
        {
            var atlas = Plugin.PluginInterface.UiBuilder.FontAtlas;
            heading = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.TrumpGothic184));
            headingLarge = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.TrumpGothic23));
            mixed = BuildMixed(atlas);
        }
        catch (Exception ex)
        {
            // Losing the game fonts costs us the headings, not the plugin.
            Plugin.Log.Warning(ex, "Shikari could not build its font handles; falling back to the default font.");
            heading = null;
            headingLarge = null;
            mixed = null;
        }
    }

    /// <summary>
    /// The normal text font with FontAwesome merged into it, so one label can carry an icon and
    /// words. Without the merge the icon is a glyph the text font has never heard of, and it
    /// renders as an empty box.
    /// </summary>
    private static IFontHandle? BuildMixed(IFontAtlas atlas)
    {
        try
        {
            // Built from the player's own default font spec rather than a hardcoded one, so
            // whatever font they picked in Dalamud's settings is what the labels use.
            var spec = Plugin.PluginInterface.UiBuilder.DefaultFontSpec;

            return spec.CreateFontHandle(atlas, e => e.OnPreBuild(toolkit =>
                toolkit.AddFontAwesomeIconFont(new SafeFontConfig
                {
                    SizePx = spec.SizePx,
                    MergeFont = toolkit.Font,
                })));
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Shikari could not merge the icon font; tabs will be text only.");
            return null;
        }
    }

    /// <summary>Whether a label may safely contain both an icon and text.</summary>
    public bool MixedAvailable => Plugin.Config.ThemeToolIcons && mixed is { Available: true };

    /// <summary>Pushes the merged font. Only call it when <see cref="MixedAvailable"/> is true.</summary>
    public IDisposable? PushMixed() => MixedAvailable ? mixed!.Push() : null;

    private static bool Usable(IFontHandle? handle) =>
        handle is { Available: true } && Plugin.Config.ThemeEnabled;

    /// <summary>Section headings. Falls through to the default font when the handle isn't ready.</summary>
    public IDisposable? PushHeading() => Usable(heading) ? heading!.Push() : null;

    public IDisposable? PushTitle() => Usable(headingLarge) ? headingLarge!.Push() : null;

    /// <summary>
    /// Dalamud's bundled FontAwesome. The fixed-width variant, because the proportional one gives
    /// every glyph its own advance width, and a square button then centres each icon differently
    /// from its neighbours.
    /// </summary>
    public static IDisposable? PushIcons()
    {
        var builder = Plugin.PluginInterface.UiBuilder;
        var handle = builder.IconFontFixedWidthHandle;

        if (handle is not { Available: true })
            handle = builder.IconFontHandle;

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

        // ImGui centres a label inside the frame padding, not inside the button. The theme's
        // padding is wider than the slack a square button leaves around an icon, so there is
        // nothing left to centre into and the glyph gets pinned against the padding edge.
        // Zero the padding for the button itself; the size passed in already accounts for it.
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, System.Numerics.Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new System.Numerics.Vector2(0.5f, 0.5f));

        try
        {
            pressed = ImGui.Button(FontAwesomeExtensions.ToIconString(icon) + "##" + id, size);
        }
        finally
        {
            ImGui.PopStyleVar(2);
            pushed.Dispose();
        }

        return true;
    }

    /// <summary>The glyph for an icon, as a string.</summary>
    public static string Glyph(FontAwesomeIcon icon) => FontAwesomeExtensions.ToIconString(icon);

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
        mixed?.Dispose();
        heading = null;
        headingLarge = null;
        mixed = null;
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
