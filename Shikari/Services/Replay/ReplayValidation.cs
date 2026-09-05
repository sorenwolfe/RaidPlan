using System;
using System.Linq;

namespace Shikari.Services.Replay;

public static class ReplayValidation
{
    public static bool IsValid(ReplayAttempt attempt)
    {
        if (attempt.Version != 1 || !Guid.TryParseExact(attempt.Id, "N", out _) ||
            !float.IsFinite(attempt.Duration) || attempt.Duration < 0 || attempt.Duration > ReplayBuffer.MaxDuration ||
            attempt.Plan?.Slides == null || attempt.Plan.Roster == null || attempt.Plan.Timeline == null ||
            attempt.Plan.Arena == null || attempt.Frames == null || attempt.Mechanics == null ||
            attempt.Frames.Count > ReplayBuffer.MaxFrames || attempt.Mechanics.Count > ReplayBuffer.MaxMechanics ||
            attempt.Plan.Slides.Any(s => s == null || s.Items == null || s.Items.Any(i => i == null || i.Points == null)) ||
            attempt.Plan.Roster.Any(r => r == null) || attempt.Plan.Timeline.Any(e => e == null)) return false;
        var last = -1f;
        foreach (var frame in attempt.Frames)
        {
            if (frame == null || !float.IsFinite(frame.Time) || frame.Time < 0 || frame.Time <= last || frame.Time > attempt.Duration ||
                frame.Players == null || frame.Players.Count > 8 ||
                (frame.Valid && (!float.IsFinite(frame.BoardPerYalm) || frame.BoardPerYalm <= 0)) ||
                frame.Players.Any(p => p == null || !float.IsFinite(p.Board.X) || !float.IsFinite(p.Board.Y))) return false;
            last = frame.Time;
        }
        return attempt.Mechanics.All(m => m != null && float.IsFinite(m.Time) && m.Time >= 0 && m.Time <= attempt.Duration && float.IsFinite(m.ExpectedResolve));
    }
}
