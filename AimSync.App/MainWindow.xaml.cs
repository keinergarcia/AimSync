using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace AimSync.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox)
            listBox.ScrollIntoView(listBox.SelectedItem);

        PageHost.BeginAnimation(OpacityProperty, null);
        PageHost.Opacity = 0;
        var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        PageHost.BeginAnimation(OpacityProperty, fadeIn);
    }
}