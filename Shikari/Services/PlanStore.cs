using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Shikari.Model;
using Shikari.Services.Storage;

namespace Shikari.Services;

/// <summary>
/// Owns the on-disk library of plans and tracks which one the planner is editing.
/// Plans live as individual JSON files under the plugin's config directory so they can be
/// backed up, diffed, or hand-edited.
/// </summary>
public sealed class PlanStore
{
    private static readonly JsonSerializerSettings Settings = PlanJson.Readable();

    private readonly string directory;
    private readonly Dictionary<string, PlanDocument> plans = new();

    public PlanStore()
    {
        directory = Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "plans");
        Directory.CreateDirectory(directory);
        LoadAll();
    }

    public PlanDocument? Active { get; private set; }
    public string? LastSaveError { get; private set; }

    public IReadOnlyCollection<PlanDocument> All => plans.Values;

    public IEnumerable<PlanDocument> Ordered =>
        plans.Values.OrderByDescending(p => p.ModifiedUtc);

    private void LoadAll()
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var doc = JsonConvert.DeserializeObject<PlanDocument>(json, Settings);
                if (doc == null)
                    continue;

                PlanNormaliser.Normalise(doc);
                plans[doc.Id] = doc;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Could not read plan file {File}.", file);
            }
        }

        var wanted = Plugin.Config.ActivePlanId;
        if (!string.IsNullOrEmpty(wanted) && plans.TryGetValue(wanted, out var active))
        {
            Active = active;
        }
        else
        {
            Active = Ordered.FirstOrDefault();
        }

        if (Active == null)
        {
            Active = PlanDocument.CreateDefault("My first plan");
            plans[Active.Id] = Active;
            Save(Active);
        }

        Plugin.Config.ActivePlanId = Active.Id;
    }

    public void SetActive(PlanDocument plan)
    {
        if (!plans.ContainsKey(plan.Id))
            plans[plan.Id] = plan;

        Active = plan;
        Plugin.Config.ActivePlanId = plan.Id;
        Plugin.PluginInterface.SavePluginConfig(Plugin.Config);
    }

    public PlanDocument CreateNew(string name)
    {
        var doc = PlanDocument.CreateDefault(name);
        plans[doc.Id] = doc;
        Save(doc);
        SetActive(doc);
        return doc;
    }

    /// <summary>Adds an imported plan, giving it a fresh id if one with that id already exists.</summary>
    public PlanDocument Import(PlanDocument doc, bool replaceExisting)
    {
        // Imports enter the editor with the same danger palette as newly drawn AoEs.
        // Do this here rather than on load, so subsequent colour edits survive saving.
        foreach (var slide in doc.Slides)
            foreach (var item in slide.Items)
                if (item.Kind == CanvasItemKind.Zone)
                    item.Color = CanvasItem.DefaultAoeColor;

        if (!replaceExisting && plans.ContainsKey(doc.Id))
        {
            doc.Id = Guid.NewGuid().ToString("N");
            doc.Name += " (imported)";
        }

        plans[doc.Id] = doc;
        Save(doc);
        SetActive(doc);
        return doc;
    }

    public PlanDocument Duplicate(PlanDocument source)
    {
        var json = JsonConvert.SerializeObject(source, Settings);
        var copy = PlanNormaliser.Normalise(JsonConvert.DeserializeObject<PlanDocument>(json, Settings)!);
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = source.Name + " (copy)";
        plans[copy.Id] = copy;
        Save(copy);
        return copy;
    }

    public void Delete(PlanDocument doc)
    {
        plans.Remove(doc.Id);
        try
        {
            var path = PathFor(doc);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not delete plan {Name}.", doc.Name);
        }

        if (Active?.Id == doc.Id)
        {
            Active = Ordered.FirstOrDefault() ?? CreateNew("New plan");
            Plugin.Config.ActivePlanId = Active.Id;
        }
    }

    public bool Save(PlanDocument doc)
    {
        var previousModified = doc.ModifiedUtc;
        try
        {
            var path = PathFor(doc);
            doc.ModifiedUtc = DateTime.UtcNow;
            var json = JsonConvert.SerializeObject(doc, Settings);
            AtomicFile.WriteAllText(path, json);
            LastSaveError = null;
            return true;
        }
        catch (Exception ex)
        {
            doc.ModifiedUtc = previousModified;
            LastSaveError = "Could not save " + doc.Name + ": " + ex.Message;
            Plugin.Log.Error(ex, "Could not save plan {Name}.", doc.Name);
            return false;
        }
    }

    public bool SaveActive() => Active == null || Save(Active);

    public void SaveAll()
    {
        foreach (var doc in plans.Values)
            Save(doc);
    }

    private string PathFor(PlanDocument doc)
    {
        if (!Guid.TryParseExact(doc.Id, "N", out _))
            throw new InvalidDataException("A plan id must be a 32-character hexadecimal identifier.");
        return Path.Combine(directory, doc.Id + ".json");
    }
}
