using System;
using System.IO;
using System.Windows;
using WoWLauncher.Updater;

namespace WoWLauncher;

public partial class SettingsWindow : IDisposable
{
    public SettingsWindow()
    {
        InitializeComponent();
        LoadRealmList();
        LoadAccountName();


    }

    public void Dispose()
    {
        Close();
    }
    private void LoadRealmList()
    {
        if (File.Exists("Data/enUS/realmlist.wtf"))
        {
            // Read existing file and save it for this session
            var _realmd = File.ReadAllText("Data/enUS/realmlist.wtf");
            if (_realmd.Length > 0)
            {
                var _realmParts = _realmd.Split(' ');
                RealmList.Text = _realmParts[2];
            }
        }
        
    }
    private void UpdateRealmButton_Click(object sender, RoutedEventArgs e)
    {
        string userInput = RealmList.Text; // Get the text from the TextBox
        RealmList.Text = "" + userInput;
        UpdateController.SetRealmList(userInput);
        Inputlog.Text = "Updated Realm List";
    }

    private void LoadAccountName()
    {
        if (File.Exists("WTF/Config.wtf"))
        {
            var lines = File.ReadAllLines("WTF/Config.wtf");

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("SET accountName"))
                {
                    string[] parts = line.Split('"');
                    if (parts.Length > 1)
                    {
                        AccountName.Text = parts[1];  // Extract the value within double quotes
                        break;
                    }
                }
            }
        }
    }

    private void UpdateAccountNameButton_Click(object sender, RoutedEventArgs e)
    {
        // Path to the configuration file
        string configFilePath = "WTF/Config.wtf";

        if (File.Exists(configFilePath))
        {
            // Read all lines from the file
            var lines = File.ReadAllLines(configFilePath);

            // Iterate through the lines to find and update the account name
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith("SET accountName"))
                {
                    // Replace the existing account name value with the new value
                    lines[i] = "SET accountName \"" + AccountName.Text + "\"";
                    break; // No need to continue searching
                }
            }

            // Write the updated lines back to the file
            File.WriteAllLines(configFilePath, lines);
            Inputlog.Text = "Updated Account Name";
        }
        else
        {
            // File doesn't exist, you may want to handle this case accordingly.
            Inputlog.Text = "Could not update Account Name";
        }
    }

}