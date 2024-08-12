using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace WoWLauncher.Patcher;

/// <summary>
///     Responsible for downloading new patches
/// </summary>
internal class PatchController
{
    private static readonly string CacheFilePath = "Cache/Hash/Cache.txt";

    // Cache for file MD5 hashes
    private readonly Dictionary<string, string> fileHashCache;

    // Log file path
    private readonly string logFilePath = "Logs/Launcher_log.log";
    private readonly Stopwatch m_DownloadStopWatch;

    // Textfile containing patches (seperated on each line, md5 checksum next to it, e.g: Patch-L.mpq 6fd76dec2bbca6b58c7dce68b497e2bf)
    private readonly string m_PatchListUri = "http://MadClownWorld.com/Patch/CheckList.txt";

    // Folder containing the individual patches, as listed in the patch list file
    private readonly string m_PatchUri = "http://MadClownWorld.com/Patch/Files/";

    // Reference parent window
    private readonly MainWindow m_WndRef;
    private List<PatchData> m_Patches;

    // Data
    private int m_PatchIndex;

    public PatchController(MainWindow _wndRef)
    {
        m_WndRef = _wndRef;
        m_DownloadStopWatch = new Stopwatch();
        m_Patches = new List<PatchData>();
        m_PatchIndex = -1;
        fileHashCache = new Dictionary<string, string>();
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
                |--- Patch-C.MPQ         and filenames must not contain spaces
                |--- ... etc
     
     *
     *
     */

    // Accessor
    public bool IsPatching { get; private set; }

    private string GetChecksumFromCache(string filename)
    {
        if (fileHashCache.ContainsKey(filename)) return fileHashCache[filename];
        return null;
    }

    private bool CheckHashFromCache(string filename)
    {
        if (fileHashCache.ContainsKey(filename)) return true;
        return false;
    }

    private void AddFileToCache(string filename, string checksum)
    {
        fileHashCache[filename] = checksum;
    }

    private void LoadCache()
    {
        try
        {
            // Load the cache from a text file
            if (File.Exists(CacheFilePath))
            {
                var lines = File.ReadAllLines(CacheFilePath);
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length == 2)
                    {
                        var filename = parts[0];
                        var checksum = parts[1];
                        fileHashCache[filename] = checksum;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Handle the exception or log an error if needed
            Log("Error loading cache: " + ex.Message);
        }
    }

    private void SaveCache()
    {
        try
        {
            // Prepare folder
            if (!Directory.Exists("Cache/Hash"))
                Directory.CreateDirectory("Cache/Hash");

            var cacheFilePath = CacheFilePath;
            var existingCache = new Dictionary<string, string>();

            // Read the existing cache, if it exists
            if (File.Exists(cacheFilePath))
            {
                var lines = File.ReadAllLines(cacheFilePath);
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length == 2)
                    {
                        var filename = parts[0];
                        var checksum = parts[1];
                        existingCache[filename] = checksum;
                    }
                }
            }

            // Update or add the entries from your current cache
            foreach (var entry in fileHashCache) existingCache[entry.Key] = entry.Value;

            // Save the updated cache to a text file
            using (var writer = new StreamWriter(cacheFilePath))
            {
                foreach (var entry in existingCache) writer.WriteLine($"{entry.Key},{entry.Value}");
            }

            Log("Saving Cache");
        }
        catch (Exception ex)
        {
            // Handle the exception or log an error if needed
            Log("Error saving cache: " + ex.Message);
        }
    }

    public static void ClearCache()
    {
        if (File.Exists(CacheFilePath))
            File.Delete(CacheFilePath);
    }

    public void Log(string message)
    {
        // Log to patcher_log.txt file
        File.AppendAllText(logFilePath, $"{DateTime.Now}: {message}\n");
    }

    public static double Map(double value, double fromSource, double toSource, double fromTarget, double toTarget)
    {
        return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
    }

