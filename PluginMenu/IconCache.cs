using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;

namespace PluginMenu;

// Dalamud downloads plugin icons for its own installer but does not hand that cache to
// plugins, and almost nothing ships images/icon.png as a loose file in its release zip.
// So the icon has to come from the manifest's IconUrl, fetched once and kept on disk.
public sealed class IconCache : IDisposable
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string directory;

    private readonly ConcurrentDictionary<string, byte> inFlight = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();

    // 512x512 png is the documented icon size. This is a sanity ceiling, not a target.
    private const long MaxBytes = 4 * 1024 * 1024;

    public IconCache()
    {
        this.directory = Path.Combine(Service.Interface.ConfigDirectory.FullName, "icons");

        try
        {
            Directory.CreateDirectory(this.directory);
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "Could not create the icon cache directory; icons will be off.");
        }
    }

    // Returns null while the icon is missing, downloading or decoding. The caller draws
    // a monogram tile in that case, so a row never waits on the network.
    public IDalamudTextureWrap? TryGet(PluginEntry entry)
    {
        var path = this.PathFor(entry.InternalName);
        if (path == null)
            return null;

        if (File.Exists(path))
        {
            try
            {
                return Service.Textures.GetFromFile(path).GetWrapOrDefault();
            }
            catch (Exception ex)
            {
                Service.Log.Verbose(ex, $"Could not decode the cached icon for {entry.InternalName}.");
                return null;
            }
        }

        this.QueueDownload(entry, path);
        return null;
    }

    private string? PathFor(string internalName)
    {
        var safe = new string(internalName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrEmpty(safe) ? null : Path.Combine(this.directory, safe + ".png");
    }

    private void QueueDownload(PluginEntry entry, string path)
    {
        if (string.IsNullOrWhiteSpace(entry.IconUrl))
            return;

        // One attempt per session. A plugin whose icon URL is dead should not be
        // retried every time the menu opens.
        if (this.failed.ContainsKey(entry.InternalName))
            return;

        if (!Uri.TryCreate(entry.IconUrl, UriKind.Absolute, out var uri))
            return;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return;

        if (!this.inFlight.TryAdd(entry.InternalName, 0))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                using var response = await this.http.GetAsync(uri).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength > MaxBytes)
                    throw new InvalidOperationException("Icon is larger than the cache allows.");

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (bytes.Length == 0 || bytes.Length > MaxBytes)
                    throw new InvalidOperationException("Icon was empty or too large.");

                // Written to a temporary file first, so a half-downloaded icon is never
                // handed to the texture provider.
                var temp = path + ".tmp";
                await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
                File.Move(temp, path, true);
            }
            catch (Exception ex)
            {
                this.failed.TryAdd(entry.InternalName, 0);
                Service.Log.Verbose(ex, $"Could not fetch the icon for {entry.InternalName}.");
            }
            finally
            {
                this.inFlight.TryRemove(entry.InternalName, out _);
            }
        });
    }

    // Exposed for the settings window, so a bad or stale icon can be re-fetched without
    // hunting through the config folder.
    public void ClearCache()
    {
        this.failed.Clear();

        try
        {
            if (!Directory.Exists(this.directory))
                return;

            foreach (var file in Directory.EnumerateFiles(this.directory))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Service.Log.Verbose(ex, "Could not delete a cached icon.");
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "Could not clear the icon cache.");
        }
    }

    public void Dispose() => this.http.Dispose();
}
