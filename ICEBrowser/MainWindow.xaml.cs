using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ICEBrowser;

public partial class MainWindow : Window
{
    private sealed class BrowserTab
    {
        public required string Id { get; init; }
        public required string Title { get; set; }
        public required WebView2 View { get; init; }
        public required Button TabButton { get; set; }
    }

    private readonly List<BrowserTab> _tabs = new();
    private BrowserTab? _active;
    private readonly string _profileRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ICEBrowser", "Profiles");
    private const string HomeUrl = "https://www.google.com";
    private bool _vpnBusy;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await NewTabAsync(HomeUrl);
            await RefreshVpnStatusAsync();
        };
    }

    private async Task NewTabAsync(string url)
    {
        var view = new WebView2();
        var profile = Path.Combine(_profileRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        BrowserHost.Children.Add(view);
        view.Visibility = Visibility.Collapsed;
        await view.EnsureCoreWebView2Async();
        view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        view.CoreWebView2.Settings.IsStatusBarEnabled = false;
        view.CoreWebView2.Settings.AreDevToolsEnabled = false;
        view.CoreWebView2.Settings.IsZoomControlEnabled = true;
        view.CoreWebView2.NavigationStarting += (_, _) => StatusText.Text = "Loading…";
        view.CoreWebView2.NavigationCompleted += (_, _) =>
        {
            StatusText.Text = "Ready";
            if (_active?.View == view) AddressBox.Text = view.Source?.ToString() ?? "";
        };
        view.CoreWebView2.DocumentTitleChanged += (_, _) =>
        {
            var tab = _tabs.FirstOrDefault(t => t.View == view);
            if (tab != null)
            {
                tab.Title = string.IsNullOrWhiteSpace(view.CoreWebView2.DocumentTitle)
                    ? "New Tab" : view.CoreWebView2.DocumentTitle;
                UpdateTabCaption(tab);
            }
        };
        var item = new BrowserTab { Id = Guid.NewGuid().ToString("N"), Title = "New Tab", View = view, TabButton = new Button() };
        item.TabButton.Click += (_, _) => ActivateTab(item);
        _tabs.Add(item);
        UpdateTabCaption(item);
        ActivateTab(item);
        Navigate(url);
    }

    private void ActivateTab(BrowserTab tab)
    {
        _active = tab;
        foreach (var t in _tabs) t.View.Visibility = t == tab ? Visibility.Visible : Visibility.Collapsed;
        AddressBox.Text = tab.View.Source?.ToString() ?? "";
        foreach (var t in _tabs)
        {
            t.TabButton.Background = t == tab ? new SolidColorBrush(Color.FromRgb(31, 65, 99)) : new SolidColorBrush(Color.FromRgb(23, 38, 58));
        }
    }

    private void UpdateTabCaption(BrowserTab tab)
    {
        tab.TabButton.Content = tab.Title.Length > 20 ? tab.Title[..20] + "…" : tab.Title;
        tab.TabButton.ToolTip = tab.Title;
        tab.TabButton.Padding = new Thickness(13, 6, 13, 6);
        if (!TabsPanel.Children.Contains(tab.TabButton)) TabsPanel.Children.Add(tab.TabButton);
    }

    private void Navigate(string text)
    {
        if (_active == null) return;
        var value = text.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            value = value.Contains(' ') || !value.Contains('.') 
                ? "https://www.google.com/search?q=" + Uri.EscapeDataString(value)
                : "https://" + value;
        }
        if (Uri.TryCreate(value, UriKind.Absolute, out var target))
        {
            _active.View.Source = target;
            AddressBox.Text = target.ToString();
            StatusText.Text = "Navigating…";
        }
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await NewTabAsync(HomeUrl);
    private void Back_Click(object sender, RoutedEventArgs e) { if (_active?.View.CanGoBack == true) _active.View.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (_active?.View.CanGoForward == true) _active.View.GoForward(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => _active?.View.Reload();
    private void Home_Click(object sender, RoutedEventArgs e) => Navigate(HomeUrl);
    private void AddressBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Navigate(AddressBox.Text); }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_active?.View.Source == null) return;
        var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ICEBrowser", "bookmarks.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.AppendAllText(file, _active.View.Source + Environment.NewLine);
        StatusText.Text = "Bookmark saved";
    }

    private async void VpnButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vpnBusy) return;
        _vpnBusy = true;
        VpnButton.IsEnabled = false;
        try
        {
            var status = await RunWarpAsync("status");
            bool connected = status.ExitCode == 0 &&
                status.Output.Contains("Connected", StringComparison.OrdinalIgnoreCase);
            if (connected)
            {
                var result = await RunWarpAsync("disconnect");
                StatusText.Text = result.ExitCode == 0 ? "VPN disconnected" : "Unable to disconnect VPN";
            }
            else
            {
                var result = await RunWarpAsync("connect");
                StatusText.Text = result.ExitCode == 0 ? "VPN connection requested" : "WARP command failed";
            }
            await Task.Delay(1200);
            await RefreshVpnStatusAsync();
        }
        catch (Exception ex) { StatusText.Text = "VPN error: " + ex.Message; }
        finally { _vpnBusy = false; VpnButton.IsEnabled = true; }
    }

    private async Task RefreshVpnStatusAsync()
    {
        var result = await RunWarpAsync("status");
        bool connected = result.ExitCode == 0 &&
            result.Output.Contains("Connected", StringComparison.OrdinalIgnoreCase);
        VpnDot.Fill = connected ? new SolidColorBrush(Color.FromRgb(49, 190, 120)) : new SolidColorBrush(Color.FromRgb(217, 154, 54));
        VpnLabel.Text = connected ? "VPN CONNECTED" : "VPN DISCONNECTED";
        VpnButton.Content = connected ? "DISCONNECT" : "CONNECT VPN";
    }

    private static async Task<(int ExitCode, string Output)> RunWarpAsync(string args)
    {
        string? exe = FindWarpCli();
        if (exe == null) return (-1, "Cloudflare WARP CLI not found. Install Cloudflare WARP first.");
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi);
        if (process == null) return (-1, "Could not start warp-cli.");
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout + Environment.NewLine + stderr);
    }

    private static string? FindWarpCli()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cloudflare", "Cloudflare WARP", "warp-cli.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cloudflare", "WARP", "warp-cli.exe")
        };
        foreach (var path in candidates) if (File.Exists(path)) return path;
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, "warp-cli.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
