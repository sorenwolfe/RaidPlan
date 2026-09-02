using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface.Textures;

namespace Shikari.Services;

/// <summary>
/// The reference images people trace plans from. A picture is copied into the plugin's own folder
/// when it is chosen, so a plan does not break when the original screenshot is tidied away.
/// </summary>
public sealed class BackdropStore
{
    private static readonly string[] Allowed = { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

    /// <summary>Beyond this a screenshot is almost certainly not what they meant to pick.</summary>
    public const long MaxBytes = 12 * 1024 * 1024;

    private readonly string directory;
    private readonly Dictionary<string, ISharedImmediateTexture> loaded = new();

    public BackdropStore()
    {
        directory = Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "backdrops");
        Directory.CreateDirectory(directory);
    }

    public static bool LooksLikeImage(string path) =>
        Allowed.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// Copies an image in and returns its id, or null with a reason the player can act on.
    /// </summary>
    public string? Adopt(string sourcePath, out string error)
    {
        error = string.Empty;
        sourcePath = (sourcePath ?? string.Empty).Trim().Trim('"');

        if (sourcePath.Length == 0)
        {
            error = "No file given.";
            return null;
        }

        if (!File.Exists(sourcePath))
        {
            error = "Nothing at that path. Paste the full path to the image file.";
            return null;
        }

        if (!LooksLikeImage(sourcePath))
        {
            error = "That is not an image. PNG, JPG, BMP and WEBP work.";
            return null;
        }

        try
        {
            var info = new FileInfo(sourcePath);
            if (info.Length > MaxBytes)
            {
                error = $"That image is {info.Length / (1024 * 1024)} MB. Keep it under {MaxBytes / (1024 * 1024)} MB.";
                return null;
            }

            var id = Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath).ToLowerInvariant();
            File.Copy(sourcePath, Path.Combine(directory, id), overwrite: false);

            return id;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not take a copy of the backdrop {Path}.", sourcePath);
            error = "Could not read that file: " + ex.Message;
            return null;
        }
    }

    /// <summary>The texture for a backdrop, or null when the file has gone.</summary>
    public ISharedImmediateTexture? Get(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (loaded.TryGetValue(id, out var cached))
            return cached;

        var path = Path.Combine(directory, id);
        if (!File.Exists(path))
            return null;

        try
        {
            var texture = Plugin.TextureProvider.GetFromFileAbsolute(path);
            loaded[id] = texture;
            return texture;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not load the backdrop {Id}.", id);
            return null;
        }
    }

    /// <summary>Deletes a backdrop nothing refers to any more.</summary>
    public void Forget(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        loaded.Remove(id);

        try
        {
            var path = Path.Combine(directory, id);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not delete the backdrop {Id}.", id);
        }
    }

    /// <summary>Clears the texture cache without touching the files.</summary>
    public void Dispose() => loaded.Clear();
}
