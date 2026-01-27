using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using BetterGenshinImpact.Core.Simulator;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace BetterGenshinImpact.ViewModel.Windows;

/// <summary>
/// 手柄调试窗口的 ViewModel
/// </summary>
public partial class GamepadDebugViewModel : ObservableObject
{
    private readonly ILogger<GamepadDebugViewModel> _logger = App.GetLogger<GamepadDebugViewModel>();
    private readonly DispatcherTimer _updateTimer;
    private readonly InputRouter _inputRouter;
    
    [ObservableProperty]
    private string _currentMode = "未知";
    
    [ObservableProperty]
    private ObservableCollection<ButtonStateItem> _buttonStates = new();
    
    [ObservableProperty]
    private short _leftStickX;
    
    [ObservableProperty]
    private short _leftStickY;
    
    [ObservableProperty]
    private short _rightStickX;
    
    [ObservableProperty]
    private short _rightStickY;
    
    [ObservableProperty]
    private byte _leftTrigger;
    
    [ObservableProperty]
    private byte _rightTrigger;
    
    public GamepadDebugViewModel()
    {
        _inputRouter = InputRouter.Instance;
        
        // 初始化按钮状态列表
        InitializeButtonStates();
        
        // 更新当前模式
        UpdateCurrentMode();
        
        // 创建定时器，每 100ms 更新一次状态
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _updateTimer.Tick += OnUpdateTimerTick;
        _updateTimer.Start();
        
        _logger.LogInformation("手柄调试窗口 ViewModel 已初始化");
    }
    
