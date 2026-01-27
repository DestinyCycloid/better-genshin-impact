using System;
using System.Windows;

namespace BetterGenshinImpact.View.Windows;

/// <summary>
/// 手柄调试窗口
/// </summary>
public partial class GamepadDebugWindow : Window
{
    private static GamepadDebugWindow? _instance;

    public static GamepadDebugWindow Instance
    {
        get
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new GamepadDebugWindow();
            }
            return _instance;
        }
    }

    private GamepadDebugWindow()
    {
        InitializeComponent();
        
        // 窗口关闭时停止更新
        Closed += OnWindowClosed;
    }
    
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // 通知 ViewModel 停止更新
        if (DataContext is ViewModel.Windows.GamepadDebugViewModel viewModel)
        {
            viewModel.StopUpdating();
        }
        _instance = null;
    }
}
