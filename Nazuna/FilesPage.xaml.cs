using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;


namespace Nazuna;

public class FileInfo
{
    public string FileName { get; set; }
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
}

public sealed partial class FilesPage : Page
{
    MainWindow mainWindow;

    public FilesPage()
    {
        InitializeComponent();

        Files.Items.Add(new FileInfo()
        {
            FileName = "OK"
        });
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is MainWindow window)
            mainWindow = window;
    }

    void Logout(object sender, RoutedEventArgs e)
    {
        Data.Delete("Token");
        mainWindow.ContentFrame.Navigate(typeof(LoginPage), mainWindow, new DrillInNavigationTransitionInfo());
    }
}