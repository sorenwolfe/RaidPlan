using System.Collections.Generic;
using RaidPlan.Model;

namespace RaidPlan.Services;

/// <summary>
/// Fills in anything a plan might be missing before the UI touches it. Plans arrive from disk,
/// from share codes and from hand editing, and the drawing code should not have to null-check
/// every list it walks.
/// </summary>
public static class PlanNormaliser
{
    public static RaidPlanDocument Normalise(RaidPlanDocument doc)
    {
        doc.Arena ??= new ArenaSettings();
        doc.Roster ??= new List<PlayerSlot>();
        doc.Slides ??= new List<Slide>();
        doc.Timeline ??= new List<TimelineEntry>();

        if (doc.Slides.Count == 0)
            doc.Slides.Add(new Slide { Title = "Slide 1" });

        if (doc.Roster.Count == 0)
            doc.Roster = RaidPlanDocument.CreateDefault().Roster;

        foreach (var slide in doc.Slides)
        {
            slide.Items ??= new List<CanvasItem>();
            foreach (var item in slide.Items)
                item.Points ??= new List<System.Numerics.Vector2>();
        }

        foreach (var entry in doc.Timeline)
        {
            entry.Assignments ??= new List<Assignment>();
            entry.SlotCallText ??= new Dictionary<int, string>();
        }

        return doc;
    }
}
