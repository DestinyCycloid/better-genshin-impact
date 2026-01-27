using Fischless.WindowsInput;
using System;
using BetterGenshinImpact.GameTask.Common;
using Microsoft.Extensions.Logging;
using Vanara.PInvoke;
using BetterGenshinImpact.Core.Simulator.Extensions;

namespace BetterGenshinImpact.Core.Simulator;

public class Simulation
{
    public static InputSimulator SendInput { get; } = new();

    public static MouseEventSimulator MouseEvent { get; } = new();

    /// <summary>
    /// 输入路由器实例，用于管理不同的输入模式
    /// </summary>
    private static InputRouter Router => InputRouter.Instance;

    public static PostMessageSimulator PostMessage(IntPtr hWnd)
    {
        return new PostMessageSimulator(hWnd);
    }

    /// <summary>
    /// 切换输入模式
    /// </summary>
    /// <param name="mode">目标输入模式</param>
    /// <returns>切换是否成功</returns>
    public static bool SwitchInputMode(InputMode mode)
    {
        return Router.SwitchMode(mode);
    }

    /// <summary>
    /// 获取当前的输入模式
    /// </summary>
    public static InputMode CurrentInputMode => Router.CurrentMode;

    /// <summary>
    /// 模拟游戏动作（通过输入路由器）
    /// </summary>
    /// <param name="action">游戏动作</param>
    /// <param name="type">按键类型</param>
    public static void SimulateAction(GIActions action, KeyType type = KeyType.KeyPress)
    {
        TaskControl.Logger.LogInformation("🎯 Simulation.SimulateAction: Action={Action}, Type={Type}, CurrentMode={Mode}", 
            action, type, Router.CurrentMode);
        Router.GetOutput().SimulateAction(action, type);
    }

    /// <summary>
    /// 设置左摇杆位置（用于移动）
    /// </summary>
    /// <param name="x">X轴坐标 (-32768 到 32767)</param>
    /// <param name="y">Y轴坐标 (-32768 到 32767)</param>
    public static void SetLeftStick(short x, short y)
    {
        Router.GetOutput().SetLeftStick(x, y);
    }

    /// <summary>
    /// 设置右摇杆位置（用于镜头）
    /// </summary>
    /// <param name="x">X轴坐标 (-32768 到 32767)</param>
    /// <param name="y">Y轴坐标 (-32768 到 32767)</param>
    public static void SetRightStick(short x, short y)
    {
        Router.GetOutput().SetRightStick(x, y);
    }

    /// <summary>
    /// 设置左扳机压力
    /// </summary>
    /// <param name="value">压力值 (0-255)</param>
    public static void SetLeftTrigger(byte value)
    {
        Router.GetOutput().SetLeftTrigger(value);
    }

    /// <summary>
    /// 设置右扳机压力
    /// </summary>
    /// <param name="value">压力值 (0-255)</param>
    public static void SetRightTrigger(byte value)
    {
        Router.GetOutput().SetRightTrigger(value);
    }

    /// <summary>
    /// 释放所有按键/按钮（通过输入路由器）
    /// </summary>
    public static void ReleaseAllKey()
    {
        Router.GetOutput().ReleaseAll();
    }

    /// <summary>
    /// 内部方法：直接释放所有键盘按键（用于 KeyboardMouseOutput，避免循环调用）
    /// </summary>
    internal static void ReleaseAllKeyboardKeys()
    {
        foreach (User32.VK key in Enum.GetValues(typeof(User32.VK)))
        {
            // 检查键是否被按下
            if (IsKeyDown(key))
            {
                TaskControl.Logger.LogDebug($"解除{key}的按下状态.");
                SendInput.Keyboard.KeyUp(key);
            }
        }
    }

    public static bool IsKeyDown(User32.VK key)
    {
        // 获取按键状态
        var state = User32.GetAsyncKeyState((int)key);

        // 检查高位是否为 1（表示按键被按下）
        return (state & 0x8000) != 0;
    }
}