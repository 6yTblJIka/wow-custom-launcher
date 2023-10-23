using System;
using System.Windows;

namespace WoWLauncher;

public partial class ClearCacheWindow : IDisposable
{
    public ClearCacheWindow()
    {
        InitializeComponent();
    }

    public void Dispose()
    {
        Close();
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}