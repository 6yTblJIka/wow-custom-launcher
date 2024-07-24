using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Microsoft.Web.WebView2.Core;
using WoWLauncher.Patcher;
using WoWLauncher.Updater;

namespace WoWLauncher;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // update controllers
    private readonly PatchController m_Patcher;
    private readonly UpdateController m_Updater;
    private ServerCheck m_ServerCheck;
    public MainWindow()
    {
        InitializeComponent(); 
        StartWebView();
        
        
        PlayBtn.IsEnabled = false; // Disable during checks

        m_Updater = new UpdateController(this);
        m_Patcher = new PatchController(this);
        m_ServerCheck = new ServerCheck(this, ref m_Updater);

        // Check launcher update
        m_Updater.CheckForUpdates();
        // Update server address
        m_Updater.RetrieveRealmIP();
        

#if !DEBUG
        // While debugging, we don't need to check this...
        // But end-users should have the launcher next to this.
        if (!File.Exists("Wow.exe"))
            if (MessageBox.Show(this,
                    "Cannot locate Wow.exe, please make sure it is next to the launcher's executable.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error) == MessageBoxResult.OK)
                Application.Current.Shutdown();

        // While debugging, we don't necessarily need this folder.
        if (!Directory.Exists("Data"))
            Directory.CreateDirectory("Data");
#endif
        var processName = "WoWLauncher";
        var currentProcessId = Process.GetCurrentProcess().Id;

        var processes = Process.GetProcessesByName(processName);

        if (processes.Length > 0)
            foreach (var process in processes)
                if (process.Id != currentProcessId)
                    process.Kill();
        // Begin checking for game updates
        m_Patcher.CheckPatch();
    }
    private bool IsWebView2RuntimeInstalled()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrEmpty(version);
        }
        catch
        {
            return false;
        }
    }

    private async Task StartWebView()
    {
        if (Directory.Exists("WoWLauncher.exe.WebView2"))
        {
            Directory.Delete("WoWLauncher.exe.WebView2", true);
            await Task.Delay(1000);
        }
        await Task.Delay(500);
        InitializeWebView2();
        
    }
    private async void InitializeWebView2()
    {
        await MyWebView2.EnsureCoreWebView2Async(null);
        // Set WebView2 initial URL or content
        MyWebView2.Source = new Uri("http://madclownworld.com/Patch/InfoText.html");
    }

    private void MyWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // Check if the navigation is a new URL
        if (e.Uri != null && !e.Uri.Equals(MyWebView2.Source.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
        {
            // Open the URL in the default browser
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri,
                UseShellExecute = true
            });

            // Cancel the navigation in WebView2
            e.Cancel = true;
        }
        
        ErrorMessage.Visibility = Visibility.Collapsed;
    }
    private void ShowErrorMessage()
    {
        MyWebView2.Visibility = Visibility.Collapsed;
        ErrorMessage.Visibility = Visibility.Visible;
    }
    
    // Drag window around holding mouse button
    private void Window_MouseDown(object? sender, MouseButtonEventArgs? e)
    {
        if (e?.ChangedButton == MouseButton.Left)
            DragMove();
    }

    // Quit application
    private void Close_Click(object? sender, RoutedEventArgs? e)
    {
        // Check if patch in progress and ask to continue
        if (!m_Patcher.IsPatching || (m_Patcher.IsPatching && MessageBox.Show(this,
                    "Patch in progress. Are you sure you want to quit?", "Patching", MessageBoxButton.YesNo) ==
                MessageBoxResult.Yes))
            Application.Current.Shutdown();
    }

    // Allow URL clicking inside the rich text block
    private void Hyperlink_RequestNavigate(object? sender, RequestNavigateEventArgs? e)
    {
        Process.Start(new ProcessStartInfo(e?.Uri.ToString() ?? "")
        {
            UseShellExecute = true
        });
    }

    // Launch actual game when play button becomes active
    private void playBtn_Click(object? sender, RoutedEventArgs? e)
    {
        // Check if the program exists, first
        if (File.Exists("Wow.exe") && !m_Patcher.IsPatching)
        {
            Process.Start(new ProcessStartInfo("Wow.exe")
            {
                UseShellExecute = true
            });
            Application.Current.Shutdown(); // close launcher, maybe make this an option?
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        
        using (var customMessageBox = new SettingsWindow())
        {
            customMessageBox.Owner = this;
            customMessageBox.WindowStartupLocation = WindowStartupLocation.CenterOwner; // Center the window
            var result = customMessageBox.ShowDialog();
            if (result == true)
            {
            }
        }
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        using (var customMessageBox = new ClearCacheWindow())
        {   
            customMessageBox.Owner = this;
            customMessageBox.WindowStartupLocation = WindowStartupLocation.CenterOwner; // Center the window
            var result = customMessageBox.ShowDialog();

            if (result == true)
            {
                PatchController.ClearCache();
                // If the launcher is here (it should be), launch it again
                if (File.Exists("WoWLauncher.exe"))
                    Process.Start(new ProcessStartInfo("WoWLauncher.exe")
                    {
                        UseShellExecute = true
                    });
            }
        }
    }
}