using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace WoWLauncher.Patcher
{
    /// <summary>
    /// Responsible for downloading new patches
    /// </summary>
    internal class PatchController
    {
        // Reference parent window
        private MainWindow m_WndRef;

        // Data
        private int m_PatchIndex;
        private bool m_Patching;
        private List<PatchData> m_Patches;
        private readonly Stopwatch m_DownloadStopWatch;

        // Textfile containing patches (seperated on each line, md5 checksum next to it, e.g: Patch-L.mpq 6fd76dec2bbca6b58c7dce68b497e2bf)
        private string m_PatchListUri = "http://MadClownWorld.com/Patch/plist.txt";
        // Folder containing the individual patches, as listed in the patch list file
        private string m_PatchUri = "http://MadClownWorld.com/Patch/Files/";
        
        // Log file path
        private string logFilePath = "Logs/Launcher_log.txt";
        // Cache for file MD5 hashes
        private Dictionary<string, string> fileHashCache; 

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
        public bool IsPatching { get { return m_Patching; } }

        public PatchController(MainWindow _wndRef)
        {
            m_WndRef = _wndRef;
            m_DownloadStopWatch = new Stopwatch();
            m_Patches = new List<PatchData>();
            m_PatchIndex = -1;
            fileHashCache = new Dictionary<string, string>();
        }
        
        private string GetChecksumFromCache(string filename)
        {
            if (fileHashCache.ContainsKey(filename))
            {
                return fileHashCache[filename];
            }
            return null;
        }
        private bool CheckHashFromCache(string filename)
        {
            if (fileHashCache.ContainsKey(filename))
            {
                return true;
            }
            return false;
        }

        private void AddFileToCache(string filename, string checksum)
        {
            if (!fileHashCache.ContainsKey(filename))
            {
                fileHashCache[filename] = checksum;
            }
        }
        
        private void LoadCache()
        {
            try
            {
                // Load the cache from a text file
                if (File.Exists("Cache/Hash/Cache.txt"))
                {
                    string[] lines = File.ReadAllLines("Cache/Hash/Cache.txt");
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length == 2)
                        {
                            string filename = parts[0];
                            string checksum = parts[1];
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
                // Save the cache to a text file
                using (StreamWriter writer = new StreamWriter("Cache/Hash/Cache.txt"))
                {
                    foreach (var entry in fileHashCache)
                    {
                        writer.WriteLine($"{entry.Key},{entry.Value}");
                    }
                }
                Log($"Saving Cache");
            }
            catch (Exception ex)
            {
                // Handle the exception or log an error if needed
                Log("Error saving cache: " + ex.Message);
            }
        }
        private void Log(string message)
        {
            // Log to patcher_log.txt file
            File.AppendAllText(logFilePath, $"{DateTime.Now}: {message}\n");
        }
        
        public static double Map(double value, double fromSource, double toSource, double fromTarget, double toTarget)
        {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }
        /// <summary>
        /// Begins checking server for patch files.
        /// </summary>
        /// <param name="_init">Is this the beginning of the check?</param>
        public void CheckPatch(bool _init = true)
        {
            if (_init)
            {
                // Reset and hide the progress info
                m_WndRef.progressInfo.IsEnabled = false;
                m_WndRef.progressBar.Value = 0;

                // Check if patch list exists
                WebRequest request = WebRequest.Create(m_PatchListUri);
                try
                {
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                }
                catch
                {
                    // Reset various controls and stop
                    m_WndRef.progressBar.Value = 100;
                    m_WndRef.playBtn.IsEnabled = true;
                    m_WndRef.progressInfo.Visibility = Visibility.Visible;
                    m_WndRef.progressInfo.Content = "Unable to download patch list!";
                    m_DownloadStopWatch.Reset();
                    return;
                }

                // Update texts
                m_WndRef.progressInfo.Visibility = Visibility.Visible;
                m_WndRef.progressInfo.Content = "Getting patch list...";

                // Prepare folders
                if (!Directory.Exists("Cache/L"))
                    Directory.CreateDirectory("Cache/L");
                if (File.Exists("Cache/L/plist.txt"))
                    File.Delete("Cache/L/plist.txt");
                if (File.Exists("Cache/Hash/Cache.txt"))
                    LoadCache();
                if (File.Exists(logFilePath))
                    File.Delete(logFilePath);

                // Begin downloading patch list
                using (WebClient wc = new WebClient())
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
                        string _incomplete = File.ReadAllText("Cache/L/patching");

                        // Remove incomplete patch files so we can download again
                        if (File.Exists($"Data/{_incomplete}"))
                            File.Delete($"Data/{_incomplete}");
                    }

                    // Begin the patch, start with first line
                    m_PatchIndex = 0;
                    m_WndRef.progressInfo.Content = "0% (Patch ?/?, downloaded 0/0 MB at 0 Mb/s)";
                    // Create recovery flag
                    File.WriteAllText("Cache/L/patching", m_Patches[m_PatchIndex].Filename);
                    // Begin patching
                    m_Patching = true;
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
        /// Create patch list
        /// </summary>
        /// <param name="_list">Raw patch list</param>
        /// <returns>Organized patch data structre</returns>
        private List<PatchData> PreparePatchList(IEnumerable<string> _list)
        {
            m_Patches = new List<PatchData>();
            foreach (string _patch in _list)
            {
                string[] _data = _patch.Split(' ');
                m_Patches.Add(new PatchData()
                {
                    Filename = _data[0],
                    Checksum = _data[1]
                });
            }
            return m_Patches;
        }

        /// <summary>
        /// The patch was downloaded, check remaining patches.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void patch_DonePatchListAsync(object? sender, AsyncCompletedEventArgs e)
        {
            CheckPatch(false);
        }
        
        private string HashFile(string patchName)
        {
            
            string _localHash = string.Empty;
            if (CheckHashFromCache(patchName))
            { 
                Log($"Cache Found: {patchName}");
                _localHash = GetChecksumFromCache(patchName);
                return _localHash;
                    
            }
            else
            {
                Log($"Cache Not Found: {patchName}");
                _localHash = string.Empty;
                using (MD5 _crypto = MD5.Create())
                {
                    using FileStream stream = File.OpenRead($"Data/{patchName}");
                    _localHash = BitConverter.ToString(_crypto.ComputeHash(stream)).Replace("-", "").ToUpperInvariant();
                    Log($"Made Hash {_localHash}");
                    AddFileToCache(patchName,_localHash);
                    SaveCache();
                    return _localHash;
                }
            }
            
        }
        /// <summary>
        /// Download patch at given index
        /// </summary>
        /// <param name="_index">Patch index from patch list</param>
        private async Task DownloadPatch(int _index)
        {
            var patchName = m_Patches[m_PatchIndex].Filename;
            var patchHash = m_Patches[m_PatchIndex].Checksum;
            double originalValue = m_PatchIndex; // Your original value in the range 0 to 30
            double minValue = 0;       // Minimum value of the original range
            double maxValue = m_Patches.Count;      // Maximum value of the original range
            double newMinValue = 0;   // Minimum value of the target range
            double newMaxValue = 100; // Maximum value of the target range

            double mappedValue = Map(originalValue, minValue, maxValue, newMinValue, newMaxValue);

            Log($"Checking Patch? {m_PatchIndex} / {m_Patches.Count} {patchName} {patchHash}");
            // Check if this patch was already downloaded previously
            // Update texts
            m_WndRef.progressInfo.IsEnabled = true;
            m_WndRef.progressBar.Value = mappedValue;
            m_WndRef.progressInfo.Visibility = Visibility.Visible;
            m_WndRef.progressInfo.Content = $"Checking Patch {m_PatchIndex} / {m_Patches.Count} {patchName}";
            await Task.Delay(50);
            
            if (File.Exists($"Data/{patchName}"))
            {
                
                
                Log($"File Found: {patchName}");
                // Calculate hash of local downloaded patch
                string _localHash = string.Empty;
                _localHash = HashFile(patchName);
                
               
                
                // Compare checksums and skip patch if it matches (no changes)
                if (_localHash.Equals(patchHash))
                {
                    Log("Compare checksums and skipped patch (no changes)");
                    // Continue with next patch
                    m_PatchIndex++;
                    if (m_PatchIndex >= m_Patches.Count)
                    {
                        Log("Done Patching");
                        FinishPatch(); // finish if nothing left
                        return;
                    }
                    else{
                        DownloadPatch(m_PatchIndex);
                        return;
                    }
                }
            }

            // Check if the given patch actually exists on server?
            string url = $"{m_PatchUri}{m_Patches[m_PatchIndex].Filename}";
            Log($"Downloading {m_Patches[m_PatchIndex].Filename} {url}");
            WebRequest request = WebRequest.Create(url);
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            }
            catch
            {
                // Stop process.
                FinishPatch();
                return;
            }

            // Update recovery flag
            File.WriteAllText("Cache/L/patching", m_Patches[m_PatchIndex].Filename);

            // Patch it in
            using (WebClient wc = new())
            {
                wc.DownloadProgressChanged += patch_GetPatchesAsync;
                wc.DownloadFileAsync(
                    new Uri($"{m_PatchUri}{m_Patches[m_PatchIndex].Filename}"),
                    $"Data/{m_Patches[m_PatchIndex].Filename}"
                );
                wc.DownloadFileCompleted += patch_DonePatchesAsync;
                m_DownloadStopWatch.Reset();
                m_DownloadStopWatch.Start();
            }

        }

        /// <summary>
        /// Completed patch, check for next.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void patch_DonePatchesAsync(object? sender, AsyncCompletedEventArgs e)
        {
            if (m_PatchIndex >= 0 && m_PatchIndex < m_Patches.Count)
            {
                // Add the downloaded file to the cache
                AddFileToCache(m_Patches[m_PatchIndex].Filename, m_Patches[m_PatchIndex].Checksum);
            }
            m_PatchIndex++;
            if (m_PatchIndex >= m_Patches.Count)
                FinishPatch();
            else
                DownloadPatch(m_PatchIndex);
        }

        /// <summary>
        /// Finish patching process and return control.
        /// </summary>
        private void FinishPatch()
        {
            // Reset visual elements
            m_WndRef.progressBar.Value = 100;
            m_WndRef.playBtn.IsEnabled = true;
            m_WndRef.progressInfo.Visibility = Visibility.Hidden;

            // Reset download data and flags
            m_Patching = false;
            m_PatchIndex = -1;
            m_Patches.Clear();
            m_DownloadStopWatch.Reset();

            // Clean up folders
            if (File.Exists("Cache/L/patching"))
                File.Delete("Cache/L/patching");
            if (File.Exists("Cache/L/plist.txt"))
                File.Delete("Cache/L/plist.txt");
            Log($"Clean Up Done");
        }

        /// <summary>
        /// Update download progress of current patch.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void patch_GetPatchesAsync(object sender, DownloadProgressChangedEventArgs e)
        {
            m_WndRef.progressInfo.Content = $"{e.ProgressPercentage}% (Patch {m_PatchIndex + 1}/{m_Patches.Count} {m_Patches[m_PatchIndex].Filename}, downloaded {e.BytesReceived / 1024f / 1024f:0.0}/{e.TotalBytesToReceive / 1024f / 1024f:0.0} MB at {(e.BytesReceived / 1024f / 1024f / m_DownloadStopWatch.Elapsed.TotalSeconds).ToString("0.0")} Mb/s)";
            m_WndRef.progressBar.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// Data structure for each patch file
        /// </summary>
        private struct PatchData
        {
            public string Filename;
            public string Checksum;
        }
    }
}
