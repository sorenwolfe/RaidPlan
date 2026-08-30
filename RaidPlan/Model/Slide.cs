using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace RaidPlan.Model;

/// <summary>One step of a strategy: a drawn arena plus the notes that go with it.</summary>
public sealed class Slide
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [DefaultValue("New slide")]
    public string Title { get; set; } = "New slide";

    /// <summary>Free-form notes shown beside the arena.</summary>
    [DefaultValue("")]
    public string Notes { get; set; } = string.Empty;

    public List<CanvasItem> Items { get; set; } = new();

    /// <summary>
    /// A reference image drawn faintly under the arena, for tracing a plan from somewhere else.
    /// Empty for most slides. The id names a file in the plugin's own backdrop folder.
    /// </summary>
    [DefaultValue("")]
    public string BackdropId { get; set; } = string.Empty;

    [DefaultValue(0.45f)]
    public float BackdropOpacity { get; set; } = 0.45f;

    public bool HasBackdrop => !string.IsNullOrEmpty(BackdropId);

    public bool ShouldSerializeItems() => Items.Count > 0;

    public bool ShouldSerializeBackdropOpacity() => HasBackdrop;

    public bool ShouldSerializeHasBackdrop() => false;

    public Slide Clone(string? newTitle = null)
    {
        return new Slide
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = newTitle ?? (Title + " (copy)"),
            Notes = Notes,
            BackdropId = BackdropId,
            BackdropOpacity = BackdropOpacity,
            Items = Items.Select(i => i.Clone()).ToList(),
        };
    }
}
