using Lumina.Excel.Sheets;

namespace Shikari.Services;

/// <summary>
/// Works out what kind of content the current zone is. The sheet lookup only happens when the
/// zone actually changes, so this is cheap to ask every frame.
/// </summary>
public sealed class ZoneClassifier
{
    private uint lastTerritory = uint.MaxValue;

    public uint ContentTypeId { get; private set; }

    public bool HighEndDuty { get; private set; }

    /// <summary>Duty name, when the zone has one. Used for labelling, not for decisions.</summary>
    public string DutyName { get; private set; } = string.Empty;

    public bool IsRaidContent => ContentPolicy.IsRaidContent(ContentTypeId, HighEndDuty);

    public void Refresh()
    {
        var territory = Plugin.ClientState.TerritoryType;
        if (territory == lastTerritory)
            return;

        lastTerritory = territory;
        ContentTypeId = 0;
        HighEndDuty = false;
        DutyName = string.Empty;

        var row = Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territory);
        var duty = row?.ContentFinderCondition.ValueNullable;
        if (duty == null)
            return;

        ContentTypeId = duty.Value.ContentType.RowId;
        HighEndDuty = duty.Value.HighEndDuty;
        DutyName = duty.Value.Name.ExtractText();
    }
}
