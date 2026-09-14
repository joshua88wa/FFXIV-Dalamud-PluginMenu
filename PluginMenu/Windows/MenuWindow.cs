using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace PluginMenu.Windows;

public sealed class MenuWindow : Window
{
    private const string PopupId = "##pm_popup";

    private readonly Configuration config;
    private readonly PluginCatalog catalog;
    private readonly IconCache icons;

    private string filter = string.Empty;
    private bool openRequested;
    private bool focusSearch;

    // The rectangle the button occupied last frame, used to decide click-through.
    private Vector2 buttonMin;
    private Vector2 buttonMax;

    public MenuWindow(Configuration config, PluginCatalog catalog, IconCache icons)
        : base("Plugin Menu##PluginMenuButton")
    {
        this.config = config;
        this.catalog = catalog;
        this.icons = icons;

        this.RespectCloseHotkey = false;
        this.DisableWindowSounds = true;
    }

    // Set by the plugin so the menu can reach its own settings.
    public Action? OpenConfig { get; set; }

    // Used by /pmenu, so the menu can be opened without the button on screen.
    public void RequestOpen()
    {
        this.openRequested = true;
        this.IsOpen = true;
    }

    public override void PreDraw()
    {
        this.Flags = ImGuiWindowFlags.NoTitleBar
                     | ImGuiWindowFlags.NoScrollbar
                     | ImGuiWindowFlags.NoScrollWithMouse
                     | ImGuiWindowFlags.AlwaysAutoResize
                     | ImGuiWindowFlags.NoCollapse
                     | ImGuiWindowFlags.NoDocking
                     | ImGuiWindowFlags.NoFocusOnAppearing;

        // Locked means the window never needs to be grabbed, so it can lose its frame
        // and stop taking clicks as well. ImGui hit testing is rectangular, so
        // click-through is done by dropping input for the whole window on frames where
        // the cursor is not over the button itself.
        if (this.config.LockPosition)
        {
            this.Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground;

            if (!this.CursorOverButton())
                this.Flags |= ImGuiWindowFlags.NoInputs;
        }

        // With the button off there is nothing to see or click, but the window has to
        // stay alive because the popup belongs to it.
        if (!this.config.ShowButton)
            this.Flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoMove;
    }

    // Measured from the previous frame, because the flags for this frame have to be
    // set before anything is drawn.
    private bool CursorOverButton()
    {
        // Nothing measured yet, so stay clickable rather than starting out dead.
        if (this.buttonMax.X <= this.buttonMin.X || this.buttonMax.Y <= this.buttonMin.Y)
            return true;

        // While the menu is up the window stays live, so the popup keeps behaving
        // normally no matter where the cursor wanders.
        if (ImGui.IsPopupOpen(PopupId))
            return true;

        var mouse = ImGui.GetIO().MousePos;

        return mouse.X >= this.buttonMin.X && mouse.X <= this.buttonMax.X
               && mouse.Y >= this.buttonMin.Y && mouse.Y <= this.buttonMax.Y;
    }

    public override void Draw()
    {
        var scale = Math.Clamp(this.config.Scale, 0.5f, 3f);
        ImGui.SetWindowFontScale(scale);

        var label = string.IsNullOrWhiteSpace(this.config.ButtonLabel)
            ? "Dalamud Plugins"
            : this.config.ButtonLabel;

        if (!this.config.ShowButton)
        {
            ImGui.Dummy(new Vector2(1, 1));
        }
        // While Shift is held the button becomes a drawing rather than an item, so the
        // drag reaches the window underneath it and the whole thing can be moved. There
        // is nothing to drag when the position is locked, so the button stays a button.
        else if (ImGui.GetIO().KeyShift && !this.config.LockPosition)
        {
            DrawInertButton(label);
        }
        else if (ImGui.Button(label))
        {
            this.openRequested = true;
        }

        this.buttonMin = ImGui.GetItemRectMin();
        this.buttonMax = ImGui.GetItemRectMax();

        if (this.config.ShowButton && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Installed plugins");
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.TextUnformatted(this.config.LockPosition
                ? "Position is locked. /pmenu config opens settings."
                : "Shift and drag moves this button. /pmenu config opens settings.");
            ImGui.PopStyleColor();
            ImGui.EndTooltip();
        }

        if (this.openRequested)
        {
            this.openRequested = false;
            this.filter = string.Empty;
            this.focusSearch = true;
            this.catalog.Refresh(true);
            ImGui.OpenPopup(PopupId);
        }

        this.DrawPopup(scale);

        ImGui.SetWindowFontScale(1f);
    }

