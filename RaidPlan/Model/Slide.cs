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

    public bool ShouldSerializeItems() => Items.Count > 0;

    public Slide Clone(string? newTitle = null)
    {
        return new Slide
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = newTitle ?? (Title + " (copy)"),
            Notes = Notes,
            Items = Items.Select(i => i.Clone()).ToList(),
        };
    }
}
