using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Shikari.Model;

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
        var path = PathFor(doc);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not delete plan file {File}.", path);
        }

        if (Active?.Id == doc.Id)
        {
            Active = Ordered.FirstOrDefault() ?? CreateNew("New plan");
            Plugin.Config.ActivePlanId = Active.Id;
        }
    }

    public void Save(PlanDocument doc)
    {
        try
        {
            doc.ModifiedUtc = DateTime.UtcNow;
            var json = JsonConvert.SerializeObject(doc, Settings);
            File.WriteAllText(PathFor(doc), json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not save plan {Name}.", doc.Name);
        }
    }

    public void SaveActive()
    {
        if (Active != null)
            Save(Active);
    }

    public void SaveAll()
    {
        foreach (var doc in plans.Values)
            Save(doc);
    }

    private string PathFor(PlanDocument doc) => Path.Combine(directory, doc.Id + ".json");
}
