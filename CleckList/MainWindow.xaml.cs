using CleckList.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CleckList
{
    public partial class MainWindow : Window
    {
        private string[] _files;
        private string _filesDirectory;
        private DispatcherTimer _timer;
        private int _currentFileIndex;
        private ProgressWindow _progressWindow;
        private string _checkListFilePath;
        private string _linksFilePath;
        private Dictionary<string, (string checksum, string url)> _oldCheckListData;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
        }

        private void InitializeApp()
        {

            _checkListFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CheckList.txt");
            _linksFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileLinks.txt");
            _filesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files");

            _files = FileHelper.InitializeFiles(_filesDirectory);
            _oldCheckListData = FileHelper.LoadOldCheckListData(_checkListFilePath);
            LoadCheckList();
            DisplayFiles();
            ResetTimer();
        }

        private void ResetTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= CalculateNextFileChecksum;
            }

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _timer.Tick += CalculateNextFileChecksum;
        }
        

        private void LoadCheckList()
        {
            if (_oldCheckListData.Count > 0)
            {
                foreach (var fileData in _oldCheckListData)
                {
                    string fileName = fileData.Key;
                    string checksum = fileData.Value.checksum;
                    string url = fileData.Value.url;

                    var filePath = Path.Combine(_filesDirectory, fileName);
                    if (File.Exists(filePath))
                    {
                        UIHelper.AddFileToGrid(FilesGrid, fileName, checksum, url, OnUrlButtonClick);
                    }
                }
            }
        }

        private void DisplayFiles()
        {
            foreach (var file in _files)
            {
                string fileName = Path.GetFileName(file);
                if (!UIHelper.IsFileInGrid(FilesGrid, fileName))
                {
                    string checksum = _oldCheckListData.ContainsKey(fileName)
                        ? _oldCheckListData[fileName].checksum
                        : string.Empty;
                    string url = _oldCheckListData.ContainsKey(fileName) ? _oldCheckListData[fileName].url : null;
                    UIHelper.AddFileToGrid(FilesGrid, fileName, checksum, url, OnUrlButtonClick);
                }
            }
        }

        private void OnUrlButtonClick(object sender, RoutedEventArgs e)
{
    try
    {
        var button = sender as Button;
        if (button == null)
        {
            MessageBox.Show("Error: Sender is not a Button.");
            return;
        }

        var parent = button.Parent;
        while (!(parent is StackPanel))
        {
            parent = (parent as FrameworkElement).Parent;
            if (parent == null)
            {
                MessageBox.Show("Error: Unable to find parent StackPanel.");
                return;
            }
        }

        var stackPanel = parent as StackPanel;
        var grid = stackPanel.Parent as Grid;

        // Debugging: Log all children in the Grid
        // foreach (UIElement child in grid.Children)
        // {
        //     if (child is TextBlock textBlock)
        //     {
        //         Console.WriteLine($"TextBlock found: {textBlock.Text}, Row: {Grid.GetRow(child)}, Column: {Grid.GetColumn(child)}");
        //     }
        //     else if (child is Button btn)
        //     {
        //         Console.WriteLine($"Button found: {btn.Content}, Row: {Grid.GetRow(child)}, Column: {Grid.GetColumn(child)}");
        //     }
        // }

        // Find the file name TextBlock in the same row
        TextBlock fileNameTextBlock = null;
        foreach (UIElement child in grid.Children)
        {
            if (Grid.GetRow(child) == Grid.GetRow(stackPanel) && Grid.GetColumn(child) == 0)
            {
                fileNameTextBlock = child as TextBlock;
                break;
            }
        }

        if (fileNameTextBlock == null)
        {
            MessageBox.Show("Error: Unable to find file name TextBlock.");
            return;
        }

        // Find the URL TextBlock in the same StackPanel
        TextBlock urlTextBlock = stackPanel.Children.OfType<TextBlock>().FirstOrDefault();
        if (urlTextBlock == null)
        {
            MessageBox.Show("Error: Unable to find URL TextBlock.");
            return;
        }

        string fileName = fileNameTextBlock.Text;

        var inputDialog = new InputDialog("Enter URL:", urlTextBlock.Text == "null" ? string.Empty : urlTextBlock.ToolTip.ToString());
        if (inputDialog.ShowDialog() == true)
        {
            if (inputDialog.IsRemoved)
            {
                urlTextBlock.Text = "null";
                urlTextBlock.ToolTip = null;
                urlTextBlock.Visibility = Visibility.Collapsed;
                button.Content = "Add";
            }
            else
            {
                var newUrlTextBlock = UIHelper.CreateUrlTextBlock(inputDialog.Answer, 380); // Adjust as needed
                newUrlTextBlock.ToolTip = inputDialog.Answer;
                newUrlTextBlock.Visibility = Visibility.Visible;

                stackPanel.Children.Remove(urlTextBlock);
                stackPanel.Children.Insert(1, newUrlTextBlock);

                button.Content = string.IsNullOrWhiteSpace(inputDialog.Answer) ? "Add" : "Update";

                // Debug logging
                Console.WriteLine($"URL set for file {fileName}: {inputDialog.Answer}");
                Console.WriteLine($"Updated URL TextBlock: {newUrlTextBlock.Text}");
            }

            FileHelper.SaveLinksToFile(_linksFilePath, FilesGrid);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}\n{ex.StackTrace}");
    }
}

        private void OnCalculateChecksumsClicked(object sender, RoutedEventArgs e)
        {
            ClearGrid();
            _progressWindow = new ProgressWindow
            {
                Owner = this
            };
            _progressWindow.ProgressBar.Maximum = _files.Length;
            _progressWindow.ProgressBar.Value = 0;
            _progressWindow.Topmost = true;
            _progressWindow.Show();
            
            _currentFileIndex = 0;
            ResetTimer();
            _timer.Start();
        }

        private bool _isCalculatingChecksum = false;

        private async void CalculateNextFileChecksum(object sender, EventArgs e)
        {
            if (_isCalculatingChecksum)
                return; // Prevent re-entrance

            _isCalculatingChecksum = true; // Set the flag to indicate processing has started

            if (_currentFileIndex >= _files.Length)
            {
                _timer.Stop();
                _progressWindow.Close();
                MessageBox.Show("Checksum calculation completed!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                //await DisplayFilesWithChecksums();
                _isCalculatingChecksum = false; // Reset the flag
                return;
            }

            string filePath = _files[_currentFileIndex];
            string fileName = Path.GetFileName(filePath);

            // Pass a lambda to update the ProgressText
            string fileChecksum = await ChecksumHelper.CalculateFileChecksumAsync(filePath, _currentFileIndex + 1, _files.Length,
                percentage => Dispatcher.Invoke(() =>
                {
                    // Check if the ProgressWindow is still open
                    if (!_progressWindow.IsLoaded)
                    {
                        Close();
                        return;
                    }

                    // Update the progress bar
                    _progressWindow.ProgressBar.Value = _currentFileIndex + 1;
                    _progressWindow.ProgressText.Text = $"Processing file {_currentFileIndex + 1} of {_files.Length}";
                    _progressWindow.ProgressBar2.Value = percentage; // Update the ProgressText
                    _progressWindow.ProgressText2.Text = $"{percentage}%"; // Update the ProgressText
                    Console.Write(
                        $"\rCalculating for file {_currentFileIndex + 1} of {_files.Length} ({percentage}%): {Path.GetFileName(filePath)}     ");
                }));

            string url = null;
            if (_oldCheckListData.ContainsKey(fileName) && _oldCheckListData[fileName].checksum == fileChecksum)
            {
                url = _oldCheckListData[fileName].url;
            }

            if (!UIHelper.IsFileInGrid(FilesGrid, fileName))
            {
                // Add the file and checksum to the grid
                UIHelper.AddFileToGrid(FilesGrid, fileName, fileChecksum, url, OnUrlButtonClick);
            }

            _currentFileIndex++;
            _isCalculatingChecksum = false; // Reset the flag after processing
        }
        private async Task DisplayFilesWithChecksums()
        {
            ClearGrid();

            foreach (var file in _files)
            {
                string fileName = Path.GetFileName(file);

                // Pass null for the progress update if you don't want to show progress here
                string fileChecksum =
                    await ChecksumHelper.CalculateFileChecksumAsync(file, 0, 0, null); // Await the checksum calculation

                string url = null;
                if (_oldCheckListData.ContainsKey(fileName) && _oldCheckListData[fileName].checksum == fileChecksum)
                {
                    url = _oldCheckListData[fileName].url;
                }

                UIHelper.AddFileToGrid(FilesGrid, fileName, fileChecksum, url, OnUrlButtonClick);
            }
        }
        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            FileHelper.SaveChecklistToFile(_checkListFilePath, FilesGrid);
            MessageBox.Show($"CheckList.txt saved to {_checkListFilePath}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void ClearGrid()
        {
            FilesGrid.Children.Clear();
            FilesGrid.RowDefinitions.Clear();
            FilesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }
}