    // The label drawn as a frame and some text, with no item behind it.
    private static void DrawInertButton(string label)
    {
        var padding = ImGui.GetStyle().FramePadding;
        var size = ImGui.CalcTextSize(label) + (padding * 2f);

        ImGui.Dummy(size);

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var draw = ImGui.GetWindowDrawList();

        draw.AddRectFilled(min, max, ImGui.GetColorU32(ImGuiCol.Button), ImGui.GetStyle().FrameRounding);
        draw.AddText(min + padding, ImGui.GetColorU32(ImGuiCol.Text), label);
    }

    private void DrawPopup(float scale)
    {
        if (!ImGui.BeginPopup(PopupId))
            return;

        var width = this.config.MenuWidth * scale;
        var height = this.config.MenuHeight * scale;

        if (this.config.ShowSearch)
        {
            if (this.focusSearch)
            {
                ImGui.SetKeyboardFocusHere();
                this.focusSearch = false;
            }

            ImGui.SetNextItemWidth(width);
            ImGui.InputTextWithHint("##pm_search", "Search", ref this.filter, 128);
            ImGui.Separator();
        }

        var rows = this.catalog.Visible(this.filter).ToList();

        if (rows.Count == 0)
        {
            ImGui.TextDisabled(this.catalog.Entries.Count == 0
                ? "No plugins found."
                : "Nothing matches.");
        }
        else
        {
            ImGui.BeginChild("##pm_list", new Vector2(width, Math.Min(height, (rows.Count * ImGui.GetFrameHeightWithSpacing()) + 8f)));
            this.DrawRows(rows, scale);
            ImGui.EndChild();
        }

        ImGui.Separator();

        if (ImGui.SmallButton("Settings"))
        {
            this.OpenConfig?.Invoke();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"{rows.Count} shown");

        ImGui.EndPopup();
    }

    private void DrawRows(List<PluginEntry> rows, float scale)
    {
        var iconSize = ImGui.GetFrameHeight();

        if (!ImGui.BeginTable("##pm_table", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
            return;

        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, iconSize);
        ImGui.TableSetupColumn("##name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##buttons", ImGuiTableColumnFlags.WidthFixed, ButtonColumnWidth(scale));

        foreach (var entry in rows)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            this.DrawIcon(entry, iconSize);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();

            var name = this.config.CleanNames ? entry.DisplayName : entry.RawName;
            if (ImGui.Selectable($"{name}##row_{entry.InternalName}", false))
            {
                this.catalog.OpenPrimary(entry);
                if (this.config.CloseAfterClick)
                    ImGui.CloseCurrentPopup();
            }

            this.DrawRowContextMenu(entry);

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(entry.RawName);
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                ImGui.TextUnformatted($"{entry.InternalName}  {entry.Version}");
                if (entry.IsDev)
                    ImGui.TextUnformatted("Dev plugin");
                else if (entry.IsTesting)
                    ImGui.TextUnformatted("Testing build");
                if (!entry.Launchable)
                    ImGui.TextUnformatted("Registers no window to open.");
                ImGui.TextUnformatted("Right click for more.");
                ImGui.PopStyleColor();
                ImGui.EndTooltip();
            }

            ImGui.TableNextColumn();
            this.DrawRowButtons(entry);
        }

