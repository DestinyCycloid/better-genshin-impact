using BetterGenshinImpact.Core.Simulator;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fischless.HotkeyCapture;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Input;

namespace BetterGenshinImpact.Model;

/// <summary>
/// 在页面展示快捷键配置的对象
/// </summary>
public partial class HotKeySettingModel : ObservableObject
{
    [ObservableProperty] private HotKey _hotKey;

    /// <summary>
    /// 键鼠监听、全局热键
    /// </summary>
    [ObservableProperty] private HotKeyTypeEnum _hotKeyType;

    [ObservableProperty] private string _hotKeyTypeName;

    [ObservableProperty]
    private ObservableCollection<HotKeySettingModel> _children = [];

    public string FunctionName { get; set; }

    public bool IsExpanded => true;

    /// <summary>
    /// 界面上显示是文件夹而不是快捷键
    /// </summary>
    [ObservableProperty]
    private bool _isDirectory;

    public string ConfigPropertyName { get; set; }

    public Action<object?, KeyPressedEventArgs>? OnKeyPressAction { get; set; }
    public Action<object?, KeyPressedEventArgs>? OnKeyDownAction { get; set; }
    public Action<object?, KeyPressedEventArgs>? OnKeyUpAction { get; set; }

    public bool IsHold { get; set; }

    [ObservableProperty] private bool _switchHotkeyTypeEnabled;

    /// <summary>
    /// 全局热键配置
    /// </summary>
    public HotkeyHook? GlobalRegisterHook { get; set; }

    /// <summary>
    /// 键盘监听配置
    /// </summary>
    public KeyboardHook? KeyboardMonitorHook { get; set; }

    /// <summary>
    /// 鼠标监听配置
    /// </summary>
    public MouseHook? MouseMonitorHook { get; set; }
    
    /// <summary>
    /// 手柄监听配置
    /// </summary>
    public GamepadHook? GamepadMonitorHook { get; set; }

    public HotKeySettingModel(string functionName)
    {
        FunctionName = functionName;
        IsDirectory = true;
    }

    public HotKeySettingModel(string functionName, string configPropertyName, string hotkey, string hotKeyTypeCode, Action<object?, KeyPressedEventArgs>? onKeyPressAction, bool isHold = false)
    {
        FunctionName = functionName;
        ConfigPropertyName = configPropertyName;
        HotKey = HotKey.FromString(hotkey);
        HotKeyType = (HotKeyTypeEnum)Enum.Parse(typeof(HotKeyTypeEnum), hotKeyTypeCode);
        HotKeyTypeName = HotKeyType.ToChineseName();
        OnKeyPressAction = onKeyPressAction;
        IsHold = isHold;
        // 按住模式可以在键鼠监听和手柄监听之间切换，但不能使用全局热键
        SwitchHotkeyTypeEnabled = true;
    }

