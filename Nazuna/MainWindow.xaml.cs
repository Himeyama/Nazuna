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
}