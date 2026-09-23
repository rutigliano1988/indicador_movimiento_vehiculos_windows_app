using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using IndicadoresMovimiento.App.Services;
using IndicadoresMovimiento.Movil;

namespace IndicadoresMovimiento.App.Views;

public partial class PhoneWindow : Window
{
    private readonly AppController _controller;
    private readonly DispatcherTimer _timer;
    private string? _addressesKey;
    private string? _shownUrl;
    private bool _loading;

    internal PhoneWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
        Closed += (_, _) => _timer.Stop();
    }

    private PhoneMotionSource Phone => _controller.Sources.Phone;

    private void Refresh()
    {
        RefreshAddresses();
        RefreshStatus();
    }

    /// <summary>Las redes pueden cambiar con la ventana abierta (p. ej. al conectarse al punto de acceso).</summary>
    private void RefreshAddresses()
    {
        var urls = Phone.GetConnectionUrls();
        var key = Phone.IsRunning + "|" + string.Join("|", urls.Select(u => u.Url));
        if (key == _addressesKey) return;
        if (_addressesKey is not null) _ = Phone.RefreshCertificateIfNeededAsync();
        _addressesKey = key;

        _loading = true;
        try
        {
            var previous = (AddressBox.SelectedItem as ComboBoxItem)?.Tag as string;
            AddressBox.Items.Clear();
            foreach (var (address, url) in urls)
            {
                var label = address.IsPhoneHotspot
                    ? $"{address.Address} (punto de acceso del iPhone)"
                    : $"{address.Address} ({address.InterfaceName})";
                AddressBox.Items.Add(new ComboBoxItem { Content = label, Tag = url });
            }

            AddressBox.SelectedItem =
                AddressBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == previous)
                ?? AddressBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
        }
        finally
        {
            _loading = false;
        }
        ShowSelectedUrl();
    }

    private void ShowSelectedUrl()
    {
        var url = (AddressBox.SelectedItem as ComboBoxItem)?.Tag as string;
        AddressBox.IsEnabled = url is not null;
        CopyButton.IsEnabled = url is not null;
        if (url == _shownUrl && url is not null) return;
        _shownUrl = url;

        if (url is null)
        {
            QrImage.Source = null;
            UrlBox.Text = "";
            QrPlaceholder.Text = Phone.LastError is { } error
                ? $"No se pudo preparar la conexión: {error}"
                : Phone.IsRunning
                    ? "No se encuentra ninguna red. Conecta el ordenador a una red Wi-Fi (por ejemplo, al punto de acceso del móvil)."
                    : "Preparando la conexión…";
            return;
        }

        QrPlaceholder.Text = "";
        UrlBox.Text = url;
        QrImage.Source = QrCodeImage.Create(url);
    }

    private void RefreshStatus()
    {
        var since = Phone.SecondsSinceLastBatch;
        string text;
        string brush;
        if (Phone.LastError is not null)
        {
            text = Phone.StatusText;
            brush = "ErrorBrush";
        }
        else if (!Phone.IsRunning)
        {
            text = "Preparando la conexión…";
            brush = "IdleBrush";
        }
        else if (since < 1.5)
        {
            text = "¡Conectado! " + Phone.StatusText + " Ya puedes cerrar esta ventana.";
            brush = "OkBrush";
        }
        else if (double.IsFinite(since))
        {
            text = Phone.StatusText;
            brush = "WarnBrush";
        }
        else
        {
            text = "Esperando a que el móvil se conecte…";
            brush = "IdleBrush";
        }

        StatusText.Text = text;
        StatusLight.Fill = (Brush)FindResource(brush);
    }

    private void OnAddressChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) ShowSelectedUrl();
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(UrlBox.Text)) return;
        try
        {
            Clipboard.SetText(UrlBox.Text);
            CopyButton.Content = "¡Copiado!";
        }
        catch (COMException)
        {
            // Otra aplicación tiene el portapapeles ocupado; se puede volver a intentar.
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
