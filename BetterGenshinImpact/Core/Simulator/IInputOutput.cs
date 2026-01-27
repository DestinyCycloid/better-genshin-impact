using System;
using BetterGenshinImpact.Core.Simulator.Extensions;

namespace BetterGenshinImpact.Core.Simulator;

/// <summary>
/// 输入输出接口，定义统一的输入操作抽象
/// </summary>
public interface IInputOutput : IDisposable
{
    /// <summary>
    /// 初始化输入输出设备
    /// </summary>
    /// <returns>初始化是否成功</returns>
    bool Initialize();

    /// <summary>
    /// 模拟游戏动作
    /// </summary>
    /// <param name="action">游戏动作</param>
    /// <param name="type">按键类型</param>
    void SimulateAction(GIActions action, KeyType type = KeyType.KeyPress);

    /// <summary>
    /// 设置左摇杆位置 (用于移动)
    /// </summary>
    /// <param name="x">X轴坐标 (-32768 到 32767)</param>
    /// <param name="y">Y轴坐标 (-32768 到 32767)</param>
    void SetLeftStick(short x, short y);

    /// <summary>
    /// 设置右摇杆位置 (用于镜头)
    /// </summary>
    /// <param name="x">X轴坐标 (-32768 到 32767)</param>
    /// <param name="y">Y轴坐标 (-32768 到 32767)</param>
    void SetRightStick(short x, short y);

    /// <summary>
    /// 设置左扳机压力
    /// </summary>
    /// <param name="value">压力值 (0-255)</param>
    void SetLeftTrigger(byte value);

    /// <summary>
    /// 设置右扳机压力
    /// </summary>
    /// <param name="value">压力值 (0-255)</param>
    void SetRightTrigger(byte value);

    /// <summary>
    /// 释放所有按键/按钮
    /// </summary>
    void ReleaseAll();

    /// <summary>
    /// 获取当前输入模式
    /// </summary>
    InputMode Mode { get; }
}
