using System.Windows.Interop;
using IndicadoresMovimiento.App.Interop;

namespace IndicadoresMovimiento.App.Services;

/// <summary>Atajo de teclado global (Ctrl+Alt+M) para mostrar u ocultar los puntos.</summary>
internal sealed class GlobalHotKey : IDisposable
{
    public const string Description = "Ctrl+Alt+M";
    private const int Id = 0x4D4F; // "MO"
    private const uint VirtualKeyM = 0x4D;

    private readonly HwndSource _source;
    private readonly Action _onPressed;

    public GlobalHotKey(Action onPressed)
    {
        _onPressed = onPressed;
        // Ventana invisible que solo recibe mensajes.
        _source = new HwndSource(new HwndSourceParameters("IndicadoresMovimientoAtajo")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE,
        });
        _source.AddHook(WndProc);
        IsRegistered = NativeMethods.RegisterHotKey(_source.Handle, Id,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, VirtualKeyM);
    }

    /// <summary>Falso si otra aplicación ya usa el mismo atajo.</summary>
    public bool IsRegistered { get; }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam == Id)
        {
            handled = true;
            _onPressed();
        }
        return 0;
    }

    public void Dispose()
    {
        if (IsRegistered) NativeMethods.UnregisterHotKey(_source.Handle, Id);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