    public void RegisterHotKey()
    {
        if (HotKey.IsEmpty)
        {
            return;
        }

        try
        {
            if (HotKeyType == HotKeyTypeEnum.GlobalRegister)
            {
                Hotkey hotkey = new(HotKey.ToString());
                GlobalRegisterHook?.Dispose();
                GlobalRegisterHook = new HotkeyHook();
                if (OnKeyPressAction != null)
                {
                    GlobalRegisterHook.KeyPressed -= OnKeyPressed;
                    GlobalRegisterHook.KeyPressed += OnKeyPressed;
                }
                GlobalRegisterHook.RegisterHotKey(hotkey.ModifierKey, hotkey.Key);
            }
            else if (HotKeyType == HotKeyTypeEnum.GamepadMonitor)
            {
                // 手柄监听模式
                GamepadMonitorHook?.Dispose();
                GamepadMonitorHook = new GamepadHook
                {
                    IsHold = IsHold
                };
                
                if (OnKeyPressAction != null)
                {
                    GamepadMonitorHook.GamepadPressed -= OnKeyPressed;
                    GamepadMonitorHook.GamepadPressed += OnKeyPressed;
                }
                if (OnKeyDownAction != null)
                {
                    GamepadMonitorHook.GamepadDownEvent -= OnKeyDown;
                    GamepadMonitorHook.GamepadDownEvent += OnKeyDown;
                }
                if (OnKeyUpAction != null)
                {
                    GamepadMonitorHook.GamepadUpEvent -= OnKeyUp;
                    GamepadMonitorHook.GamepadUpEvent += OnKeyUp;
                }
                
                // 从HotKey字符串解析手柄按钮（支持组合键，如 "LB+A"）
                var hotkeyStr = HotKey.ToString();
                if (hotkeyStr.Contains("+"))
                {
                    // 组合键：修饰键 + 主键
                    var parts = hotkeyStr.Split('+');
                    if (parts.Length == 2 && 
                        Enum.TryParse<GamepadButton>(parts[0].Trim(), out var modifier) &&
                        Enum.TryParse<GamepadButton>(parts[1].Trim(), out var button))
                    {
                        GamepadMonitorHook.RegisterHotKey(modifier, button);
                    }
                }
                else
                {
                    // 单键
                    if (Enum.TryParse<GamepadButton>(hotkeyStr, out var button))
                    {
                        GamepadMonitorHook.RegisterHotKey(button);
                    }
                }
            }
            else
            {
                MouseMonitorHook?.Dispose();
                KeyboardMonitorHook?.Dispose();
                if (HotKey.MouseButton is MouseButton.XButton1 or MouseButton.XButton2)
                {
                    MouseMonitorHook = new MouseHook
                    {
                        IsHold = IsHold
                    };

                    if (OnKeyPressAction != null)
                    {
                        MouseMonitorHook.MousePressed -= OnKeyPressed;
                        MouseMonitorHook.MousePressed += OnKeyPressed;
                    }
                    if (OnKeyDownAction != null)
                    {
                        MouseMonitorHook.MouseDownEvent -= OnKeyDown;
                        MouseMonitorHook.MouseDownEvent += OnKeyDown;
                    }
                    if (OnKeyUpAction != null)
                    {
                        MouseMonitorHook.MouseUpEvent -= OnKeyUp;
                        MouseMonitorHook.MouseUpEvent += OnKeyUp;
                    }
                    MouseMonitorHook.RegisterHotKey((MouseButtons)Enum.Parse(typeof(MouseButtons), HotKey.MouseButton.ToString()));
                }
                else
                {
                    // 如果是组合键，不支持
                    if (HotKey.Modifiers != ModifierKeys.None)
                    {
                        HotKey = HotKey.None;
                        return;
                    }
                    KeyboardMonitorHook = new KeyboardHook
                    {
                        IsHold = IsHold
                    };
                    if (OnKeyPressAction != null)
                    {
                        KeyboardMonitorHook.KeyPressedEvent -= OnKeyPressed;
                        KeyboardMonitorHook.KeyPressedEvent += OnKeyPressed;
                    }
                    if (OnKeyDownAction != null)
                    {
                        KeyboardMonitorHook.KeyDownEvent -= OnKeyDown;
                        KeyboardMonitorHook.KeyDownEvent += OnKeyDown;
                    }
                    if (OnKeyUpAction != null)
                    {
                        KeyboardMonitorHook.KeyUpEvent -= OnKeyUp;
                        KeyboardMonitorHook.KeyUpEvent += OnKeyUp;
                    }

                    KeyboardMonitorHook.RegisterHotKey((Keys)Enum.Parse(typeof(Keys), HotKey.Key.ToString()));
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            HotKey = HotKey.None;
        }
    }

    private void OnKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        OnKeyPressAction?.Invoke(sender, e);
    }

    private void OnKeyDown(object? sender, KeyPressedEventArgs e)
    {
        OnKeyDownAction?.Invoke(sender, e);
    }

    private void OnKeyUp(object? sender, KeyPressedEventArgs e)
    {
        OnKeyUpAction?.Invoke(sender, e);
    }

    public void UnRegisterHotKey()
    {
        GlobalRegisterHook?.Dispose();
        MouseMonitorHook?.Dispose();
        KeyboardMonitorHook?.Dispose();
        GamepadMonitorHook?.Dispose();
    }

    [RelayCommand]
    public void OnSwitchHotKeyType()
    {
        if (IsHold)
        {
            // 按住模式：只在键鼠监听和手柄监听之间切换（不支持全局热键）
            HotKeyType = HotKeyType switch
            {
                HotKeyTypeEnum.KeyboardMonitor => HotKeyTypeEnum.GamepadMonitor,
                HotKeyTypeEnum.GamepadMonitor => HotKeyTypeEnum.KeyboardMonitor,
                _ => HotKeyTypeEnum.KeyboardMonitor // 默认键鼠监听
            };
        }
        else
        {
            // 非按住模式：三种类型循环切换
            HotKeyType = HotKeyType switch
            {
                HotKeyTypeEnum.GlobalRegister => HotKeyTypeEnum.KeyboardMonitor,
                HotKeyTypeEnum.KeyboardMonitor => HotKeyTypeEnum.GamepadMonitor,
                HotKeyTypeEnum.GamepadMonitor => HotKeyTypeEnum.GlobalRegister,
                _ => HotKeyTypeEnum.GlobalRegister
            };
        }
        HotKeyTypeName = HotKeyType.ToChineseName();
    }
}
