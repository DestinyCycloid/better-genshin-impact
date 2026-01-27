using System;
using System.Windows;

namespace BetterGenshinImpact.View.Windows;

/// <summary>
/// 手柄调试窗口演示类
/// 用于在应用程序中打开调试窗口
/// </summary>
public static class GamepadDebugWindowDemo
{
    /// <summary>
    /// 打开手柄调试窗口
    /// </summary>
    public static void ShowDebugWindow()
    {
        var debugWindow = GamepadDebugWindow.Instance;
        if (!debugWindow.IsLoaded)
        {
            debugWindow.Show();
        }
        else
        {
            debugWindow.Activate();
        }
    }
    
    /// <summary>
    /// 关闭手柄调试窗口
    /// </summary>
    public static void CloseDebugWindow()
    {
        var debugWindow = GamepadDebugWindow.Instance;
        if (debugWindow.IsLoaded)
        {
            debugWindow.Close();
        }
    }
}
