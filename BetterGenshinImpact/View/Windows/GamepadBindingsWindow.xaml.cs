using BetterGenshinImpact.ViewModel.Windows;
using System.Windows;

namespace BetterGenshinImpact.View.Windows;

public partial class GamepadBindingsWindow
{
    private static GamepadBindingsWindow? _instance;

    public static GamepadBindingsWindow Instance
    {
        get
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new GamepadBindingsWindow();
            }
            return _instance;
        }
    }

    private GamepadBindingsViewModel ViewModel { get; }

    private GamepadBindingsWindow()
    {
        DataContext = ViewModel = new GamepadBindingsViewModel();
        InitializeComponent();
    }

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
        _instance = null;
    }
}