    /// <summary>
    ///     Begins checking server for patch files.
    /// </summary>
    /// <param name="_init">Is this the beginning of the check?</param>
    public async Task CheckPatch(bool _init = true)
    {
        if (_init)
        {
            // Reset and hide the progress info
            m_WndRef.ProgressInfo.IsEnabled = false;
            m_WndRef.ProgressInfo2.IsEnabled = false;
            m_WndRef.ProgressInfo3.IsEnabled = false;
            m_WndRef.ProgressBar.Value = 0;

            // Check if patch list exists
            var request = WebRequest.Create(m_PatchListUri);
            try
            {
                var response = await request.GetResponseAsync(); // Use GetResponseAsync for non-blocking operation
                if (response is HttpWebResponse httpResponse)
                {
                    // Handle the response as needed
                    // For example, you can read the response stream asynchronously
                    using (var responseStream = httpResponse.GetResponseStream())
                    using (var reader = new StreamReader(responseStream))
                    {
                        var responseContent = await reader.ReadToEndAsync();
                        // Process the responseContent here
                    }
                }
            }
            catch
            {
                // Reset various controls and stop
                m_WndRef.ProgressBar.Value = 100;
                m_WndRef.PlayBtn.IsEnabled = true;
                m_WndRef.ProgressInfo.Visibility = Visibility.Visible;
                m_WndRef.ProgressInfo2.Visibility = Visibility.Visible;
                m_WndRef.ProgressInfo3.Visibility = Visibility.Visible;
                m_WndRef.ProgressInfo4.Visibility = Visibility.Visible;
                m_WndRef.ProgressInfo.Content = "Unable to download Check list!";
                m_WndRef.ProgressInfo2.Content = " ";
                m_WndRef.ProgressInfo3.Content = " ";
                m_WndRef.ProgressInfo4.Content = " ";
                m_DownloadStopWatch.Reset();
                return;
            }

            // Update texts
            m_WndRef.ProgressInfo.Visibility = Visibility.Visible;
            m_WndRef.ProgressInfo2.Visibility = Visibility.Visible;
            m_WndRef.ProgressInfo3.Visibility = Visibility.Visible;
            m_WndRef.ProgressInfo4.Visibility = Visibility.Visible;
            m_WndRef.ProgressInfo.Content = "Getting Check list...";
            m_WndRef.ProgressInfo2.Content = " ";
            m_WndRef.ProgressInfo3.Content = " ";
            m_WndRef.ProgressInfo4.Content = " ";

            // Prepare folders
            if (File.Exists(CacheFilePath))
            {
                DateTime specificDate = new DateTime(2023, 10, 24, 11, 25, 0); // October 31, 2023, 12:00:00 PM

                DateTime fileLastModified = File.GetLastWriteTime(CacheFilePath);

                if (fileLastModified < specificDate)
                {
                    File.Delete(CacheFilePath);
                    Log("You had and old Hash Cache. A New way was made.");
                }
                
            }
            
            
            if (!Directory.Exists("Cache/L"))
                Directory.CreateDirectory("Cache/L");
            if (File.Exists("Cache/L/plist.txt"))
                File.Delete("Cache/L/plist.txt");
            if (File.Exists("Cache/L/patching"))
                File.Delete("Cache/L/patching");
            if (File.Exists(CacheFilePath))
                LoadCache();
            if (!Directory.Exists("Logs"))
                Directory.CreateDirectory("Logs");
            if (File.Exists(logFilePath))
                File.Delete(logFilePath);
            
           
            

            // Begin downloading patch list
            using (var wc = new WebClient())
            {
                wc.DownloadFileAsync(
                    new Uri(m_PatchListUri),
                    "Cache/L/plist.txt"
                );
                wc.DownloadFileCompleted += patch_DonePatchListAsync;
            }

            return;
        }

        // Check if file was placed correctly
        if (File.Exists("Cache/L/plist.txt"))
        {
            // Check if there's any patches available
            m_Patches = PreparePatchList(File.ReadLines("Cache/L/plist.txt"));

            if (m_Patches.Count > 0)
            {
                // Prepare game data folder
                if (!Directory.Exists("Data"))
                    Directory.CreateDirectory("Data");

                // Check for incomplete data
                if (File.Exists("Cache/L/patching"))
                {
                    var _incomplete = File.ReadAllText("Cache/L/patching");

                    // Remove incomplete patch files so we can download again
                    if (File.Exists($"Data/{_incomplete}"))
                        File.Delete($"Data/{_incomplete}");
                }

                // Begin the patch, start with first line
                m_PatchIndex = 0;
                m_WndRef.ProgressInfo.Content = "0% (Patch ?/?, downloaded 0/0 MB at 0 Mb/s)";
                m_WndRef.ProgressInfo2.Content = " ";
                m_WndRef.ProgressInfo3.Content = " ";
                m_WndRef.ProgressInfo3.Content = " ";
                // Create recovery flag
                File.WriteAllText("Cache/L/patching", m_Patches[m_PatchIndex].Filename.ToLower());
                // Begin patching
                IsPatching = true;
                DownloadPatch(m_PatchIndex);
            }
            else
            {
                // Finish up and return control
                FinishPatch();
            }
        }
    }

