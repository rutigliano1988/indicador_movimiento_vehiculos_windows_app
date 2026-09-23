using IndicadoresMovimiento.App.Services;
using IndicadoresMovimiento.Core;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace IndicadoresMovimiento.App;

/// <summary>Icono junto al reloj de Windows con el menú de la aplicación.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly AppController _controller;
    private readonly Forms.NotifyIcon _icon;
    private readonly Drawing.Icon _activeIcon;
    private readonly Drawing.Icon _pausedIcon;
    private readonly Forms.ToolStripMenuItem _enabledItem;
    private readonly Forms.ToolStripMenuItem _modeAutomatic;
    private readonly Forms.ToolStripMenuItem _modeAlways;
    private readonly Dictionary<MotionSourceKind, Forms.ToolStripMenuItem> _sourceItems = [];

    public TrayIcon(AppController controller)
    {
        _controller = controller;
        _activeIcon = LoadIcon("app.ico");
        _pausedIcon = LoadIcon("app-pausa.ico");

        _enabledItem = new Forms.ToolStripMenuItem("Mostrar los indicadores", null, (_, _) => controller.ToggleEnabled())
        {
            ShortcutKeyDisplayString = GlobalHotKey.Description,
        };

        _modeAutomatic = new Forms.ToolStripMenuItem("Automático (al detectar movimiento)", null,
            (_, _) => controller.UpdateSettings(s => s.Mode = DisplayMode.Automatic));
        _modeAlways = new Forms.ToolStripMenuItem("Siempre", null,
            (_, _) => controller.UpdateSettings(s => s.Mode = DisplayMode.Always));
        var modeMenu = new Forms.ToolStripMenuItem("Cuándo mostrarlos");
        modeMenu.DropDownItems.AddRange([_modeAutomatic, _modeAlways]);

        var sourceMenu = new Forms.ToolStripMenuItem("Fuente del movimiento");
        foreach (var (kind, text) in new[]
                 {
                     (MotionSourceKind.Automatic, "Automática"),
                     (MotionSourceKind.BuiltInSensor, "Sensor del ordenador"),
                     (MotionSourceKind.Phone, "Móvil"),
                     (MotionSourceKind.Demo, "Demostración"),
                 })
        {
            var item = new Forms.ToolStripMenuItem(text, null, (_, _) => controller.UpdateSettings(s => s.Source = kind));
            _sourceItems[kind] = item;
            sourceMenu.DropDownItems.Add(item);
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(modeMenu);
        menu.Items.Add(sourceMenu);
        menu.Items.Add(new Forms.ToolStripMenuItem("Usar el móvil como sensor…", null, (_, _) => controller.ShowPhoneWindow()));
        menu.Items.Add(new Forms.ToolStripMenuItem("Ajustes…", null, (_, _) => controller.ShowSettings())
        {
            Font = new Drawing.Font(menu.Font, Drawing.FontStyle.Bold),
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Salir", null, (_, _) => controller.Exit()));
        menu.Opening += (_, _) => Refresh();

        _icon = new Forms.NotifyIcon
        {
            Icon = _activeIcon,
            Text = "Indicadores de movimiento",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) controller.ShowSettings();
        };
        _icon.BalloonTipClicked += (_, _) => controller.ShowSettings();
    }

    public void Refresh()
    {
        var settings = _controller.Settings;
        _enabledItem.Checked = settings.Enabled;
        _modeAutomatic.Checked = settings.Mode == DisplayMode.Automatic;
        _modeAlways.Checked = settings.Mode == DisplayMode.Always;
        foreach (var (kind, item) in _sourceItems) item.Checked = settings.Source == kind;

        _icon.Icon = settings.Enabled ? _activeIcon : _pausedIcon;
        var state = settings.Enabled ? $"activos · {_controller.Sources.Active.DisplayName.ToLowerInvariant()}" : "en pausa";
        var text = $"Indicadores de movimiento: {state}";
        _icon.Text = text.Length > 127 ? text[..127] : text;
    }

    public void Notify(string title, string text) =>
        _icon.ShowBalloonTip(6000, title, text, Forms.ToolTipIcon.Info);

    private static Drawing.Icon LoadIcon(string name)
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream(name);
        return stream is null
            ? (Drawing.Icon)Drawing.SystemIcons.Application.Clone()
            : new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _activeIcon.Dispose();
        _pausedIcon.Dispose();
    }
}
