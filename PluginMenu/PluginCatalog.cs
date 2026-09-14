using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Plugin;

namespace PluginMenu;

public sealed class PluginEntry
{
    public string InternalName = string.Empty;
    public string RawName = string.Empty;      // exactly what the manifest says
    public string DisplayName = string.Empty;  // what the menu shows
    public string? IconUrl;
    public string Version = string.Empty;
    public bool HasMainUi;
    public bool HasConfigUi;
    public bool IsDev;
    public bool IsTesting;
    public IExposedPlugin Plugin = null!;

    public bool Launchable => this.HasMainUi || this.HasConfigUi;
}

// Everything that touches Dalamud's plugin list lives here, so the windows only ever
// deal with a plain list of rows.
public sealed class PluginCatalog
{
    private readonly Configuration config;
    private List<PluginEntry> entries = new();
    private DateTime builtAt = DateTime.MinValue;

    public PluginCatalog(Configuration config) => this.config = config;

    public IReadOnlyList<PluginEntry> Entries => this.entries;

    // InstalledPlugins allocates a fresh wrapper per plugin every time it is read, so
    // it is built when the menu opens rather than every frame. Opening the menu is also
    // the moment the list needs to be right, which is what Umbra's version gets wrong.
    public void Refresh(bool force = false)
    {
        if (!force && (DateTime.UtcNow - this.builtAt).TotalSeconds < 1)
            return;

        this.builtAt = DateTime.UtcNow;

        var built = new List<PluginEntry>();

        try
        {
            foreach (var plugin in Service.Interface.InstalledPlugins)
            {
                try
                {
                    if (!plugin.IsLoaded)
                        continue;

                    var raw = string.IsNullOrWhiteSpace(plugin.Name) ? plugin.InternalName : plugin.Name;

                    built.Add(new PluginEntry
                    {
                        InternalName = plugin.InternalName,
                        RawName = raw,
                        DisplayName = CleanName(raw),
                        IconUrl = SafeIconUrl(plugin),
                        Version = plugin.Version?.ToString() ?? string.Empty,
                        HasMainUi = plugin.HasMainUi,
                        HasConfigUi = plugin.HasConfigUi,
                        IsDev = plugin.IsDev,
                        IsTesting = plugin.IsTesting,
                        Plugin = plugin,
                    });
                }
                catch (Exception ex)
                {
                    // One plugin in a strange state should cost that row, not the menu.
                    Service.Log.Verbose(ex, "Skipped a plugin while building the list.");
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "Could not read the installed plugin list.");
        }

        built.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        this.entries = built;
    }

    private static string? SafeIconUrl(IExposedPlugin plugin)
    {
        try
        {
            var url = plugin.Manifest?.IconUrl;
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch
        {
            return null;
        }
    }

    // Rows the menu should actually draw, after the hidden list and the no-window rule.
    public IEnumerable<PluginEntry> Visible(string filter)
    {
        var query = this.entries.Where(e => !this.config.Hidden.Contains(e.InternalName));

        if (this.config.OnlyWithWindows)
            query = query.Where(e => e.Launchable);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var needle = filter.Trim();
            query = query.Where(e =>
                e.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || e.InternalName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    // A trailing version number, with or without a v and with or without brackets.
    // The bare form needs at least one dot, so a plugin genuinely called something
    // like "Thing 2" keeps its name.
    private static readonly Regex TrailingVersion = new(
        @"[\s\-_,:|]*[\[\(]?\s*v(?:er(?:sion)?)?\.?\s*\d+(?:\.\d+){0,3}[a-z]?\s*[\]\)]?\s*$|[\s\-_,:|]*[\[\(]?\s*\d+(?:\.\d+){1,3}[a-z]?\s*[\]\)]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Channel and build markers that plugins tack onto their own name.
    private static readonly Regex TrailingTag = new(
        @"[\s\-_,:|]*[\[\(]\s*(testing|test(?:ing)? version|test build|beta|alpha|dev(?:elopment)?|preview|nightly|early access|wip)\s*[\]\)]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Whitespace = new(@"\s{2,}", RegexOptions.Compiled);

    // Applied repeatedly because "Thing (Testing) v1.2.3" needs two passes. Bounded so
    // a pathological name cannot loop, and the original is always kept for the tooltip.
    public static string CleanName(string name)
    {
        var result = name.Trim();

        for (var i = 0; i < 4; i++)
        {
            var before = result;
            result = TrailingTag.Replace(result, string.Empty).Trim();
            result = TrailingVersion.Replace(result, string.Empty).Trim();

            if (result == before)
                break;
        }

        result = Whitespace.Replace(result, " ").Trim(' ', '-', '_', ':', '|', ',');

        // Never hand back nothing, which would happen for a plugin named only "v1.0".
        return string.IsNullOrWhiteSpace(result) ? name.Trim() : result;
    }

    // Opening another plugin's window raises an event inside that plugin. Deferred to
    // the next framework tick so it does not run in the middle of our ImGui frame, and
    // wrapped because a plugin that throws in its handler should not take us with it.
    public void OpenMain(PluginEntry entry) => Invoke(entry, main: true);

    public void OpenSettings(PluginEntry entry) => Invoke(entry, main: false);

    // Row click: do the preferred thing if the plugin has it, otherwise the other one.
    public void OpenPrimary(PluginEntry entry)
    {
        var wantsMain = this.config.RowClick == PrimaryAction.Open;

        if (wantsMain)
        {
            if (entry.HasMainUi) OpenMain(entry);
            else if (entry.HasConfigUi) OpenSettings(entry);
            return;
        }

        if (entry.HasConfigUi) OpenSettings(entry);
        else if (entry.HasMainUi) OpenMain(entry);
    }

    private static void Invoke(PluginEntry entry, bool main)
    {
        // Both methods throw if the matching Has flag is false, so this is checked
        // again here rather than trusting the row that was drawn a frame ago.
        if (main && !entry.HasMainUi)
            return;
        if (!main && !entry.HasConfigUi)
            return;

        Service.Framework.RunOnTick(() =>
        {
            try
            {
                if (main)
                    entry.Plugin.OpenMainUi();
                else
                    entry.Plugin.OpenConfigUi();
            }
            catch (Exception ex)
            {
                Service.Log.Warning(ex, $"Could not open the {(main ? "main" : "settings")} window for {entry.InternalName}.");
            }
        });
    }
}
