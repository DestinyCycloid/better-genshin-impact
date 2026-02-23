using BetterGenshinImpact.Core.Simulator;
using Fischless.HotkeyCapture;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BetterGenshinImpact.Model;

/// <summary>
/// 手柄按键监听器（类似于KeyboardHook和MouseHook）
/// 支持单键和组合键（修饰键 + 主键）
/// </summary>
public class GamepadHook : IDisposable
{
    private readonly GamepadInputMonitor _monitor;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private GamepadButton _bindButton = GamepadButton.None;
    private GamepadButton _modifierButton = GamepadButton.None;
    private bool _prevPressed = false;
    
    public event EventHandler<KeyPressedEventArgs>? GamepadPressed;
    public event EventHandler<KeyPressedEventArgs>? GamepadDownEvent;
    public event EventHandler<KeyPressedEventArgs>? GamepadUpEvent;
    
    public bool IsHold { get; set; } = false;
    
    /// <summary>
    /// 主按键
    /// </summary>
    public GamepadButton BindButton
    {
        get => _bindButton;
        set
        {
            _bindButton = value;
            StartMonitoring();
        }
    }
    
    /// <summary>
    /// 修饰键（可选，用于组合键）
    /// </summary>
    public GamepadButton ModifierButton
    {
        get => _modifierButton;
        set
        {
            _modifierButton = value;
            StartMonitoring();
        }
    }
    
    public GamepadHook()
    {
        _monitor = new GamepadInputMonitor();
    }
    
    /// <summary>
    /// 注册单键快捷键
    /// </summary>
    public void RegisterHotKey(GamepadButton button)
    {
        ModifierButton = GamepadButton.None;
        BindButton = button;
    }
    
    /// <summary>
    /// 注册组合键快捷键（修饰键 + 主键）
    /// </summary>
    public void RegisterHotKey(GamepadButton modifier, GamepadButton button)
    {
        ModifierButton = modifier;
        BindButton = button;
    }
    
    public void UnregisterHotKey()
    {
        StopMonitoring();
        _bindButton = GamepadButton.None;
        _modifierButton = GamepadButton.None;
    }
    
    private void StartMonitoring()
    {
        StopMonitoring();
        
        if (_bindButton == GamepadButton.None)
        {
            return;
        }
        
        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _monitor.UpdateState();
                    
                    if (!_monitor.IsConnected)
                    {
                        await Task.Delay(500, _cts.Token);
                        continue;
                    }
                    
                    // 检测组合键或单键
                    bool isPressed;
                    if (_modifierButton != GamepadButton.None)
                    {
                        // 组合键：修饰键和主键都必须按下
                        bool modifierPressed = _monitor.IsButtonPressed(_modifierButton);
                        bool mainPressed = _monitor.IsButtonPressed(_bindButton);
                        isPressed = modifierPressed && mainPressed;
                    }
                    else
                    {
                        // 单键
                        isPressed = _monitor.IsButtonPressed(_bindButton);
                    }
                    
                    if (isPressed && !_prevPressed)
                    {
                        // 按下事件
                        GamepadDownEvent?.Invoke(this, new KeyPressedEventArgs(0, 0));
                        
                        if (!IsHold)
                        {
                            // 非按住模式：按下时触发
                            GamepadPressed?.Invoke(this, new KeyPressedEventArgs(0, 0));
                        }
                    }
                    else if (!isPressed && _prevPressed)
                    {
                        // 松开事件
                        GamepadUpEvent?.Invoke(this, new KeyPressedEventArgs(0, 0));
                        
                        if (IsHold)
                        {
                            // 按住模式：松开时触发
                            GamepadPressed?.Invoke(this, new KeyPressedEventArgs(0, 0));
                        }
                    }
                    
                    _prevPressed = isPressed;
                    
                    // 降低CPU占用
                    await Task.Delay(50, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // 忽略异常，继续监听
                }
            }
        }, _cts.Token);
    }
    
    private void StopMonitoring()
    {
        _cts?.Cancel();
        _monitorTask?.Wait(100);
        _cts?.Dispose();
        _cts = null;
        _monitorTask = null;
        _prevPressed = false;
    }
    
    public void Dispose()
    {
        UnregisterHotKey();
    }
}
