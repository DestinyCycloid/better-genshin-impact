using System;
using System.Threading;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Simulator.Extensions;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Helpers;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.Core.Simulator;

/// <summary>
/// XInput 手柄输出适配器，通过 ViGEm 虚拟手柄驱动发送手柄输入
/// </summary>
public class XInputOutput : IInputOutput
{
    private readonly ILogger<XInputOutput> _logger = App.GetLogger<XInputOutput>();
    private readonly GamepadBindingsConfig _bindings;
    
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;
    private bool _isInitialized;
    private int _reconnectAttempts;
    private const int MaxReconnectAttempts = 3;
    
    // 跟踪左摇杆的状态（用于判断移动键是否按下）
    private short _leftStickX = 0;
    private short _leftStickY = 0;
    
    public InputMode Mode => InputMode.XInput;
    
    public XInputOutput()
    {
        // 从全局配置获取手柄绑定配置
        _bindings = TaskContext.Instance().Config.GamepadBindingsConfig;
    }
    
    /// <summary>
    /// 初始化虚拟手柄设备
    /// </summary>
    /// <returns>初始化是否成功</returns>
    public bool Initialize()
    {
        if (_isInitialized)
        {
            _logger.LogDebug("虚拟手柄已经初始化，跳过重复初始化");
            return true;
        }
        
        try
        {
            _logger.LogInformation("正在初始化虚拟 XInput 手柄...");
            
            // 创建 ViGEm 客户端
            _client = new ViGEmClient();
            _logger.LogDebug("ViGEm 客户端创建成功");
            
            // 创建虚拟 Xbox 360 手柄
            _controller = _client.CreateXbox360Controller();
            _logger.LogDebug("虚拟 Xbox 360 手柄创建成功");
            
            // 连接手柄
            _controller.Connect();
            _logger.LogDebug("虚拟手柄连接成功");
            
            _isInitialized = true;
            _reconnectAttempts = 0;
            
            _logger.LogInformation("✓ 虚拟 XInput 手柄初始化成功");
            
            // 显示成功提示
            UIDispatcherHelper.Invoke(() =>
            {
                Toast.Success("XInput 手柄模式已启用");
            });
            
            return true;
        }
        catch (VigemBusNotFoundException ex)
        {
            _logger.LogWarning(ex, "ViGEmBus 驱动未安装，无法使用 XInput 模式");
            
            // 显示友好的错误提示
            UIDispatcherHelper.Invoke(() =>
            {
                Toast.Warning("ViGEmBus 驱动未安装\n请访问 https://github.com/nefarius/ViGEmBus/releases 下载并安装驱动");
            });
            
            return false;
        }
        catch (VigemAlreadyConnectedException ex)
        {
            _logger.LogWarning(ex, "虚拟手柄已经连接，可能是之前的实例未正确释放");
            _isInitialized = true;
            
            UIDispatcherHelper.Invoke(() =>
            {
                Toast.Information("虚拟手柄已连接");
            });
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化虚拟手柄失败：{Message}", ex.Message);
            
            UIDispatcherHelper.Invoke(() =>
            {
                Toast.Error($"初始化虚拟手柄失败：{ex.Message}");
            });
            
            return false;
        }
    }
    
