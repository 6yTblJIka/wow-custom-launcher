using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
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
        playBtn.IsEnabled = false; // Disable during checks

        m_Updater = new UpdateController(this);
        m_Patcher = new PatchController(this);
        m_ServerCheck = new ServerCheck(this, ref m_Updater);

        // Check launcher update
        m_Updater.CheckForUpdates();
        // Update server address
        m_Updater.RetrieveRealmIP();
        if (File.Exists("Data/enUS/realmlist.wtf"))
        {
            // Read existing file and save it for this session
            var _realmd = File.ReadAllText("Data/enUS/realmlist.wtf");
            if (_realmd.Length > 0)
            {
                var _realmParts = _realmd.Split(' ');
                textInputBox_Realm.Text = _realmParts[2];
            }
        }

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
        // Begin checking for game updates
        m_Patcher.CheckPatch();
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

    private void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        var userInput = textInputBox_Realm.Text; // Get the text from the TextBox
        textInputBox_Realm.Text = "" + userInput;
        UpdateController.SetRealmList(userInput);
    }
}