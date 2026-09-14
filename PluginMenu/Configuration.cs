using System;
using System.Collections.Generic;
using System.Reflection;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace PluginMenu;

public enum PrimaryAction { Open, Settings }

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // The floating button
    public bool ShowButton = true;
    public string ButtonLabel = "Dalamud Plugins";
    public bool LockPosition = false;
    public float Scale = 1.0f;

    // The menu itself
    public bool ShowIcons = true;
    public bool ShowSearch = true;
    public bool CleanNames = true;
    public int MenuWidth = 360;                // pixels wide, before scale
    public int MenuHeight = 420;               // pixels of list before it scrolls
    public bool CloseAfterClick = true;

    // Plugins that expose neither a main window nor a settings window cannot be
    // launched from here, so they are off the list unless you want to see them.
    public bool OnlyWithWindows = true;

    // Clicking the row itself does this, when the plugin has it. Open falls back to
    // Settings and the other way round, so a row click always does something.
    public PrimaryAction RowClick = PrimaryAction.Open;

    // Internal names, because display names change with a plugin update and this has
    // to survive that. Everything not in here is shown, so new plugins appear on
    // their own rather than needing to be enabled one at a time.
    public HashSet<string> Hidden = new();

    [NonSerialized] private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi) => this.pluginInterface = pi;

    public void Save() => this.pluginInterface?.SavePluginConfig(this);

    // Reflection over the public fields rather than a hand-written list, so a setting
    // added later cannot be missed.
    public void ResetToDefaults()
    {
        var defaults = new Configuration();

        foreach (var field in typeof(Configuration).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.IsInitOnly || field.IsLiteral)
                continue;

            field.SetValue(this, field.GetValue(defaults));
        }

        this.Save();
    }
}