    /// <summary>
    /// 模拟游戏动作
    /// </summary>
    public void SimulateAction(GIActions action, KeyType type = KeyType.KeyPress)
    {
        _logger.LogInformation(">>> SimulateAction 被调用: Action={Action}, Type={Type}", action, type);
        
        if (!EnsureConnected())
        {
            _logger.LogWarning("❌ 手柄未连接，无法执行动作: {Action}", action);
            return;
        }
        
        // 特殊处理：移动动作（使用左摇杆）
        if (action == GIActions.MoveForward || action == GIActions.MoveBackward || 
            action == GIActions.MoveLeft || action == GIActions.MoveRight)
        {
            _logger.LogInformation("🎮 执行移动动作: {Action} ({Type})", action, type);
            
            try
            {
                // 摇杆最大值为 32767
                // 手柄模式下，摇杆推到最大会奔跑，推到中等会步行
                // 默认使用步行速度（约50%强度），与键鼠模式的默认步行行为一致
                const short walkValue = 16000;  // 约50%强度，步行速度
                const short maxValue = 32767;   // 100%强度，奔跑速度
                
                // 使用步行速度作为默认值
                short moveValue = walkValue;
                
                switch (type)
                {
                    case KeyType.KeyDown:
                        // 按下：设置摇杆值
                        if (action == GIActions.MoveForward)
                        {
                            _logger.LogInformation("  → 左摇杆向上推 (Y={Value}, 步行模式)", moveValue);
                            _leftStickY = moveValue;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbY, moveValue);
                        }
                        else if (action == GIActions.MoveBackward)
                        {
                            _logger.LogInformation("  → 左摇杆向下推 (Y={Value}, 步行模式)", -moveValue);
                            _leftStickY = (short)-moveValue;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbY, (short)-moveValue);
                        }
                        else if (action == GIActions.MoveLeft)
                        {
                            _logger.LogInformation("  → 左摇杆向左推 (X={Value}, 步行模式)", -moveValue);
                            _leftStickX = (short)-moveValue;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbX, (short)-moveValue);
                        }
                        else if (action == GIActions.MoveRight)
                        {
                            _logger.LogInformation("  → 左摇杆向右推 (X={Value}, 步行模式)", moveValue);
                            _leftStickX = moveValue;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbX, moveValue);
                        }
                        _controller!.SubmitReport();
                        _logger.LogInformation("  → 已提交报告");
                        break;
                        
                    case KeyType.KeyUp:
                        // 释放：重置摇杆为0
                        if (action == GIActions.MoveForward || action == GIActions.MoveBackward)
                        {
                            _logger.LogInformation("  → 释放左摇杆Y轴 (Y=0)");
                            _leftStickY = 0;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
                        }
                        else if (action == GIActions.MoveLeft || action == GIActions.MoveRight)
                        {
                            _logger.LogInformation("  → 释放左摇杆X轴 (X=0)");
                            _leftStickX = 0;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
                        }
                        _controller!.SubmitReport();
                        _logger.LogInformation("  → 已提交报告");
                        break;
                        
                    case KeyType.KeyPress:
                        // 按下并释放（短暂移动）
                        _logger.LogInformation("  → 执行短暂移动");
                        