    /// <summary>
    /// 初始化按钮状态列表
    /// </summary>
    private void InitializeButtonStates()
    {
        // 添加所有 Xbox 360 按钮
        ButtonStates.Add(new ButtonStateItem { ButtonName = "A", Button = Xbox360Button.A });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "B", Button = Xbox360Button.B });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "X", Button = Xbox360Button.X });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "Y", Button = Xbox360Button.Y });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "LB", Button = Xbox360Button.LeftShoulder });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "RB", Button = Xbox360Button.RightShoulder });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "Back", Button = Xbox360Button.Back });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "Start", Button = Xbox360Button.Start });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "LS", Button = Xbox360Button.LeftThumb });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "RS", Button = Xbox360Button.RightThumb });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "Up", Button = Xbox360Button.Up });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "Down", Button = Xbox360Button.Down });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "Left", Button = Xbox360Button.Left });
        ButtonStates.Add(new ButtonStateItem { ButtonName = "Right", Button = Xbox360Button.Right });
    }
    
    /// <summary>
    /// 更新当前输入模式
    /// </summary>
    private void UpdateCurrentMode()
    {
        CurrentMode = _inputRouter.CurrentMode switch
        {
            InputMode.KeyboardMouse => "键盘鼠标",
            InputMode.XInput => "XInput 手柄",
            _ => "未知"
        };
    }
    
    /// <summary>
    /// 定时器更新事件
    /// </summary>
    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        try
        {
            // 更新模式（可能在运行时切换）
            UpdateCurrentMode();
            
            // 注意：实际的按钮状态、摇杆和扳机值需要从手柄读取
            // 由于 ViGEm 是输出设备，我们无法直接读取状态
            // 这里只是演示界面，实际使用时需要配合输入监控
            // 在实际应用中，可以通过监听 XInputOutput 的操作来更新这些值
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新手柄状态时发生错误");
        }
    }
    
    /// <summary>
    /// 停止更新
    /// </summary>
    public void StopUpdating()
    {
        _updateTimer?.Stop();
        _logger.LogInformation("手柄调试窗口已停止更新");
    }
    
    #region 测试命令
    
    [RelayCommand]
    private async Task TestButtonA()
    {
        await TestButton(Xbox360Button.A, "A");
    }
    
    [RelayCommand]
    private async Task TestButtonB()
    {
        await TestButton(Xbox360Button.B, "B");
    }
    
    [RelayCommand]
    private async Task TestButtonX()
    {
        await TestButton(Xbox360Button.X, "X");
    }
    
    [RelayCommand]
    private async Task TestButtonY()
    {
        await TestButton(Xbox360Button.Y, "Y");
    }
    
    /// <summary>
    /// 测试按钮按下
    /// </summary>
    private async Task TestButton(Xbox360Button button, string buttonName)
    {
        try
        {
            _logger.LogInformation($"测试按钮: {buttonName}");
            
            var output = _inputRouter.GetOutput();
            if (output.Mode != InputMode.XInput)
            {
                _logger.LogWarning("当前不是 XInput 模式，无法测试手柄按钮");
                return;
            }
            
            // 模拟按钮按下
            if (output is XInputOutput xinput)
            {
                // 更新 UI 状态
                var buttonState = ButtonStates.FirstOrDefault(b => b.Button == button);
                if (buttonState != null)
                {
                    buttonState.IsPressed = true;
                }
                
                // 实际按下按钮（需要通过反射或扩展方法访问内部状态）
                // 这里简单演示，实际需要扩展 XInputOutput 以支持直接按钮操作
                
                await Task.Delay(500); // 保持按下 500ms
                
                // 释放按钮
                if (buttonState != null)
                {
                    buttonState.IsPressed = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"测试按钮 {buttonName} 时发生错误");
        }
    }
    
    [RelayCommand]
    private async Task TestLeftStick()
    {
        try
        {
            _logger.LogInformation("测试左摇杆");
            
            var output = _inputRouter.GetOutput();
            if (output.Mode != InputMode.XInput)
            {
                _logger.LogWarning("当前不是 XInput 模式，无法测试手柄摇杆");
                return;
            }
            
            // 向右移动
            LeftStickX = short.MaxValue;
            LeftStickY = 0;
            output.SetLeftStick(LeftStickX, LeftStickY);
            await Task.Delay(500);
            
            // 向上移动
            LeftStickX = 0;
            LeftStickY = short.MaxValue;
            output.SetLeftStick(LeftStickX, LeftStickY);
            await Task.Delay(500);
            
            // 向左移动
            LeftStickX = short.MinValue;
            LeftStickY = 0;
            output.SetLeftStick(LeftStickX, LeftStickY);
            await Task.Delay(500);
            
            // 向下移动
            LeftStickX = 0;
            LeftStickY = short.MinValue;
            output.SetLeftStick(LeftStickX, LeftStickY);
            await Task.Delay(500);
            
            // 归中
            LeftStickX = 0;
            LeftStickY = 0;
            output.SetLeftStick(LeftStickX, LeftStickY);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试左摇杆时发生错误");
        }
    }
    
    [RelayCommand]
    private async Task TestRightStick()
    {
        try
        {
            _logger.LogInformation("测试右摇杆");
            
            var output = _inputRouter.GetOutput();
            if (output.Mode != InputMode.XInput)
            {
                _logger.LogWarning("当前不是 XInput 模式，无法测试手柄摇杆");
                return;
            }
            
            // 向右移动
            RightStickX = short.MaxValue;
            RightStickY = 0;
            output.SetRightStick(RightStickX, RightStickY);
            await Task.Delay(500);
            
            // 向上移动
            RightStickX = 0;
            RightStickY = short.MaxValue;
            output.SetRightStick(RightStickX, RightStickY);
            await Task.Delay(500);
            
            // 向左移动
            RightStickX = short.MinValue;
            RightStickY = 0;
            output.SetRightStick(RightStickX, RightStickY);
            await Task.Delay(500);
            
            // 向下移动
            RightStickX = 0;
            RightStickY = short.MinValue;
            output.SetRightStick(RightStickX, RightStickY);
            await Task.Delay(500);
            
            // 归中
            RightStickX = 0;
            RightStickY = 0;
            output.SetRightStick(RightStickX, RightStickY);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试右摇杆时发生错误");
        }
    }
    
    [RelayCommand]
    private async Task TestTriggers()
    {
        try
        {
            _logger.LogInformation("测试扳机");
            
            var output = _inputRouter.GetOutput();
            if (output.Mode != InputMode.XInput)
            {
                _logger.LogWarning("当前不是 XInput 模式，无法测试手柄扳机");
                return;
            }
            
            // 逐渐按下左扳机
            for (byte i = 0; i <= 255; i += 5)
            {
                LeftTrigger = i;
                output.SetLeftTrigger(LeftTrigger);
                await Task.Delay(20);
            }
            
            // 逐渐释放左扳机
            for (byte i = 255; i > 0; i -= 5)
            {
                LeftTrigger = i;
                output.SetLeftTrigger(LeftTrigger);
                await Task.Delay(20);
            }
            LeftTrigger = 0;
            output.SetLeftTrigger(LeftTrigger);
            
            await Task.Delay(200);
            
            // 逐渐按下右扳机
            for (byte i = 0; i <= 255; i += 5)
            {
                RightTrigger = i;
                output.SetRightTrigger(RightTrigger);
                await Task.Delay(20);
            }
            
            // 逐渐释放右扳机
            for (byte i = 255; i > 0; i -= 5)
            {
                RightTrigger = i;
                output.SetRightTrigger(RightTrigger);
                await Task.Delay(20);
            }
            RightTrigger = 0;
            output.SetRightTrigger(RightTrigger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试扳机时发生错误");
        }
    }
    
    [RelayCommand]
    private void ResetAll()
    {
        try
        {
            _logger.LogInformation("重置所有手柄状态");
            
            var output = _inputRouter.GetOutput();
            output.ReleaseAll();
            
            // 重置 UI 显示
            foreach (var button in ButtonStates)
            {
                button.IsPressed = false;
            }
            
            LeftStickX = 0;
            LeftStickY = 0;
            RightStickX = 0;
            RightStickY = 0;
            LeftTrigger = 0;
            RightTrigger = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置手柄状态时发生错误");
        }
    }
    
    [RelayCommand]
    private void SwitchToKeyboardMouse()
    {
        try
        {
            _logger.LogInformation("切换到键盘鼠标模式");
            bool success = _inputRouter.SwitchMode(InputMode.KeyboardMouse);
            
            if (success)
            {
                UpdateCurrentMode();
                _logger.LogInformation("已切换到键盘鼠标模式");
            }
            else
            {
                _logger.LogWarning("切换到键盘鼠标模式失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换到键盘鼠标模式时发生错误");
        }
    }
    
    [RelayCommand]
    private void SwitchToXInput()
    {
        try
        {
            _logger.LogInformation("切换到 XInput 手柄模式");
            bool success = _inputRouter.SwitchMode(InputMode.XInput);
            
            if (success)
            {
                UpdateCurrentMode();
                _logger.LogInformation("已切换到 XInput 手柄模式");
            }
            else
            {
                _logger.LogWarning("切换到 XInput 手柄模式失败，可能是驱动未安装");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换到 XInput 手柄模式时发生错误");
        }
    }
    
    #endregion
}

/// <summary>
/// 按钮状态项
/// </summary>
public partial class ButtonStateItem : ObservableObject
{
    [ObservableProperty]
    private string _buttonName = string.Empty;
    
    [ObservableProperty]
    private Xbox360Button _button;
    
    [ObservableProperty]
    private bool _isPressed;
}
