using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace PluginMenu.Windows;

// The menu drawn in an ordinary window rather than a popup, so it can sit on screen
// while the size settings are being adjusted. A popup cannot do this: clicking into
// the settings window would close it, which is exactly the back and forth this is
// here to avoid.
//
// It draws the real menu, not a mock up, so what you size is what you get.
public sealed class PreviewWindow : Window
{
    private readonly MenuWindow menu;
    private readonly Configuration config;

    public PreviewWindow(MenuWindow menu, Configuration config)
        : base("Plugin Menu - Preview##PluginMenuPreview")
    {
        this.menu = menu;
        this.config = config;

        this.RespectCloseHotkey = false;
        this.DisableWindowSounds = true;
        this.Flags = ImGuiWindowFlags.AlwaysAutoResize
                     | ImGuiWindowFlags.NoCollapse
                     | ImGuiWindowFlags.NoDocking
                     | ImGuiWindowFlags.NoFocusOnAppearing;
    }

    public override void Draw()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextUnformatted("Live preview. Resizes as you change the settings.");
        ImGui.PopStyleColor();
        ImGui.Separator();

        var scale = Math.Clamp(this.config.Scale, 0.5f, 3f);
        this.menu.DrawContents(scale);
    }
}