    /// <summary>
    ///     Create patch list
    /// </summary>
    /// <param name="_list">Raw patch list</param>
    /// <returns>Organized patch data structre</returns>
    private List<PatchData> PreparePatchList(IEnumerable<string> list)
    {
        if (list == null) throw new ArgumentNullException(nameof(list));

        var patches = new List<PatchData>();

        foreach (var patch in list)
        {
            var data = patch?.Split(' ');

            if (data != null && data.Length >= 3)
                patches.Add(new PatchData
                {
                    Filename = data[0]?.ToLower(),
                    Checksum = data[1],
                    Link = data[2]
                });
            // Handle invalid or missing data
        }

        return patches;
    }

    /// <summary>
    ///     The patch was downloaded, check remaining patches.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void patch_DonePatchListAsync(object? sender, AsyncCompletedEventArgs e)
    {
        CheckPatch(false);
    }

    /// <summary>
    ///     Download patch at given index
    /// </summary>
    /// <param name="_index">Patch index from patch list</param>
    private async Task DownloadPatch(int _index)
    {
        var patchName = m_Patches[m_PatchIndex].Filename.ToLower();
        var patchHash = m_Patches[m_PatchIndex].Checksum;
        var patchLink = m_Patches[m_PatchIndex].Link;
        double originalValue = m_PatchIndex; // Your original value in the range 0 to 30
        double minValue = 0; // Minimum value of the original range
        double maxValue = m_Patches.Count; // Maximum value of the original range
        double newMinValue = 0; // Minimum value of the target range
        double newMaxValue = 100; // Maximum value of the target range

        var mappedValue = Map(originalValue, minValue, maxValue, newMinValue, newMaxValue);

        Log($"Checking File {m_PatchIndex + 1} / {m_Patches.Count} {patchName}");
        if (!Directory.Exists("Data"))
        {
            CheckPatch();
            return;
        }

        // Check if this patch was already downloaded previously
        // Update texts
        m_WndRef.ProgressInfo.IsEnabled = true;
        m_WndRef.ProgressInfo2.IsEnabled = true;
        m_WndRef.ProgressInfo3.IsEnabled = true;
        m_WndRef.ProgressInfo4.IsEnabled = true;
        //m_WndRef.ProgressBar.Value = mappedValue;
        m_WndRef.ProgressInfo.Visibility = Visibility.Visible;
        m_WndRef.ProgressInfo2.Visibility = Visibility.Visible;
        m_WndRef.ProgressInfo3.Visibility = Visibility.Visible;
        m_WndRef.ProgressInfo4.Visibility = Visibility.Visible;
        m_WndRef.ProgressInfo.Content = $"Checking File {m_PatchIndex + 1} / {m_Patches.Count} {patchName}";
        m_WndRef.ProgressInfo2.Content = " ";
        m_WndRef.ProgressInfo3.Content = " ";
        m_WndRef.ProgressInfo4.Content = " ";

        if (File.Exists($"Data/{patchName}"))
        {
            Log($"Patch Found: {patchName}");
            // Calculate hash of local downloaded patch
            var _localHash = string.Empty;
            if (CheckHashFromCache(patchName))
            {
                Log($"Cache Found: {patchName}");
                _localHash = GetChecksumFromCache(patchName);
                m_WndRef.ProgressBar.Value = 100;
                await Task.Delay(25);
            }
            else
            {
                Log($"Cache Not Found: {patchName}");
                await Task.Delay(500); // You can add a delay if needed

                m_WndRef.ProgressBar.Value = 0;
                string localHash = await Task.Run(async () =>
                {
                    using (var _crypto = SHA256.Create())
                    using (var stream = File.OpenRead($"Data/{patchName}"))
                    {
                        long totalBytesRead = 0;
                        long fileSize = new FileInfo($"Data/{patchName}").Length;
                        byte[] buffer = new byte[4096];
                        int updateFrequency = 10000; // Set the frequency of progress updates (e.g., every 4096 bytes)
                        



                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            try
                            {
                                _crypto.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalBytesRead += bytesRead;
                                
                                if (totalBytesRead % updateFrequency == 0)
                                {
                                    m_WndRef.Dispatcher.Invoke(() =>
                                    {
                                        UpdateSHA256Progress(totalBytesRead, fileSize);
                                    });
                                }
                                

                            }
                            catch (Exception ex)
                            {
                                Log($"Error in SHA-256 hashing: {ex.Message}");
                                // Handle the exception or log it for further investigation
                                // You may want to add more specific error handling here
                            }
                        }

                        Log("Here 4");
                        _crypto.TransformFinalBlock(buffer, 0, 0);
                        byte[] hashBytes = _crypto.Hash;

                        string _localHash = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
                        Log($"Made Hash {_localHash}");
                        AddFileToCache(patchName, _localHash);
                        SaveCache();

                        return _localHash; // Return the computed hash from the task
                    }
                });

                _localHash = localHash;


            }


            // Compare checksums and skip patch if it matches (no changes)

            Log($"Compare {patchName} Hash Local {_localHash} Server {patchHash}");
            if (_localHash.Equals(patchHash))
            {
                Log("Pass");
                // Continue with next patch
                m_PatchIndex++;
                if (m_PatchIndex >= m_Patches.Count)
                {
                    Log("Done Patching");
                    FinishPatch(); // finish if nothing left
                    return;
                }

                DownloadPatch(m_PatchIndex);
                return;
            }

            Log("Fail");
        }
        else
        {
            Log($"Patch {patchName} missing");
        }

