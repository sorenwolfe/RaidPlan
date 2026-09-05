using System;

namespace Shikari.UI.Theme;

/// <summary>A short ease-out that can be redirected without jumping or delaying input.</summary>
public sealed class NavigationMotion
{
    public const float Duration = 0.16f;
    public float Position { get; private set; }
    private float start;
    private float elapsed;
    private int target = -1;

    public float Update(int selection, float deltaSeconds)
    {
        selection = Math.Clamp(selection, 0, 2);
        if (target < 0)
        {
            Position = start = selection;
            target = selection;
            elapsed = Duration;
        }
        else if (target != selection)
        {
            start = Position;
            target = selection;
            elapsed = 0;
        }
        if (float.IsFinite(deltaSeconds) && deltaSeconds > 0)
            elapsed = MathF.Min(Duration, elapsed + deltaSeconds);
        var remaining = 1f - Math.Clamp(elapsed / Duration, 0, 1);
        Position = start + (target - start) * (1f - remaining * remaining * remaining);
        return Position;
    }
}
