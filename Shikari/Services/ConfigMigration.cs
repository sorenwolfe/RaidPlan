using System;
using System.IO;

namespace Shikari.Services;

/// <summary>
/// Brings settings and plans across from the days when this was called RaidPlan.
/// </summary>
/// <remarks>
/// Dalamud keys a plugin's storage by its internal name, so renaming the plugin points it at an
/// empty folder. To Dalamud this is simply a different plugin that happens to look familiar; to
/// somebody who has written thirty plans it is every one of them gone, with the old ones sitting
/// intact in a folder they have no reason to know exists.
///
/// So the first run of the renamed plugin looks next door. It copies rather than moves: if any of
/// this goes wrong, or the player decides to go back, the originals are still there. And it only
/// ever writes into an empty destination, so it cannot overwrite work done under the new name —
/// which matters because this runs on every startup, not just the first.
/// </remarks>
public static class ConfigMigration
{
    /// <summary>What the plugin used to be called.</summary>
    public const string PreviousName = "RaidPlan";

    /// <summary>
    /// Copies the old settings file and plans folder across, if there is nothing here yet.
    /// </summary>
    /// <returns>How many plans were brought over. Zero means there was nothing to do.</returns>
    /// <remarks>
    /// Must run before the settings are read, because reading them is what creates the new file
    /// and makes this look like a plugin that has already been set up.
    /// </remarks>
    public static int Run(string configDirectory, string configFile)
    {
        try
        {
            var parent = Directory.GetParent(configDirectory)?.FullName;
            if (parent == null)
                return 0;

            var oldDirectory = Path.Combine(parent, PreviousName);
            var oldFile = Path.Combine(parent, PreviousName + ".json");

            var moved = 0;

            // The settings file. Only when the new one does not exist: a player who has already
            // configured the renamed plugin must not have it reverted by an old file next door.
            if (File.Exists(oldFile) && !File.Exists(configFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configFile)!);
                File.Copy(oldFile, configFile);
            }

            if (!Directory.Exists(oldDirectory))
                return 0;

            moved += CopyPlans(Path.Combine(oldDirectory, "plans"), Path.Combine(configDirectory, "plans"));

            return moved;
        }
        catch (Exception ex)
        {
            // Never fatal. Starting with no plans is bad; refusing to start at all is worse, and
            // the originals are untouched either way.
            Plugin.Log.Error(ex, "Could not bring the old RaidPlan settings across.");
            return 0;
        }
    }

    /// <summary>Copies plan files that do not already exist under the new name.</summary>
    private static int CopyPlans(string from, string to)
    {
        if (!Directory.Exists(from))
            return 0;

        Directory.CreateDirectory(to);
        var copied = 0;

        foreach (var file in Directory.EnumerateFiles(from, "*.json"))
        {
            var destination = Path.Combine(to, Path.GetFileName(file));

            // Skipped rather than overwritten. A plan edited under the new name is the newer one.
            if (File.Exists(destination))
                continue;

            File.Copy(file, destination);
            copied++;
        }

        return copied;
    }
}
