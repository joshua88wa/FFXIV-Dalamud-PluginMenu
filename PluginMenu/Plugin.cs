using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using PluginMenu.Windows;

namespace PluginMenu;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/pmenu";

    private readonly WindowSystem windowSystem = new("PluginMenu");
    private readonly Configuration config;
    private readonly PluginCatalog catalog;
    private readonly IconCache icons;
    private readonly MenuWindow menuWindow;
    private readonly ConfigWindow configWindow;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        this.config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.config.Initialize(pluginInterface);

        this.catalog = new PluginCatalog(this.config);
        this.icons = new IconCache();

        this.menuWindow = new MenuWindow(this.config, this.catalog, this.icons) { IsOpen = true };
        this.configWindow = new ConfigWindow(this.config, this.catalog, this.icons);
        this.menuWindow.OpenConfig = () => this.configWindow.IsOpen = true;

        this.windowSystem.AddWindow(this.menuWindow);
        this.windowSystem.AddWindow(this.configWindow);

        Service.Commands.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open the plugin menu. /pmenu config opens settings, /pmenu button toggles the floating button.",
        });

        pluginInterface.UiBuilder.Draw += this.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += this.OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi += this.OpenMenu;
    }

    public void Dispose()
    {
        Service.Interface.UiBuilder.Draw -= this.Draw;
        Service.Interface.UiBuilder.OpenConfigUi -= this.OpenConfig;
        Service.Interface.UiBuilder.OpenMainUi -= this.OpenMenu;

        Service.Commands.RemoveHandler(CommandName);

        this.windowSystem.RemoveAllWindows();
        this.icons.Dispose();
    }

    // The popup lives inside the menu window, so that window stays open even when the
    // button is turned off. With no button it draws a single transparent pixel that
    // does not take input, which is cheaper than tearing the window down and back up.
    private void Draw() => this.windowSystem.Draw();

    private void OpenConfig() => this.configWindow.IsOpen = true;

    private void OpenMenu() => this.menuWindow.RequestOpen();

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "config":
            case "settings":
                this.OpenConfig();
                break;
            case "button":
                this.config.ShowButton = !this.config.ShowButton;
                this.config.Save();
                break;
            default:
                this.OpenMenu();
                break;
        }
    }
}
