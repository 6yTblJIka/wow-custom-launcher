using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CleckList.Systems
{
    public static class UIHelper
    {
        static readonly TextAlignment _SetTextAlignment = TextAlignment.Left;
        static readonly VerticalAlignment _SetVerticalAlignment = VerticalAlignment.Center;
        static readonly double _SetHeight = 20;

        public static void AddFileToGrid(Grid grid, string fileName, string checksum, string url, RoutedEventHandler urlButtonClickHandler)
        {
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_SetHeight + 10) });

            // Create a container Grid for the row
            var rowGrid = new Grid();
            rowGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(500) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });

            var fileNameTextBlock = new TextBlock
            {
                Text = fileName,
                Margin = new Thickness(5),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = fileName,
                MaxWidth = 125, // Adjust as needed
                Height = _SetHeight,
                VerticalAlignment = _SetVerticalAlignment,
                TextAlignment = _SetTextAlignment,
            };

            var checksumTextBlock = new TextBlock
            {
                Text = checksum,
                Margin = new Thickness(5),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = checksum,
                MaxWidth = 500, // Adjust as needed
                Height = _SetHeight,
                VerticalAlignment = _SetVerticalAlignment,
                TextAlignment = _SetTextAlignment,
            };

            var urlTextBlock = CreateUrlTextBlock(url, 380); // Adjust as needed

            var urlButton = new Button
            {
                Content = url == null || url == "null" ? "Add" : "Update",
                Margin = new Thickness(5)
            };
            urlButton.Click += urlButtonClickHandler;

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Add URL Button and URL TextBlock to the StackPanel
            stackPanel.Children.Add(urlButton);
            stackPanel.Children.Add(urlTextBlock);

            // Set row and column for each element in the rowGrid
            Grid.SetRow(fileNameTextBlock, 0);
            Grid.SetColumn(fileNameTextBlock, 0);

            Grid.SetRow(checksumTextBlock, 0);
            Grid.SetColumn(checksumTextBlock, 1);

            Grid.SetRow(stackPanel, 0);
            Grid.SetColumn(stackPanel, 2);

            // Add elements to the rowGrid
            rowGrid.Children.Add(fileNameTextBlock);
            rowGrid.Children.Add(checksumTextBlock);
            rowGrid.Children.Add(stackPanel);

            // Apply alternating row color
            if (row % 2 == 0)
            {
                var lightGrayBrush = new SolidColorBrush(Colors.LightGray);
                rowGrid.Background = lightGrayBrush;
            }

            // Set row and column for the rowGrid in the main grid
            Grid.SetRow(rowGrid, row);
            Grid.SetColumnSpan(rowGrid, 3);

            // Add the rowGrid to the main grid
            grid.Children.Add(rowGrid);

            // Debug logging
            Console.WriteLine($"Added row {row}: {fileName}, {checksum}, {url ?? "null"}");
        }

        public static bool IsFileInGrid(Grid grid, string fileName)
        {
            foreach (UIElement element in grid.Children)
            {
                if (element is Grid rowGrid)
                {
                    foreach (UIElement child in rowGrid.Children)
                    {
                        if (child is TextBlock textBlock && textBlock.Text == fileName && Grid.GetColumn(textBlock) == 0)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static TextBlock CreateUrlTextBlock(string url, double maxWidth)
        {
            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = url,
                MaxWidth = maxWidth,
                Height = _SetHeight,
                Margin = new Thickness(5),
                TextTrimming = TextTrimming.CharacterEllipsis, // We'll handle trimming manually
                VerticalAlignment = _SetVerticalAlignment,
                TextAlignment = _SetTextAlignment,
            };

            if (!string.IsNullOrEmpty(url) && url.Length > 50) // Adjust the length as needed
            {
                string visiblePart = url.Substring(0, 44); // Adjust the length as needed
                textBlock.Inlines.Add(new Run(visiblePart));
                textBlock.Inlines.Add(new Run(".....") { FontWeight = FontWeights.Bold });
            }
            else
            {
                textBlock.Text = url ?? "null";
            }

            return textBlock;
        }
    }
}
