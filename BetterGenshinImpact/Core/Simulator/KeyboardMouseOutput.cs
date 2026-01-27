using System;
using BetterGenshinImpact.Core.Simulator.Extensions;
using Fischless.WindowsInput;
using Microsoft.Extensions.Logging;
using Vanara.PInvoke;

namespace BetterGenshinImpact.Core.Simulator;

/// <summary>
/// 键盘鼠标输出适配器，将统一的输入接口转换为键鼠操作
/// </summary>
public class KeyboardMouseOutput : IInputOutput
{
    private static readonly ILogger<KeyboardMouseOutput> _logger = App.GetLogger<KeyboardMouseOutput>();
    private readonly InputSimulator _simulator;
    
    // 当前按下的 WASD 键状态
    private bool _wPressed;
    private bool _aPressed;
    private bool _sPressed;
    private bool _dPressed;
    
    // 鼠标移动灵敏度（可配置）
    private const float MouseSensitivity = 0.01f;
    
    public InputMode Mode => InputMode.KeyboardMouse;
    
    public KeyboardMouseOutput()
    {
        _simulator = Simulation.SendInput;
    }
    
    /// <summary>
    /// 初始化键鼠输出（无需特殊初始化）
    /// </summary>
    public bool Initialize()
    {
        _logger.LogDebug("键鼠模式初始化（无需特殊操作）");
        // 键鼠模式无需特殊初始化
        return true;
    }
    
    /// <summary>
    /// 模拟游戏动作
    /// </summary>
    public void SimulateAction(GIActions action, KeyType type = KeyType.KeyPress)
    {
        try
        {
            _logger.LogTrace("执行键鼠动作: {Action} ({Type})", action, type);
            // 委托给现有的 InputSimulatorExtension
            _simulator.SimulateAction(action, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行键鼠动作失败: {Action}", action);
        }
    }
    
    /// <summary>
    /// 设置左摇杆位置（转换为 WASD 按键）
    /// </summary>
    public void SetLeftStick(short x, short y)
    {
        // 将摇杆坐标转换为 WASD 按键状态
        // 使用阈值来判断是否按下按键（避免微小抖动）
        const short threshold = 16384; // 约 50% 的摇杆推动
        
        bool w = y > threshold;
        bool s = y < -threshold;
        bool a = x < -threshold;
        bool d = x > threshold;
        
        // 更新按键状态
        UpdateWASDKeys(w, a, s, d);
    }
    
    /// <summary>
    /// 更新 WASD 按键状态
    /// </summary>
    private void UpdateWASDKeys(bool w, bool a, bool s, bool d)
    {
        // W 键
        if (w != _wPressed)
        {
            if (w)
                _simulator.Keyboard.KeyDown(User32.VK.VK_W);
            else
                _simulator.Keyboard.KeyUp(User32.VK.VK_W);
            _wPressed = w;
        }
        
        // A 键
        if (a != _aPressed)
        {
            if (a)
                _simulator.Keyboard.KeyDown(User32.VK.VK_A);
            else
                _simulator.Keyboard.KeyUp(User32.VK.VK_A);
            _aPressed = a;
        }
        
        // S 键
        if (s != _sPressed)
        {
            if (s)
                _simulator.Keyboard.KeyDown(User32.VK.VK_S);
            else
                _simulator.Keyboard.KeyUp(User32.VK.VK_S);
            _sPressed = s;
        }
        
        // D 键
        if (d != _dPressed)
        {
            if (d)
                _simulator.Keyboard.KeyDown(User32.VK.VK_D);
            else
                _simulator.Keyboard.KeyUp(User32.VK.VK_D);
            _dPressed = d;
        }
    }
    
    /// <summary>
    /// 设置右摇杆位置（转换为鼠标移动）
    /// </summary>
    public void SetRightStick(short x, short y)
    {
        // 将摇杆坐标转换为鼠标相对移动量
        // 摇杆范围是 -32768 到 32767，需要缩放到合适的鼠标移动量
        int mouseX = (int)(x * MouseSensitivity);
        int mouseY = (int)(y * MouseSensitivity);
        
        // 只有在移动量不为零时才执行鼠标移动
        if (mouseX != 0 || mouseY != 0)
        {
            _simulator.Mouse.MoveMouseBy(mouseX, mouseY);
        }
    }
    
    /// <summary>
    /// 设置左扳机压力（映射到对应按键）
    /// </summary>
    public void SetLeftTrigger(byte value)
    {
        // 左扳机通常用于瞄准，映射到右键
        // 使用阈值判断是否按下
        const byte threshold = 128;
        
        if (value > threshold)
        {
            _simulator.Mouse.RightButtonDown();
        }
        else
        {
            _simulator.Mouse.RightButtonUp();
        }
    }
    
    /// <summary>
    /// 设置右扳机压力（映射到对应按键）
    /// </summary>
    public void SetRightTrigger(byte value)
    {
        // 右扳机通常用于攻击，映射到左键
        // 使用阈值判断是否按下
        const byte threshold = 128;
        
        if (value > threshold)
        {
            _simulator.Mouse.LeftButtonDown();
        }
        else
        {
            _simulator.Mouse.LeftButtonUp();
        }
    }
    
    /// <summary>
    /// 释放所有按键
    /// </summary>
    public void ReleaseAll()
    {
        // 调用 Simulation 的内部方法释放所有键盘按键（避免循环调用）
        Simulation.ReleaseAllKeyboardKeys();
        
        // 重置内部状态
        _wPressed = false;
        _aPressed = false;
        _sPressed = false;
        _dPressed = false;
    }
    
    /// <summary>
    /// 释放资源（键鼠模式无需特殊清理）
    /// </summary>
    public void Dispose()
    {
        try
        {
            _logger.LogDebug("释放键鼠模式资源");
            // 确保释放所有按键
            ReleaseAll();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放键鼠模式资源时发生错误");
        }
    }
}