        ImGui.EndTable();
    }

    // The button is sized to the row rather than to the glyph, so it can never be the
    // thing that makes a row taller. Cell padding on both sides, plus a little to keep
    // it off the scrollbar.
    private static float ButtonColumnWidth(float scale)
        => ImGui.GetFrameHeight() + (ImGui.GetStyle().CellPadding.X * 2f) + (4f * scale);

    private void DrawRowContextMenu(PluginEntry entry)
    {
        if (!ImGui.BeginPopupContextItem($"##ctx_{entry.InternalName}"))
            return;

        ImGui.TextDisabled(entry.RawName);
        ImGui.Separator();

        if (entry.HasMainUi && ImGui.MenuItem("Open"))
        {
            this.catalog.OpenMain(entry);
            if (this.config.CloseAfterClick)
                ImGui.CloseCurrentPopup();
        }

        if (entry.HasConfigUi && ImGui.MenuItem("Settings"))
        {
            this.catalog.OpenSettings(entry);
            if (this.config.CloseAfterClick)
                ImGui.CloseCurrentPopup();
        }

        if (ImGui.MenuItem("Hide from this list"))
        {
            this.config.Hidden.Add(entry.InternalName);
            this.config.Save();
        }

        ImGui.EndPopup();
    }

    // Clicking the row already does the primary action, so the only button worth a
    // column is the other one, and only when the plugin actually has both. A row that
    // can do one thing does not need a button offering that same thing.
    private void DrawRowButtons(PluginEntry entry)
    {
        if (!entry.HasMainUi || !entry.HasConfigUi)
            return;

        var secondaryIsSettings = this.config.RowClick == PrimaryAction.Open;
        var icon = secondaryIsSettings ? FontAwesomeIcon.Cog : FontAwesomeIcon.ExternalLinkAlt;

        // An empty button of a known size, with the glyph drawn into it afterwards.
        // Letting the icon font size the button is what clipped it: those glyphs are
        // taller than the text font at the same nominal size, so the button came out
        // taller than the row and the table cut the top and bottom off.
        var size = ImGui.GetFrameHeight();
        var clicked = ImGui.Button($"##alt_{entry.InternalName}", new Vector2(size, size));

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var hovered = ImGui.IsItemHovered();

        DrawIconGlyph(icon, min, max, ImGui.GetColorU32(hovered ? ImGuiCol.Text : ImGuiCol.TextDisabled));

        if (hovered)
            ImGui.SetTooltip(secondaryIsSettings ? "Settings" : "Open");

        if (!clicked)
            return;

        if (secondaryIsSettings)
            this.catalog.OpenSettings(entry);
        else
            this.catalog.OpenMain(entry);

        if (this.config.CloseAfterClick)
            ImGui.CloseCurrentPopup();
    }

    // Drawn at an explicit font size and centred on the glyph's own ink, not on the box
    // CalcTextSize reports. That box is the advance width and the full line height, and
    // an icon glyph does not sit in the middle of it, which is what left the cog looking
    // slightly off centre inside its button.
    private static unsafe void DrawIconGlyph(FontAwesomeIcon icon, Vector2 min, Vector2 max, uint colour)
    {
        var draw = ImGui.GetWindowDrawList();
        var text = icon.ToIconString();
        var box = max - min;
        var fontSize = Math.Min(box.X, box.Y) * 0.62f;
        var centre = (min + max) * 0.5f;

        using var pushed = Service.Interface.UiBuilder.IconFontHandle.Push();

        var font = ImGui.GetFont();
        var position = centre - (ImGui.CalcTextSize(text) * (fontSize / ImGui.GetFontSize()) * 0.5f);

        // Glyph metrics are stored at the font's own size, so they scale with the size
        // actually being drawn at. If the glyph is missing, the measured box above is
        // still a reasonable place to put it.
        var glyph = font.FindGlyph((char)icon);
        if (glyph != null)
        {
            var ratio = fontSize / font.FontSize;
            var inkMin = new Vector2(glyph->X0, glyph->Y0) * ratio;
            var inkMax = new Vector2(glyph->X1, glyph->Y1) * ratio;

            position = centre - ((inkMax - inkMin) * 0.5f) - inkMin;
        }

        draw.AddText(font, fontSize, position, colour, text);
    }

    // Space is reserved with a Dummy and the art is drawn into it, so a missing icon
    // costs nothing in layout and the monogram lines up with the real ones.
    private void DrawIcon(PluginEntry entry, float size)
    {
        ImGui.Dummy(new Vector2(size, size));

        if (!this.config.ShowIcons)
            return;

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var draw = ImGui.GetWindowDrawList();

        var texture = this.icons.TryGet(entry);
        if (texture != null)
        {
            draw.AddImage(texture.Handle, min, max, Vector2.Zero, Vector2.One);
            return;
        }

        DrawMonogram(draw, entry, min, max, size);
    }

    // Shown until the icon arrives, and permanently for plugins that have no icon URL.
    private static void DrawMonogram(ImDrawListPtr draw, PluginEntry entry, Vector2 min, Vector2 max, float size)
    {
        var hash = 0;
        foreach (var c in entry.InternalName)
            hash = (hash * 31) + c;

        var hue = Math.Abs(hash % 360) / 360f;
        var colour = HsvToRgb(hue, 0.45f, 0.55f);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(colour), size * 0.2f);

        var initial = entry.DisplayName.FirstOrDefault(char.IsLetterOrDigit);
        if (initial == '\0')
            return;

        var letter = char.ToUpperInvariant(initial).ToString();
        var textSize = ImGui.CalcTextSize(letter);
        var centre = (min + max) * 0.5f;
        draw.AddText(centre - (textSize * 0.5f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), letter);
    }

    // Hand rolled rather than via ImGui's converter, so the monogram colours do not
    // depend on which helpers the bindings happen to expose.
    private static Vector4 HsvToRgb(float h, float s, float v)
    {
        var i = (int)Math.Floor(h * 6f);
        var f = (h * 6f) - i;
        var p = v * (1f - s);
        var q = v * (1f - (f * s));
        var t = v * (1f - ((1f - f) * s));

        return (i % 6) switch
        {
            0 => new Vector4(v, t, p, 1f),
            1 => new Vector4(q, v, p, 1f),
            2 => new Vector4(p, v, t, 1f),
            3 => new Vector4(p, q, v, 1f),
            4 => new Vector4(t, p, v, 1f),
            _ => new Vector4(v, p, q, 1f),
        };
    }
}
