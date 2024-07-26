using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace CleckList.Systems
{
    public static class FileHelper
    {
        public static string[] InitializeFiles(string filesDirectory)
        {
            if (!Directory.Exists(filesDirectory))
            {
                MessageBox.Show("The 'Files' directory does not exist.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return Array.Empty<string>();
            }

            return Directory.GetFiles(filesDirectory);
        }

        public static Dictionary<string, (string checksum, string url)> LoadOldCheckListData(string checkListFilePath)
        {
            var oldCheckListData = new Dictionary<string, (string checksum, string url)>();

            if (File.Exists(checkListFilePath))
            {
                var checkListLines = File.ReadAllLines(checkListFilePath);
                foreach (var line in checkListLines)
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 2)
                    {
                        var fileName = parts[0];
                        var checksum = parts[1];
                        var url = parts.Length == 3 ? parts[2] : null;
                        oldCheckListData[fileName] = (checksum, url);
                    }
                }
            }

            return oldCheckListData;
        }

        public static void SaveChecklistToFile(string checkListFilePath, Grid filesGrid)
        {
            try
            {
                using (var writer = new StreamWriter(checkListFilePath))
                {
                    var processedFiles = new HashSet<string>();

                    foreach (UIElement element in filesGrid.Children)
                    {
                        if (element is Grid rowGrid)
                        {
                            var fileNameTextBlock = GetTextBlockInGrid(rowGrid, 0);
                            var checksumTextBlock = GetTextBlockInGrid(rowGrid, 1);
                            var urlTextBlock = GetUrlTextBlockInGrid(rowGrid);

                            if (fileNameTextBlock == null || checksumTextBlock == null)
                            {
                                MessageBox.Show("Error: Unable to find necessary TextBlocks.");
                                continue;
                            }

                            var fileName = fileNameTextBlock.Text;

                            // Skip duplicate files
                            if (processedFiles.Contains(fileName))
                            {
                                continue;
                            }

                            processedFiles.Add(fileName);

                            var checksum = checksumTextBlock.Text;
                            var url = urlTextBlock?.Text == "null" ? null : urlTextBlock?.Text;

                            // Detailed logging
                            Console.WriteLine($"File: {fileName}");
                            Console.WriteLine($"Checksum: {checksum}");
                            Console.WriteLine($"URL TextBlock: {urlTextBlock?.Text}");
                            Console.WriteLine($"URL: {url}");

                            writer.WriteLine($"{fileName} {checksum} {url ?? "null"}");

                            // Debug messages
                            Console.WriteLine($"Saved: {fileName} {checksum} {url ?? "null"}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving checklist to file: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static TextBlock GetTextBlockInGrid(Grid grid, int columnIndex)
        {
            foreach (UIElement element in grid.Children)
            {
                if (element is TextBlock textBlock && Grid.GetColumn(textBlock) == columnIndex)
                {
                    return textBlock;
                }
            }
            return null;
        }

        private static TextBlock GetUrlTextBlockInGrid(Grid grid)
        {
            foreach (UIElement element in grid.Children)
            {
                if (element is StackPanel stackPanel)
                {
                    foreach (UIElement child in stackPanel.Children)
                    {
                        if (child is TextBlock textBlock)
                        {
                            Console.WriteLine($"Found URL TextBlock: {textBlock.Text} in StackPanel");
                            return textBlock;
                        }
                    }
                }
            }

            Console.WriteLine("No URL TextBlock found in StackPanel");
            return null;
        }

        public static void SaveLinksToFile(string linksFilePath, Grid filesGrid)
        {
            try
            {
                using (var writer = new StreamWriter(linksFilePath))
                {
                    var processedFiles = new HashSet<string>();

                    foreach (UIElement element in filesGrid.Children)
                    {
                        if (element is Grid rowGrid)
                        {
                            var fileNameTextBlock = GetTextBlockInGrid(rowGrid, 0);
                            var urlTextBlock = GetUrlTextBlockInGrid(rowGrid);

                            if (fileNameTextBlock != null && urlTextBlock != null && urlTextBlock.Text != "null")
                            {
                                var fileName = fileNameTextBlock.Text;

                                // Skip duplicate files
                                if (processedFiles.Contains(fileName))
                                {
                                    continue;
                                }

                                processedFiles.Add(fileName);

                                writer.WriteLine($"{fileName} {urlTextBlock.Text}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving links to file: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