                        // 按下
                        if (action == GIActions.MoveForward)
                        {
                            _leftStickY = moveValue;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbY, moveValue);
                        }
                        else if (action == GIActions.MoveBackward)
                        {
                            _leftStickY = (short)-moveValue;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbY, (short)-moveValue);
                        }
                        else if (action == GIActions.MoveLeft)
                        {
                            _leftStickX = (short)-moveValue;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbX, (short)-moveValue);
                        }
                        else if (action == GIActions.MoveRight)
                        {
                            _leftStickX = moveValue;
                            _controller!.SetAxisValue(Xbox360Axis.LeftThumbX, moveValue);
                        }
                        _controller!.SubmitReport();
                        Thread.Sleep(50); // 保持50ms
                        
                        // 释放
                        if (action == GIActions.MoveForward || action == GIActions.MoveBackward)
                        {
                            _leftStickY = 0;
                            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
                        }
                        else if (action == GIActions.MoveLeft || action == GIActions.MoveRight)
                        {
                            _leftStickX = 0;
                            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
                        }
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 已提交报告");
                        break;
                }
                
                _logger.LogInformation("✓ 移动动作执行完成");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 执行移动动作失败");
                return;
            }
        }
        
        // 特殊处理：打开地图 (LB + 右摇杆向下)
        if (action == GIActions.OpenMap)
        {
            _logger.LogInformation("🎮 执行打开地图动作: LB + 右摇杆向下");
            
            try
            {
                // 1. 按住 LB（至少保持1秒）
                _logger.LogInformation("  → 按下 LB 并保持");
                _controller!.SetButtonState(Xbox360Button.LeftShoulder, true);
                _controller.SubmitReport();
                Thread.Sleep(1000); // 保持1秒
                
                // 2. 右摇杆向下拉到最大值 (Y轴负值，最大值为 -32768)
                _logger.LogInformation("  → 右摇杆向下拉到最大");
                _controller.SetAxisValue(Xbox360Axis.RightThumbY, -32768);
                _controller.SubmitReport();
                Thread.Sleep(300); // 保持300ms
                
                // 3. 释放右摇杆
                _logger.LogInformation("  → 释放右摇杆");
                _controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);
                _controller.SubmitReport();
                Thread.Sleep(100); // 等待100ms
                
                // 4. 释放 LB
                _logger.LogInformation("  → 释放 LB");
                _controller.SetButtonState(Xbox360Button.LeftShoulder, false);
                _controller.SubmitReport();
                
                _logger.LogInformation("✓ 打开地图动作执行完成");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 执行打开地图动作失败");
                return;
            }
        }
        
        _logger.LogInformation("✓ 手柄已连接，正在获取按键映射...");
        
        // 从配置中获取对应的手柄按钮映射
        var mapping = _bindings.GetButtonMapping(action);
        
        if (mapping == null)
        {
            _logger.LogWarning("❌ 动作 {Action} 没有配置手柄按钮映射", action);
            return;
        }
        
        _logger.LogInformation("✓ 获取到映射: IsTrigger={IsTrigger}, IsCombo={IsCombo}, Button={Button}", 
            mapping.IsTrigger, mapping.IsCombo, mapping.Button);
        
        try
        {
            if (mapping.IsCombo)
            {
                // 组合键映射
                var comboName = $"{GetButtonName(mapping.ModifierButton)}+{GetButtonName(mapping.MainButton)}";
                _logger.LogInformation("🎮 执行组合键动作: {Action} -> {Combo} ({Type})", action, comboName, type);
                
                switch (type)
                {
                    case KeyType.KeyPress:
                        // 1. 按住修饰键（LB）
                        _logger.LogInformation("  → 按下修饰键 {Modifier}", mapping.ModifierButton);
                        _controller!.SetButtonState(mapping.ModifierButton, true);
                        _controller.SubmitReport();
                        Thread.Sleep(30); // 短暂延迟确保修饰键生效
                        
                        // 2. 按下主键（Y/B）
                        _logger.LogInformation("  → 按下主键 {Main}", mapping.MainButton);
                        _controller.SetButtonState(mapping.MainButton, true);
                        _controller.SubmitReport();
                        Thread.Sleep(50); // 保持按下状态
                        
                        // 3. 释放主键
                        _logger.LogInformation("  → 释放主键 {Main}", mapping.MainButton);
                        _controller.SetButtonState(mapping.MainButton, false);
                        _controller.SubmitReport();
                        Thread.Sleep(30);
                        
                        // 4. 释放修饰键
                        _logger.LogInformation("  → 释放修饰键 {Modifier}", mapping.ModifierButton);
                        _controller.SetButtonState(mapping.ModifierButton, false);
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 组合键执行完成");
                        break;
                        
                    case KeyType.KeyDown:
                        // 按下组合键（先按修饰键，再按主键）
                        _logger.LogInformation("  → 按下修饰键 {Modifier}", mapping.ModifierButton);
                        _controller!.SetButtonState(mapping.ModifierButton, true);
                        _controller.SubmitReport();
                        Thread.Sleep(30);
                        
                        _logger.LogInformation("  → 按下主键 {Main}", mapping.MainButton);
                        _controller.SetButtonState(mapping.MainButton, true);
                        _controller.SubmitReport();
                        break;
                        
                    case KeyType.KeyUp:
                        // 释放组合键（先释放主键，再释放修饰键）
                        _logger.LogInformation("  → 释放主键 {Main}", mapping.MainButton);
                        _controller!.SetButtonState(mapping.MainButton, false);
                        _controller.SubmitReport();
                        Thread.Sleep(30);
                        
                        _logger.LogInformation("  → 释放修饰键 {Modifier}", mapping.ModifierButton);
                        _controller.SetButtonState(mapping.ModifierButton, false);
                        _controller.SubmitReport();
                        break;
                }
                
                _logger.LogInformation("✓ 组合键动作执行完成");
            }
            else if (mapping.IsTrigger)
            {
                // 扳机映射
                var triggerName = mapping.IsLeftTrigger ? "LT (左扳机)" : "RT (右扳机)";
                _logger.LogInformation("🎮 执行扳机动作: {Action} -> {Trigger} ({Type})", action, triggerName, type);
                
                switch (type)
                {
                    case KeyType.KeyPress:
                        // 按下并释放扳机
                        _logger.LogInformation("  → 按下扳机 (255)");
                        if (mapping.IsLeftTrigger)
                        {
                            _controller!.SetSliderValue(Xbox360Slider.LeftTrigger, 255);
                        }
                        else
                        {
                            _controller!.SetSliderValue(Xbox360Slider.RightTrigger, 255);
                        }
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 已提交报告，等待 50ms");
                        Thread.Sleep(50);
                        
                        _logger.LogInformation("  → 释放扳机 (0)");
                        if (mapping.IsLeftTrigger)
                        {
                            _controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
                        }
                        else
                        {
                            _controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
                        }
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 已提交报告");
                        break;
                        
                    case KeyType.KeyDown:
                        // 按下扳机
                        _logger.LogInformation("  → 按下扳机 (255)");
                        if (mapping.IsLeftTrigger)
                        {
                            _controller!.SetSliderValue(Xbox360Slider.LeftTrigger, 255);
                        }
                        else
                        {
                            _controller!.SetSliderValue(Xbox360Slider.RightTrigger, 255);
                        }
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 已提交报告");
                        break;
                        
                    case KeyType.KeyUp:
                        // 释放扳机
                        _logger.LogInformation("  → 释放扳机 (0)");
                        if (mapping.IsLeftTrigger)
                        {
                            _controller!.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
                        }
                        else
                        {
                            _controller!.SetSliderValue(Xbox360Slider.RightTrigger, 0);
                        }
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 已提交报告");
                        break;
                }
                
                _logger.LogInformation("✓ 扳机动作执行完成");
            }
            else
            {
                // 按钮映射
                var button = mapping.Button;
                _logger.LogInformation("🎮 执行按钮动作: {Action} -> {Button} ({Type})", action, button, type);
                
                switch (type)
                {
                    case KeyType.KeyPress:
                        // 按下并释放
                        _logger.LogInformation("  → 按下按钮 {Button}", button);
                        _controller!.SetButtonState(button, true);
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 已提交报告，等待 50ms");
                        Thread.Sleep(50); // 短暂延迟模拟真实按键
                        
                        _logger.LogInformation("  → 释放按钮 {Button}", button);
                        _controller.SetButtonState(button, false);
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 已提交报告");
                        break;
                        
                    case KeyType.KeyDown:
                        // 仅按下
                        _logger.LogInformation("  → 按下按钮 {Button}", button);
                        _controller!.SetButtonState(button, true);
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 已提交报告");
                        break;
                        
                    case KeyType.KeyUp:
                        // 仅释放
                        _logger.LogInformation("  → 释放按钮 {Button}", button);
                        _controller!.SetButtonState(button, false);
                        _controller.SubmitReport();
                        _logger.LogInformation("  → 已提交报告");
                        break;
                }
                
                _logger.LogInformation("✓ 按钮动作执行完成");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 执行手柄动作失败: {Action}", action);
            
            // 尝试恢复连接
            if (!EnsureConnected())
            {
                UIDispatcherHelper.Invoke(() =>
                {
                    Toast.Warning("手柄连接丢失，正在尝试恢复...");
                });
            }
        }
    }
    
    /// <summary>
    /// 获取按钮名称（用于日志）
    /// </summary>
    private static string GetButtonName(Xbox360Button button)
    {
        if (button == Xbox360Button.A) return "A";
        if (button == Xbox360Button.B) return "B";
        if (button == Xbox360Button.X) return "X";
        if (button == Xbox360Button.Y) return "Y";
        if (button == Xbox360Button.LeftShoulder) return "LB";
        if (button == Xbox360Button.RightShoulder) return "RB";
        if (button == Xbox360Button.Up) return "方向键上";
        if (button == Xbox360Button.Down) return "方向键下";
        if (button == Xbox360Button.Left) return "方向键左";
        if (button == Xbox360Button.Right) return "方向键右";
        return button.ToString();
    }
    
    /// <summary>
    /// 设置左摇杆位置（用于移动）
    /// </summary>
    public void SetLeftStick(short x, short y)
    {
        if (!EnsureConnected())
        {
            return;
        }
        
        try
        {
            _logger.LogTrace("设置左摇杆: X={X}, Y={Y}", x, y);
            
            _controller!.SetAxisValue(Xbox360Axis.LeftThumbX, x);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, y);
            _controller.SubmitReport();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置左摇杆位置失败: X={X}, Y={Y}", x, y);
            EnsureConnected(); // 尝试恢复连接
        }
    }
    
    /// <summary>
    /// 设置右摇杆位置（用于镜头）
    /// </summary>
    public void SetRightStick(short x, short y)
    {
        if (!EnsureConnected())
        {
            return;
        }
        
        try
        {
            // 计算百分比（用于日志显示）
            float percentX = (x / 32767.0f) * 100.0f;
            float percentY = (y / 32767.0f) * 100.0f;
            
            _logger.LogInformation("【SetRightStick】右摇杆: ({X}, {Y}) = ({PercentX:F1}%, {PercentY:F1}%)", 
                x, y, percentX, percentY);
            
            _controller!.SetAxisValue(Xbox360Axis.RightThumbX, x);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY, y);
            _controller.SubmitReport();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置右摇杆位置失败: X={X}, Y={Y}", x, y);
            EnsureConnected(); // 尝试恢复连接
        }
    }
    
    /// <summary>
    /// 设置左扳机压力
    /// </summary>
    public void SetLeftTrigger(byte value)
    {
        if (!EnsureConnected())
        {
            return;
        }
        
        try
        {
            _logger.LogTrace("设置左扳机: {Value}", value);
            
            _controller!.SetSliderValue(Xbox360Slider.LeftTrigger, value);
            _controller.SubmitReport();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置左扳机压力失败: {Value}", value);
            EnsureConnected(); // 尝试恢复连接
        }
    }
    
    /// <summary>
    /// 设置右扳机压力
    /// </summary>
    public void SetRightTrigger(byte value)
    {
        if (!EnsureConnected())
        {
            return;
        }
        
        try
        {
            _logger.LogTrace("设置右扳机: {Value}", value);
            
            _controller!.SetSliderValue(Xbox360Slider.RightTrigger, value);
            _controller.SubmitReport();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置右扳机压力失败: {Value}", value);
            EnsureConnected(); // 尝试恢复连接
        }
    }
    
    /// <summary>
    /// 按下指定的手柄按钮
    /// </summary>
    public void SetButtonDown(Xbox360Button button)
    {
        if (!EnsureConnected())
        {
            return;
        }
        
        try
        {
            _logger.LogTrace("按下按钮: {Button}", button);
            
            _controller!.SetButtonState(button, true);
            _controller.SubmitReport();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "按下按钮失败: {Button}", button);
            EnsureConnected(); // 尝试恢复连接
        }
    }
    
    /// <summary>
    /// 松开指定的手柄按钮
    /// </summary>
    public void SetButtonUp(Xbox360Button button)
    {
        if (!EnsureConnected())
        {
            return;
        }
        
        try
        {
            _logger.LogTrace("松开按钮: {Button}", button);
            
            _controller!.SetButtonState(button, false);
            _controller.SubmitReport();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "松开按钮失败: {Button}", button);
            EnsureConnected(); // 尝试恢复连接
        }
    }
    
    /// <summary>
    /// 释放所有按键/按钮，重置手柄状态
    /// </summary>
    public void ReleaseAll()
    {
        if (!_isInitialized || _controller == null)
        {
            return;
        }
        
        try
        {
            // 重置所有按钮状态（Xbox360Button 是结构体，需要手动列出所有按钮）
            var buttons = new[]
            {
                Xbox360Button.A,
                Xbox360Button.B,
                Xbox360Button.X,
                Xbox360Button.Y,
                Xbox360Button.LeftShoulder,
                Xbox360Button.RightShoulder,
                Xbox360Button.Back,
                Xbox360Button.Start,
                Xbox360Button.Guide,
                Xbox360Button.LeftThumb,
                Xbox360Button.RightThumb,
                Xbox360Button.Up,
                Xbox360Button.Down,
                Xbox360Button.Left,
                Xbox360Button.Right
            };
            
            foreach (var button in buttons)
            {
                _controller.SetButtonState(button, false);
            }
            
            // 重置所有摇杆到中心位置
            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
            _controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);
            
            // 重置所有扳机
            _controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
            _controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
            
            _controller.SubmitReport();
            
            _logger.LogDebug("已重置所有手柄状态");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置手柄状态失败");
        }
    }
    
    /// <summary>
    /// 确保手柄连接，如果断开则尝试重连
    /// </summary>
    /// <returns>手柄是否已连接</returns>
    private bool EnsureConnected()
    {
        if (!_isInitialized || _controller == null || _client == null)
        {
            _logger.LogDebug("手柄未初始化");
            return false;
        }
        
        // 尝试提交一个空报告来检测连接状态
        try
        {
            // 如果手柄已连接，这个操作应该成功
            _controller.SubmitReport();
            return true;
        }
        catch (Exception ex)
        {
            // 连接丢失，尝试重连
            _logger.LogWarning(ex, "检测到手柄连接丢失，尝试重连... (尝试 {Attempt}/{Max})", 
                _reconnectAttempts + 1, MaxReconnectAttempts);
            
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                _logger.LogError("重连失败次数已达上限 ({Max})，放弃重连", MaxReconnectAttempts);
                
                UIDispatcherHelper.Invoke(() =>
                {
                    Toast.Error($"虚拟手柄连接丢失且无法恢复\n已尝试重连 {MaxReconnectAttempts} 次");
                });
                
                return false;
            }
            
            _reconnectAttempts++;
            
            try
            {
                // 尝试重新连接
                _logger.LogDebug("尝试重新连接虚拟手柄...");
                _controller.Connect();
                
                // 验证连接
                _controller.SubmitReport();
                
                _reconnectAttempts = 0;
                _logger.LogInformation("✓ 手柄重连成功");
                
                UIDispatcherHelper.Invoke(() =>
                {
                    Toast.Success("虚拟手柄已重新连接");
                });
                
                return true;
            }
            catch (Exception reconnectEx)
            {
                _logger.LogError(reconnectEx, "手柄重连失败 (尝试 {Attempt}/{Max}): {Message}", 
                    _reconnectAttempts, MaxReconnectAttempts, reconnectEx.Message);
                
                // 短暂延迟后再试
                Thread.Sleep(100);
                
                // 如果这是最后一次尝试，显示错误提示
                if (_reconnectAttempts >= MaxReconnectAttempts)
                {
                    UIDispatcherHelper.Invoke(() =>
                    {
                        Toast.Error("虚拟手柄重连失败\n请检查 ViGEmBus 驱动是否正常运行");
                    });
                }
                
                return false;
            }
        }
    }
    
    /// <summary>
    /// 使用左摇杆移动光标（模拟鼠标移动）
    /// 用于地图传送等需要移动光标的场景
    /// </summary>
    /// <param name="deltaX">X轴移动距离（像素）</param>
    /// <param name="deltaY">Y轴移动距离（像素）</param>
    /// <param name="durationMs">移动持续时间（毫秒）</param>
    public void MoveLeftStickForCursor(int deltaX, int deltaY, int durationMs = 500)
    {
        if (!EnsureConnected())
        {
            return;
        }
        
        try
        {
            _logger.LogInformation("🎮 使用左摇杆移动光标: ΔX={DeltaX}, ΔY={DeltaY}, 持续时间={Duration}ms", 
                deltaX, deltaY, durationMs);
            
            // 计算摇杆方向和强度
            // 摇杆值范围: -32768 到 32767
            // 根据移动距离计算摇杆强度（距离越大，摇杆推得越远）
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (distance < 1)
            {
                _logger.LogDebug("移动距离太小，跳过");
                return;
            }
            
            // 归一化方向
            double dirX = deltaX / distance;
            double dirY = deltaY / distance;
            
            // 计算摇杆强度（根据距离动态调整，最大32767）
            // 距离越大，摇杆推得越远，移动越快
            double strength = Math.Min(distance * 100, 32767); // 100是调整系数
            
            // 计算摇杆坐标（注意Y轴方向相反）
            short stickX = (short)(dirX * strength);
            short stickY = (short)(-dirY * strength); // Y轴反向
            
            _logger.LogInformation("  → 摇杆方向: ({DirX:F2}, {DirY:F2}), 强度: {Strength:F0}", 
                dirX, dirY, strength);
            _logger.LogInformation("  → 摇杆坐标: X={StickX}, Y={StickY}", stickX, stickY);
            
            // 推动摇杆
            _controller!.SetAxisValue(Xbox360Axis.LeftThumbX, stickX);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, stickY);
            _controller.SubmitReport();
            
            // 保持一段时间
            Thread.Sleep(durationMs);
            
            // 释放摇杆（回到中心位置）
            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
            _controller.SubmitReport();
            
            _logger.LogInformation("✓ 左摇杆移动光标完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 使用左摇杆移动光标失败");
            EnsureConnected(); // 尝试恢复连接
        }
    }
    
    /// <summary>
    /// 检查前进键（左摇杆Y轴）是否按下
    /// </summary>
    /// <returns>如果左摇杆Y轴有正值（向前推），返回true</returns>
    public bool IsMoveForwardPressed()
    {
        return _leftStickY > 0;
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_isInitialized)
        {
            return;
        }
        
        try
        {
            _logger.LogInformation("正在释放虚拟手柄资源...");
            
            // 重置手柄状态
            ReleaseAll();
            
            // 断开手柄连接
            if (_controller != null)
            {
                try
                {
                    _controller.Disconnect();
                    _logger.LogDebug("虚拟手柄已断开连接");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "断开虚拟手柄连接时发生错误");
                }
            }
            
            // 释放客户端
            if (_client != null)
            {
                try
                {
                    _client.Dispose();
                    _logger.LogDebug("ViGEm 客户端已释放");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "释放 ViGEm 客户端时发生错误");
                }
            }
            
            _controller = null;
            _client = null;
            _isInitialized = false;
            _reconnectAttempts = 0;
            
            _logger.LogInformation("✓ 虚拟手柄资源已释放");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放虚拟手柄资源时发生错误: {Message}", ex.Message);
        }
    }
}
