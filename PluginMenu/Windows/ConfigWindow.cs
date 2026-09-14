using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace PluginMenu.Windows;

public sealed class ConfigWindow : Window
{
    private const string IssueUrl = "https://github.com/joshua88wa/FFXIV-Dalamud-PluginMenu/issues";

    private readonly Configuration config;
    private readonly PluginCatalog catalog;
    private readonly IconCache icons;

    private string hiddenFilter = string.Empty;

    public ConfigWindow(Configuration config, PluginCatalog catalog, IconCache icons)
        : base("Plugin Menu - Settings##PluginMenuConfig")
    {
        this.config = config;
        this.catalog = catalog;
        this.icons = icons;
        this.Size = new Vector2(460, 560);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnOpen() => this.catalog.Refresh(true);

    public override void Draw()
    {
        var dirty = false;

        if (ImGui.BeginTabBar("##pm_tabs"))
        {
            if (ImGui.BeginTabItem("Button"))
            {
                dirty |= this.DrawButtonTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Menu"))
            {
                dirty |= this.DrawMenuTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Plugins"))
            {
                dirty |= this.DrawPluginsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Other"))
            {
                dirty |= this.DrawOtherTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        if (dirty)
            this.config.Save();
    }

    private bool DrawButtonTab()
    {
        var dirty = false;

        var show = this.config.ShowButton;
        if (ImGui.Checkbox("Show the floating button", ref show))
        {
            this.config.ShowButton = show;
            dirty = true;
        }

        HelpMarker("With this off, /pmenu still opens the menu. The menu appears where the button would have been.");

        var label = this.config.ButtonLabel;
        ImGui.SetNextItemWidth(220);
        if (ImGui.InputText("Button label", ref label, 64))
        {
            this.config.ButtonLabel = label;
            dirty = true;
        }

        var locked = this.config.LockPosition;
        if (ImGui.Checkbox("Lock button position", ref locked))
        {
            this.config.LockPosition = locked;
            dirty = true;
        }

        HelpMarker("Locking also drops the window frame behind the button and stops it taking clicks anywhere except on the button itself, so it sits on the HUD without eating anything. Unlock it to move it again.");

        var scalePercent = (int)Math.Round(this.config.Scale * 100f);
        ImGui.SetNextItemWidth(160);
        if (ImGui.InputInt("Scale (%)", ref scalePercent, 5, 25))
        {
            this.config.Scale = Math.Clamp(scalePercent, 50, 300) / 100f;
            dirty = true;
        }

        ImGui.Separator();
        TextHint("Shift and drag moves the button, from anywhere on it.");
        TextHint("/pmenu opens the menu.");
        TextHint("/pmenu config opens this window.");

        return dirty;
    }

    private bool DrawMenuTab()
    {
        var dirty = false;

        var width = this.config.MenuWidth;
        ImGui.SetNextItemWidth(160);
        if (ImGui.InputInt("Menu width", ref width, 10, 50))
        {
            this.config.MenuWidth = Math.Clamp(width, 200, 900);
            dirty = true;
        }

        var height = this.config.MenuHeight;
        ImGui.SetNextItemWidth(160);
        if (ImGui.InputInt("Menu height", ref height, 10, 50))
        {
            this.config.MenuHeight = Math.Clamp(height, 120, 1200);
            dirty = true;
        }

        HelpMarker("How tall the list is allowed to get before it scrolls. A short list is still drawn short.");

        ImGui.Separator();

        var search = this.config.ShowSearch;
        if (ImGui.Checkbox("Show the search box", ref search))
        {
            this.config.ShowSearch = search;
            dirty = true;
        }

        var showIcons = this.config.ShowIcons;
        if (ImGui.Checkbox("Show plugin icons", ref showIcons))
        {
            this.config.ShowIcons = showIcons;
            dirty = true;
        }

        HelpMarker("Icons come from each plugin's manifest and are downloaded once, then kept in this plugin's config folder. Until one arrives, or if a plugin has no icon, a coloured initial is drawn instead.");

        var clean = this.config.CleanNames;
        if (ImGui.Checkbox("Tidy up plugin names", ref clean))
        {
            this.config.CleanNames = clean;
            dirty = true;
        }

        HelpMarker("Strips a trailing version number and markers like (testing) from the displayed name. The full name is still in the tooltip.");

        var onlyWindows = this.config.OnlyWithWindows;
        if (ImGui.Checkbox("Only list plugins that have a window", ref onlyWindows))
        {
            this.config.OnlyWithWindows = onlyWindows;
            dirty = true;
        }

        HelpMarker("Plugins that register no window have nothing for this menu to open, so they are off the list by default. They still appear on the Plugins tab, marked with the reason.");

        var close = this.config.CloseAfterClick;
        if (ImGui.Checkbox("Close the menu after opening a plugin", ref close))
        {
            this.config.CloseAfterClick = close;
            dirty = true;
        }

        ImGui.Separator();

        ImGui.SetNextItemWidth(220);
        var rowClick = (int)this.config.RowClick;
        if (ImGui.Combo("Clicking a row", ref rowClick, "Opens the plugin\0Opens its settings\0"))
        {
            this.config.RowClick = (PrimaryAction)rowClick;
            dirty = true;
        }

        HelpMarker("If the plugin does not have the one you picked, the other one is used, so a row click always does something.");

        return dirty;
    }

    private bool DrawPluginsTab()
    {
        var dirty = false;

        var total = this.catalog.Entries.Count;
        var listed = this.catalog.Visible(string.Empty).Count();

        TextHint($"{listed} of {total} installed plugins are in the menu. Untick one to keep it out, tick it again to bring it back.");

        if (ImGui.Button("Refresh"))
            this.catalog.Refresh(true);

        ImGui.SameLine();
        if (ImGui.Button("Show all"))
        {
            this.config.Hidden.Clear();
            dirty = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("Hide all"))
        {
            foreach (var entry in this.catalog.Entries)
                this.config.Hidden.Add(entry.InternalName);
            dirty = true;
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##pm_hidden_search", "Search", ref this.hiddenFilter, 128);

        ImGui.Separator();

        ImGui.BeginChild("##pm_hidden_list", new Vector2(0, 0));

        var needle = this.hiddenFilter.Trim();
        foreach (var entry in this.catalog.Entries)
        {
            if (needle.Length > 0
                && !entry.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                && !entry.InternalName.Contains(needle, StringComparison.OrdinalIgnoreCase))
                continue;

            dirty |= this.DrawPluginRow(entry);
        }

        ImGui.EndChild();

        return dirty;
    }

    // Every installed plugin gets a row here, including the ones the menu is currently
    // leaving out, because "why is this not in my list" is a question this tab should
    // answer rather than raise.
    private bool DrawPluginRow(PluginEntry entry)
    {
        var dirty = false;

        var hiddenByRule = this.config.OnlyWithWindows && !entry.Launchable;
        var visible = !this.config.Hidden.Contains(entry.InternalName);

        // Greyed while a rule is keeping it out, so the reason is visible at a glance
        // and the tick still reads as your own choice rather than the current state.
        if (hiddenByRule)
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);

        if (ImGui.Checkbox($"{entry.DisplayName}##vis_{entry.InternalName}", ref visible))
        {
            if (visible)
                this.config.Hidden.Remove(entry.InternalName);
            else
                this.config.Hidden.Add(entry.InternalName);

            dirty = true;
        }

        if (hiddenByRule)
            ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(entry.RawName);
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextUnformatted($"{entry.InternalName}  {entry.Version}");
            ImGui.TextUnformatted(WindowSummary(entry));
            ImGui.PopStyleColor();
            ImGui.EndTooltip();
        }

        if (!hiddenByRule)
            return dirty;

        ImGui.SameLine();
        ImGui.TextDisabled("(no window)");

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 24f);
            ImGui.TextWrapped("This plugin registers neither a main window nor a settings window, so the menu has nothing to open for it. That does not mean it is idle: it may run in the background, or draw on top of a game window when one is open. It stays out while \"Only list plugins that have a window\" is on, over on the Menu tab.");
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        return dirty;
    }

    private static string WindowSummary(PluginEntry entry)
    {
        if (entry.HasMainUi && entry.HasConfigUi)
            return "Has a main window and a settings window.";
        if (entry.HasMainUi)
            return "Has a main window only.";
        if (entry.HasConfigUi)
            return "Has a settings window only.";

        return "Registers no window with Dalamud, so the menu has nothing to open.";
    }

    private bool DrawOtherTab()
    {
        var dirty = false;

        if (ImGui.Button("Clear the icon cache"))
            this.icons.ClearCache();

        HelpMarker("Deletes the downloaded icons so they are fetched again. Useful if a plugin changed its icon or one failed to download.");

        ImGui.Separator();

        // Ctrl guard, because there is no undo for this.
        var ctrl = ImGui.GetIO().KeyCtrl;
        if (!ctrl)
            ImGui.BeginDisabled();

        if (ImGui.Button("Reset all settings to defaults"))
        {
            this.config.ResetToDefaults();
            dirty = true;
        }

        if (!ctrl)
            ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.TextDisabled("(hold Ctrl)");

        ImGui.Separator();

        TextHint("Found a bug, or have an idea? Open an issue:");

        if (ImGui.Button("Open the issue tracker"))
            Dalamud.Utility.Util.OpenLink(IssueUrl);

        ImGui.SameLine();
        if (ImGui.Button("Copy link"))
            ImGui.SetClipboardText(IssueUrl);

        TextHint(IssueUrl);

        return dirty;
    }

    private static void HelpMarker(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 24f);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    // ImGui.TextDisabled does not wrap.
    private static void TextHint(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }
}