        // Check if the given patch actually exists on server?
        var url = $"{patchLink}";
        var urlBackup = $"{m_PatchUri}{patchName}";
        Log($"Downloading {patchName} {url}");
        HttpWebRequest request = null;

        try
        {
            request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 10000; // Set the timeout to 10 seconds
        }
        catch (Exception ex)
        {
            Log($"Error creating request for primary URL: {ex.Message}");
        }

        if (request != null)
            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                // Continue with your download logic here
            }
            catch (Exception ex)
            {
                // Handle the error with the primary URL
                Log($"Primary URL Error: {ex.Message}");

                // Retry with the backup URL
                Log($"Retrying with backup URL {urlBackup}");

                try
                {
                    var requestBackup = (HttpWebRequest)WebRequest.Create(urlBackup);
                    requestBackup.Timeout = 10000; // Set the timeout to 10 seconds for the backup URL
                    var responseBackup = (HttpWebResponse)requestBackup.GetResponse();
                    // Continue with your download logic using the backup URL here
                }
                catch (Exception exBackup)
                {
                    // Handle the error with the backup URL, e.g., log the error or finish the patch
                    Log($"Backup URL Error: {exBackup.Message}");
                    Log("Failed to fully update client. Try again.");
                    FinishPatch();
                }
            }
        else
            // Handle the case where creating the request for the primary URL failed


        // Update recovery flag
        File.WriteAllText("Cache/L/patching", patchName);

        // Patch it in
        using (WebClient wc = new())
        {
            wc.DownloadProgressChanged += patch_GetPatchesAsync;
            wc.DownloadFileAsync(
                new Uri($"{m_PatchUri}{patchName}"),
                $"Data/{patchName}"
            );
            wc.DownloadFileCompleted += patch_DonePatchesAsync;
            m_DownloadStopWatch.Reset();
            m_DownloadStopWatch.Start();
        }
    }
    private async Task UpdateSHA256Progress(long totalBytesRead, long fileSize)
    {
        if (fileSize > 0)
        {
            int percentage = (int)((totalBytesRead * 100) / fileSize);

            // Update the SHA-256 progress on the UI thread using Dispatcher.BeginInvoke
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Update UI controls here
                m_WndRef.ProgressBar.Value = percentage;
                m_WndRef.ProgressInfo2.Content = $"{percentage}%";
            }));

        }
    }



    /// <summary>
    ///     Completed patch, check for next.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void patch_DonePatchesAsync(object? sender, AsyncCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            ErroredPatch($"Download failed: {e.Error.Message}");
            return;
        }

        if (e.Cancelled)
        {
            ErroredPatch("Download was cancelled.");
            return;
        }

        Log("Download Completed");
        var patchName = m_Patches[m_PatchIndex].Filename.ToLower();
        var patchHash = m_Patches[m_PatchIndex].Checksum;

        if (m_PatchIndex >= 0 && m_PatchIndex < m_Patches.Count)
        {
            // Add the downloaded file to the cache
            AddFileToCache(patchName, patchHash);
            SaveCache();
        }

        m_PatchIndex++;
        if (m_PatchIndex >= m_Patches.Count)
            FinishPatch();
        else
            DownloadPatch(m_PatchIndex);
    }
    
    /// <summary>
    ///     Errored patching process and return control.
    /// </summary>
    private void ErroredPatch(string MSG)
    {
        // Reset visual elements
        m_WndRef.ProgressBar.Value = 100;
        m_WndRef.PlayBtn.IsEnabled = false;
        m_WndRef.ProgressInfo.Visibility = Visibility.Visible;
        m_WndRef.ProgressInfo2.Visibility = Visibility.Visible;
        m_WndRef.ProgressInfo3.Visibility = Visibility.Hidden;
        m_WndRef.ProgressInfo4.Visibility = Visibility.Hidden;
        m_WndRef.ProgressInfo.Content = "Something happen that caused and error downloading.";
        m_WndRef.ProgressInfo2.Content = "Please restart launcher to try again.";
        m_DownloadStopWatch.Reset();
        Log(MSG);
    }

    /// <summary>
    ///     Finish patching process and return control.
    /// </summary>
    private void FinishPatch()
    {
        // Reset visual elements
        m_WndRef.ProgressBar.Value = 100;
        m_WndRef.PlayBtn.IsEnabled = true;
        m_WndRef.ProgressInfo.Visibility = Visibility.Hidden;
        m_WndRef.ProgressInfo2.Visibility = Visibility.Hidden;
        m_WndRef.ProgressInfo3.Visibility = Visibility.Hidden;
        m_WndRef.ProgressInfo4.Visibility = Visibility.Hidden;

        // Reset download data and flags
        IsPatching = false;
        m_PatchIndex = -1;
        m_Patches.Clear();
        m_DownloadStopWatch.Reset();

        // Clean up folders
        if (File.Exists("Cache/L/patching"))
            File.Delete("Cache/L/patching");
        if (File.Exists("Cache/L/plist.txt"))
            File.Delete("Cache/L/plist.txt");
        Log("Clean Up Done");
    }

    /// <summary>
    ///     Update download progress of current patch.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void patch_GetPatchesAsync(object sender, DownloadProgressChangedEventArgs e)
    {
        var patchName = m_Patches[m_PatchIndex].Filename.ToLower();
        var patchSizeMB = e.TotalBytesToReceive / (1024f * 1024f);
        var downloadedMB = e.BytesReceived / (1024f * 1024f);
        var downloadSpeed = downloadedMB / m_DownloadStopWatch.Elapsed.TotalSeconds;
        var LeftToDownload = patchSizeMB - downloadedMB;

        //Calculate the remaining time
        var remainingSeconds = (patchSizeMB - downloadedMB) / downloadSpeed;

        // Format the remaining time as hh:mm:ss
        var eta = TimeSpan.FromSeconds(remainingSeconds).ToString(@"h\h\ m\m\ s\s");


        m_WndRef.ProgressBar.Value = e.ProgressPercentage;
        m_WndRef.ProgressInfo.Content = $"Downloading {m_PatchIndex + 1}/{m_Patches.Count} {patchName}";
        m_WndRef.ProgressInfo2.Content = $"{e.ProgressPercentage}%    {downloadSpeed:0.0} Mb/s";
        m_WndRef.ProgressInfo3.Content = $"{LeftToDownload:0.0} MB";
        m_WndRef.ProgressInfo4.Content = $"ETA: {eta}";
    }

    /// <summary>
    ///     Data structure for each patch file
    /// </summary>
    private struct PatchData
    {
        public string Filename;
        public string Checksum;
        public string Link;
    }
}