using System;
using System.Numerics;

namespace Shikari.UI.Theme;

/// <summary>
/// The colours the dark glass theme is built from. Stored as 0xRRGGBB and packed on use, because
/// ImGui wants ABGR and hand-writing that is how you end up with an orange that should be blue.
/// </summary>
public static class Palette
{
    public const uint Window = 0x09090B;
    public const uint Panel = 0x111113;
    public const uint PanelRaised = 0x1B1B1F;
    public const uint Hover = 0x302125;
    public const uint Pressed = 0x211216;

    public const uint Text = 0xF5F5F6;
    public const uint TextMuted = 0xB3B3BA;
    public const uint TextDim = 0x98989F;

    public const uint DefaultAccent = 0xEF4054;
    public const uint Attention = 0xFFB74A;
    public const uint Good = 0xE1E1E6;
    public const uint Danger = 0xFF6474;

    /// <summary>Packs 0xRRGGBB plus an alpha into the 0xAABBGGRR ImGui wants.</summary>
    public static uint Pack(uint rgb, float alpha)
    {
        var r = (rgb >> 16) & 0xFF;
        var g = (rgb >> 8) & 0xFF;
        var b = rgb & 0xFF;
        var a = (uint)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255);

        return (a << 24) | (b << 16) | (g << 8) | r;
    }

    public static uint Pack(uint rgb) => Pack(rgb, 1f);

    public static Vector4 Vec(uint rgb, float alpha)
    {
        return new Vector4(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            alpha);
    }

    public static Vector4 Vec(uint rgb) => Vec(rgb, 1f);

    /// <summary>The accent the player picked, or the default when they never touched it.</summary>
    public static uint Accent => Plugin.Config.ThemeAccent == 0 ? DefaultAccent : Plugin.Config.ThemeAccent;

    /// <summary>Border white at a given strength. Every border in the theme is one of these.</summary>
    public static uint Line(float alpha) => Pack(0xFFFFFF, alpha);
}
