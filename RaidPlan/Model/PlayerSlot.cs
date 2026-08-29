using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace RaidPlan.Model;

/// <summary>
/// One seat in the raid. A plan is written against seats rather than character names so a
/// substitute can be dropped in without rewriting every assignment.
/// </summary>
public sealed class PlayerSlot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Nothing references a seat by id; the roster index is the identity.</summary>
    public bool ShouldSerializeId() => false;

    /// <summary>Character name, if the seat has been filled.</summary>
    [DefaultValue("")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short name shown on tokens. Falls back to the first word of <see cref="Name"/>.</summary>
    [DefaultValue("")]
    public string Nickname { get; set; } = string.Empty;

    /// <summary>ClassJob sheet row id. 0 means unset.</summary>
    [DefaultValue(0u)]
    public uint JobId { get; set; }

    [DefaultValue(RaidRole.Unknown)]
    public RaidRole Role { get; set; } = RaidRole.Unknown;

    /// <summary>Packed ABGR token colour. 0 means "use the role's default".</summary>
    [DefaultValue(0u)]
    public uint Color { get; set; }

    /// <summary>Label shown when the seat is empty, e.g. "T1", "H2", "M1".</summary>
    [DefaultValue("")]
    public string Placeholder { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Nickname))
                return Nickname;
            if (!string.IsNullOrWhiteSpace(Name))
            {
                var space = Name.IndexOf(' ');
                return space > 0 ? Name[..space] : Name;
            }

            return string.IsNullOrWhiteSpace(Placeholder) ? "?" : Placeholder;
        }
    }

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Name) && JobId == 0;
}
