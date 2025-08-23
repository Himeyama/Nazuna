using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;


namespace Nazuna;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Title = AppTitleTextBlock.Text;

        ContentFrame.Navigate(typeof(LoginPage), this, new DrillInNavigationTransitionInfo());
    }

    public void Progress(bool isEnabled)
    {
        ProgressBar.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
    }
    
    public static void Debug(object message)
    {
        string debugFile = "debug.txt";
        string output;
        if (message is string str)
        {
            output = str;
        }
        else
        {
            try
            {
                output = System.Text.Json.JsonSerializer.Serialize(
                    message,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                );
            }
            catch
            {
                output = message?.ToString() ?? "null";
            }
        }

        string log = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {output}{Environment.NewLine}";
        System.IO.File.AppendAllText(debugFile, log);
    }
}