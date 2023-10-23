using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;

namespace WoWLauncher.Updater;

/// <summary>
///     Responsible for updating the launcher itself
/// </summary>
internal class UpdateController
{
    // Data
    private static string m_RealmAddress;

    private readonly string m_ServerAddressUri = "http://MadClownWorld.com/Patch/realm.txt";

    // Textfile containing version number of latest launcher (e.g 1.2) 
    private readonly string m_UpdateVersionUri = "http://MadClownWorld.com/Patch/update.txt";

    // Reference parent window
    private readonly MainWindow m_WndRef;

    public UpdateController(MainWindow _wndRef)
    {
        m_WndRef = _wndRef;
        NeedsUpdate = false;
    }
    /*
     * HOW TO ORGANIZE YOUR PATCH SERVER
     * 

        patch-folder (e.g www.example.com/Patch/) 
            |
            |- Patch
                |--- plist.txt       <== your list of patch files (each filename on seperate line)
                |--- realm.txt       <== contains the IP address of your game server
                |--- update.txt      <== version number of latest launcher
                |--- client.zip      <== latest launcher files as zip

                |--- Patch-4.MPQ     <== list of patch files, can be any name (for WoW must start with "Patch-"
                |--- Patch-C.MPQ    
                |--- ... etc

     *
     *
     */

    // Accessor
    public bool NeedsUpdate { get; private set; }

    public string RealmAddress => m_RealmAddress;

    /// <summary>
    ///     Begin checking for launcher updates.
    /// </summary>
    public void CheckForUpdates()
    {
        // Check if update file exists
        var url = m_UpdateVersionUri;
        var request = WebRequest.Create(url);
        try
        {
            var response = (HttpWebResponse)request.GetResponse();
        }
        catch
        {
            // Reset and continue business as usual
            NeedsUpdate = false;
            return;
        }

        // Begin downloading update info
        using (WebClient wc = new())
        {
            wc.DownloadStringAsync(new Uri(m_UpdateVersionUri), "Cache/L/version.txt");
            wc.DownloadStringCompleted += update_DoneRetrieveAsync;
        }
    }


    /// <summary>
    ///     Retrieve latest game server address.
    /// </summary>
    public void RetrieveRealmIP()
    {
        // Set default and prepare folders
        m_RealmAddress = "MadClownWorld.com";
        if (!Directory.Exists("Data/enUS"))
            Directory.CreateDirectory("Data/enUS");

        var url = m_ServerAddressUri;
        var request = WebRequest.Create(url);
        try
        {
            var response = (HttpWebResponse)request.GetResponse();
        }
        catch
        {
            // No server file online, check if local file exists
            if (File.Exists("Data/enUS/realmlist.wtf"))
            {
                // Read existing file and save it for this session
                var _realmd = File.ReadAllText("Data/enUS/realmlist.wtf");
                if (_realmd.Length > 0)
                {
                    var _realmParts = _realmd.Split(' ');
                    m_RealmAddress = _realmParts[2];
                    m_WndRef.TextInputBoxRealm.Text = m_RealmAddress;
                }
            }
            else // create new dummy file if nothing else exists. Silly.
            {
                File.WriteAllText("Data/enUS/realmlist.wtf", $"set realmlist {m_RealmAddress}");
                m_WndRef.TextInputBoxRealm.Text = m_RealmAddress;
            }

            return;
        }

        // Update texts
        m_WndRef.ProgressInfo.Visibility = Visibility.Visible;
        m_WndRef.ProgressInfo.Content = "Updating server IP...";

        // Prepare folders
        if (!Directory.Exists("Cache/L"))
            Directory.CreateDirectory("Cache/L");
        if (File.Exists("Cache/L/realm.txt"))
            File.Delete("Cache/L/realm.txt");

        // Begin downloading server address update
        using (WebClient wc = new())
        {
            wc.DownloadStringAsync(new Uri(m_ServerAddressUri), "Cache/L/realm.txt");
            wc.DownloadStringCompleted += realm_DonePatchListAsync;
        }
    }

    public static void SetRealmList(string _input)
    {
        // Set default and prepare folders
        if (!Directory.Exists("Data/enUS"))
            Directory.CreateDirectory("Data/enUS");
        File.WriteAllText("Data/enUS/realmlist.wtf", $"set realmlist {_input}");
    }

    /// <summary>
    ///     Completed server address update, apply.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void realm_DonePatchListAsync(object sender, DownloadStringCompletedEventArgs e)
    {
        File.WriteAllText("Data/enUS/realmlist.wtf", $"set realmlist {e.Result}");
        if (File.Exists("Cache/L/realm.txt"))
            File.Delete("Cache/L/realm.txt");
    }

    /// <summary>
    ///     Completed update download, check if it's newer
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void update_DoneRetrieveAsync(object sender, DownloadStringCompletedEventArgs e)
    {
        // Store complete versions
        var _onlineVersion = e.Result;
        var _thisVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0";
        // Split into important bits
        var _onlineVersionParts = _onlineVersion.Split('.');
        var _localVersionParts = _thisVersion.Split('.');

        // This is a little silly, but it gets the job done
        NeedsUpdate = false;
        if (int.TryParse(_onlineVersionParts[0], out var _majorVersionOnline))
            if (int.TryParse(_localVersionParts[0], out var _majorVersionLocal))
            {
                // Major update, definitely update
                if (_majorVersionOnline > _majorVersionLocal)
                    NeedsUpdate = true;

                // Same major version? Check for minor update
                if (_majorVersionOnline == _majorVersionLocal)
                    if (int.TryParse(_onlineVersionParts[1], out var _minorVersionOnline))
                        if (int.TryParse(_localVersionParts[1], out var _minorVersionLocal))
                            // Minor update, update anyway
                            if (_minorVersionOnline > _minorVersionLocal)
                                NeedsUpdate = true;
            }

        // Actual update available, 
        if (NeedsUpdate)
            // Ask for installation
            if (MessageBox.Show(m_WndRef, "The launcher has an update. Do you want to update now?", "Update available!",
                    MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.Yes,
                    MessageBoxOptions.DefaultDesktopOnly) == MessageBoxResult.Yes)
            {
                // Switch to updater software
                if (File.Exists("Updater.exe"))
                {
                    Application.Current.Shutdown();
                    Process.Start(new ProcessStartInfo("Updater.exe")
                    {
                        UseShellExecute = true
                    });
                }
                else // uh-oh. Oh well.
                {
                    MessageBox.Show(m_WndRef, "The launcher has an update but the updater is missing.",
                        "Updater missing!", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
    }
}