using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace BetterGenshinImpact.View.Controls.HotKey;

public class HotKeyTextBox : TextBox
{
    private GamepadInputMonitor? _gamepadMonitor;
    private CancellationTokenSource? _gamepadCts;
    private Task? _gamepadMonitorTask;
    private bool _isMonitoringGamepad = false;
    
    public static readonly DependencyProperty HotkeyTypeNameProperty = DependencyProperty.Register(
        nameof(HotKeyTypeName),
        typeof(string),
        typeof(HotKeyTextBox),
        new FrameworkPropertyMetadata(
            default(string),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (sender, _) =>
            {
                var control = (HotKeyTextBox)sender;
                // 当切换到手柄监听模式时，启动手柄监听
                if (control.HotKeyTypeName == HotKeyTypeEnum.GamepadMonitor.ToChineseName())
                {
                    control.StartGamepadMonitoring();
                }
                else
                {
                    control.StopGamepadMonitoring();
                }
            }
        )
    );

    /// <summary>
    /// 热键类型 (中文)
    /// </summary>
    public string HotKeyTypeName
    {
        get => (string)GetValue(HotkeyTypeNameProperty);
        set => SetValue(HotkeyTypeNameProperty, value);
    }

    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(Model.HotKey),
        typeof(HotKeyTextBox),
        new FrameworkPropertyMetadata(
            default(Model.HotKey),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (sender, _) =>
            {
                var control = (HotKeyTextBox)sender;
                control.Text = control.Hotkey.ToString();
            }
        )
    );

    public Model.HotKey Hotkey
    {
        get => (Model.HotKey)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public HotKeyTextBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;

        if (ContextMenu is not null)
            ContextMenu.Visibility = Visibility.Collapsed;

        Text = Hotkey.ToString();
        
        // 当控件获得焦点时，如果是手柄模式，启动监听
        GotFocus += (s, e) =>
        {
            if (HotKeyTypeName == HotKeyTypeEnum.GamepadMonitor.ToChineseName())
            {
                StartGamepadMonitoring();
            }
        };
        
        // 当控件失去焦点时，停止手柄监听
        LostFocus += (s, e) =>
        {
            StopGamepadMonitoring();
        };
        
        // 当控件卸载时，停止手柄监听
        Unloaded += (s, e) =>
        {
            StopGamepadMonitoring();
        };
    }
    
    private void StartGamepadMonitoring()
    {
        if (_isMonitoringGamepad)
            return;
            
        _isMonitoringGamepad = true;
        _gamepadMonitor = new GamepadInputMonitor();
        _gamepadCts = new CancellationTokenSource();
        
        _gamepadMonitorTask = Task.Run(async () =>
        {
            List<GamepadButton>? lastPressedButtons = null;
            DateTime firstPressTime = DateTime.MinValue;
            bool isWaitingForRelease = false;
            
            while (!_gamepadCts.Token.IsCancellationRequested)
            {
                try
                {
                    _gamepadMonitor.UpdateState();
                    
                    if (!_gamepadMonitor.IsConnected)
                    {
                        await Task.Delay(500, _gamepadCts.Token);
                        continue;
                    }
                    
                    var pressedButtons = _gamepadMonitor.GetAllPressedButtons();
                    
                    if (isWaitingForRelease)
                    {
                        // 等待所有按键松开
                        if (pressedButtons.Count == 0)
                        {
                            isWaitingForRelease = false;
                            lastPressedButtons = null;
                        }
                    }
                    else if (pressedButtons.Count > 0)
                    {
                        // 检查是否是新的按键组合
                        if (lastPressedButtons == null || lastPressedButtons.Count == 0)
                        {
                            // 第一次按下按键，开始计时
                            lastPressedButtons = new List<GamepadButton>(pressedButtons);
                            firstPressTime = DateTime.Now;
                        }
                        else
                        {
                            // 检查按键是否增加（允许组合键）
                            bool hasNewButtons = pressedButtons.Count > lastPressedButtons.Count;
                            bool allPreviousStillPressed = lastPressedButtons.All(b => pressedButtons.Contains(b));
                            
                            if (hasNewButtons && allPreviousStillPressed)
                            {
                                // 按键增加（组合键），更新按键列表但不重置计时器
                                lastPressedButtons = new List<GamepadButton>(pressedButtons);
                            }
                            else if (!allPreviousStillPressed || pressedButtons.Count < lastPressedButtons.Count)
                            {
                                // 按键减少或完全不同，重新开始
                                lastPressedButtons = new List<GamepadButton>(pressedButtons);
                                firstPressTime = DateTime.Now;
                            }
                            
                            // 检查是否超过500ms
                            if ((DateTime.Now - firstPressTime).TotalMilliseconds > 500)
                            {
                                // 在UI线程更新
                                Dispatcher.Invoke(() =>
                                {
                                    if (pressedButtons.Count == 1)
                                    {
                                        // 单键
                                        Hotkey = Model.HotKey.FromString(pressedButtons[0].ToString());
                                    }
                                    else if (pressedButtons.Count >= 2)
                                    {
                                        // 组合键（取前两个）
                                        Hotkey = Model.HotKey.FromString($"{pressedButtons[0]}+{pressedButtons[1]}");
                                    }
                                });
                                
                                // 等待按键松开
                                isWaitingForRelease = true;
                            }
                        }
                    }
                    else
                    {
                        // 所有按键都松开了
                        lastPressedButtons = null;
                    }
                    
                    await Task.Delay(50, _gamepadCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 忽略异常
                }
            }
        }, _gamepadCts.Token);
    }
    
    private void StopGamepadMonitoring()
    {
        if (!_isMonitoringGamepad)
            return;
            
        _isMonitoringGamepad = false;
        _gamepadCts?.Cancel();
        _gamepadMonitorTask?.Wait(100);
        _gamepadCts?.Dispose();
        _gamepadCts = null;
        _gamepadMonitorTask = null;
        _gamepadMonitor = null;
    }

    private static bool HasKeyChar(Key key) =>
        key
            is
            // A - Z
            >= Key.A
            and <= Key.Z
            or
            // 0 - 9
            >= Key.D0
            and <= Key.D9
            or
            // Numpad 0 - 9
            >= Key.NumPad0
            and <= Key.NumPad9
            or
            // The rest
            Key.OemQuestion
            or Key.OemQuotes
            or Key.OemPlus
            or Key.OemOpenBrackets
            or Key.OemCloseBrackets
            or Key.OemMinus
            or Key.DeadCharProcessed
            or Key.Oem1
            or Key.Oem5
            or Key.Oem7
            or Key.OemPeriod
            or Key.OemComma
            or Key.Add
            or Key.Divide
            or Key.Multiply
            or Key.Subtract
            or Key.Oem102
            or Key.Decimal;

    protected override void OnPreviewKeyDown(KeyEventArgs args)
    {
        // 手柄模式下不处理键盘输入
        if (HotKeyTypeName == HotKeyTypeEnum.GamepadMonitor.ToChineseName())
        {
            args.Handled = true;
            
            // Delete/Backspace/Escape 清空快捷键
            if (args.Key is Key.Delete or Key.Back or Key.Escape)
            {
                Hotkey = Model.HotKey.None;
            }
            return;
        }
        
        args.Handled = true;

        // Get modifiers and key data
        var modifiers = Keyboard.Modifiers;
        var key = args.Key;

        // If nothing was pressed - return
        if (key == Key.None)
            return;

        // If Alt is used as modifier - the key needs to be extracted from SystemKey
        if (key == Key.System)
            key = args.SystemKey;

        // If Delete/Backspace/Escape is pressed without modifiers - clear current value and return
        if (key is Key.Delete or Key.Back or Key.Escape && modifiers == ModifierKeys.None)
        {
            Hotkey = Model.HotKey.None;
            return;
        }

        // If the only key pressed is one of the modifier keys - return
        if (
            key
            is Key.LeftCtrl
            or Key.RightCtrl
            or Key.LeftAlt
            or Key.RightAlt
            or Key.LeftShift
            or Key.RightShift
            or Key.LWin
            or Key.RWin
            or Key.Clear
            or Key.OemClear
            or Key.Apps
        )
            return;

        // If Enter/Space/Tab is pressed without modifiers - return
        if (key is Key.Enter or Key.Tab && modifiers == ModifierKeys.None)
            return;

        if (HotKeyTypeName == HotKeyTypeEnum.GlobalRegister.ToChineseName() && key is Key.Enter or Key.Space or Key.Tab && modifiers == ModifierKeys.None)
            return;

        // If key has a character and pressed without modifiers or only with Shift - return
        if (HotKeyTypeName == HotKeyTypeEnum.GlobalRegister.ToChineseName() && HasKeyChar(key) && modifiers is ModifierKeys.None or ModifierKeys.Shift)
            return;

        // Set value
        Hotkey = new Model.HotKey(key, modifiers);
    }

    /// <summary>
    /// 支持鼠标侧键配置
    /// </summary>
    /// <param name="args"></param>
    protected override void OnPreviewMouseDown(MouseButtonEventArgs args)
    {
        // 手柄模式下不处理鼠标输入
        if (HotKeyTypeName == HotKeyTypeEnum.GamepadMonitor.ToChineseName())
        {
            return;
        }
        
        if (args.ChangedButton is MouseButton.XButton1 or MouseButton.XButton2)
        {
            if (HotKeyTypeName == HotKeyTypeEnum.GlobalRegister.ToChineseName())
            {
                Hotkey = new Model.HotKey(Key.None);
            }
            else
            {
                Hotkey = new Model.HotKey(Key.None, ModifierKeys.None, args.ChangedButton);
            }
        }
    }
}
