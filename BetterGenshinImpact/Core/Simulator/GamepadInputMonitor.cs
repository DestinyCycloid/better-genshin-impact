using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace BetterGenshinImpact.Core.Simulator;

/// <summary>
/// 手柄输入监听器，用于读取物理手柄的输入状态
/// </summary>
public class GamepadInputMonitor
{
    private readonly ILogger<GamepadInputMonitor> _logger = App.GetLogger<GamepadInputMonitor>();
    
    // XInput API
    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetState(int dwUserIndex, ref XINPUT_STATE pState);
    
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_DEVICE_NOT_CONNECTED = 1167;
    
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }
    
    // 按钮定义
    private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
    private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
    private const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
    private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    private const ushort XINPUT_GAMEPAD_START = 0x0010;
    private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
    private const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
    private const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
    private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;  // LB
    private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200; // RB
    private const ushort XINPUT_GAMEPAD_A = 0x1000;
    private const ushort XINPUT_GAMEPAD_B = 0x2000;
    private const ushort XINPUT_GAMEPAD_X = 0x4000;
    private const ushort XINPUT_GAMEPAD_Y = 0x8000;
    
    // 扳机阈值（0-255）
    private const byte TRIGGER_THRESHOLD = 30;
    
    private XINPUT_STATE _lastState;
    private bool _isConnected;
    
    public GamepadInputMonitor()
    {
        _lastState = new XINPUT_STATE();
        _isConnected = false;
    }
    
    /// <summary>
    /// 更新手柄状态
    /// </summary>
    /// <returns>是否成功读取状态</returns>
    public bool UpdateState()
    {
        XINPUT_STATE state = new XINPUT_STATE();
        int result = XInputGetState(0, ref state); // 读取第一个手柄
        
        if (result == ERROR_SUCCESS)
        {
            if (!_isConnected)
            {
                _logger.LogInformation("🎮 检测到手柄连接");
                _isConnected = true;
            }
            _lastState = state;
            return true;
        }
        else if (result == ERROR_DEVICE_NOT_CONNECTED)
        {
            if (_isConnected)
            {
                _logger.LogWarning("⚠️ 手柄断开连接");
                _isConnected = false;
            }
            return false;
        }
        
        return false;
    }
    
    /// <summary>
    /// 检测十字键上是否被按下（用于切换角色）
    /// </summary>
    public bool IsDPadUpPressed()
    {
        return (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_DPAD_UP) != 0;
    }
    
    /// <summary>
    /// 检测十字键下是否被按下（用于切换角色）
    /// </summary>
    public bool IsDPadDownPressed()
    {
        return (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_DPAD_DOWN) != 0;
    }
    
    /// <summary>
    /// 检测十字键左是否被按下（用于切换角色）
    /// </summary>
    public bool IsDPadLeftPressed()
    {
        return (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_DPAD_LEFT) != 0;
    }
    
    /// <summary>
    /// 检测十字键右是否被按下（用于切换角色）
    /// </summary>
    public bool IsDPadRightPressed()
    {
        return (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_DPAD_RIGHT) != 0;
    }
    
    // ========== 以下为扩展的按键检测方法，用于快捷键支持 ==========
    
    public bool IsAPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_A) != 0;
    public bool IsBPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_B) != 0;
    public bool IsXPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_X) != 0;
    public bool IsYPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_Y) != 0;
    public bool IsLBPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0;
    public bool IsRBPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0;
    public bool IsStartPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_START) != 0;
    public bool IsBackPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_BACK) != 0;
    public bool IsLeftThumbPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_LEFT_THUMB) != 0;
    public bool IsRightThumbPressed() => (_lastState.Gamepad.wButtons & XINPUT_GAMEPAD_RIGHT_THUMB) != 0;
    public bool IsLTPressed() => _lastState.Gamepad.bLeftTrigger > TRIGGER_THRESHOLD;
    public bool IsRTPressed() => _lastState.Gamepad.bRightTrigger > TRIGGER_THRESHOLD;
    
    /// <summary>
    /// 获取当前按下的所有按钮（用于快捷键检测）
    /// </summary>
    public List<GamepadButton> GetAllPressedButtons()
    {
        var buttons = new List<GamepadButton>();
        
        if (IsAPressed()) buttons.Add(GamepadButton.A);
        if (IsBPressed()) buttons.Add(GamepadButton.B);
        if (IsXPressed()) buttons.Add(GamepadButton.X);
        if (IsYPressed()) buttons.Add(GamepadButton.Y);
        if (IsLBPressed()) buttons.Add(GamepadButton.LB);
        if (IsRBPressed()) buttons.Add(GamepadButton.RB);
        if (IsLTPressed()) buttons.Add(GamepadButton.LT);
        if (IsRTPressed()) buttons.Add(GamepadButton.RT);
        if (IsStartPressed()) buttons.Add(GamepadButton.Start);
        if (IsBackPressed()) buttons.Add(GamepadButton.Back);
        if (IsLeftThumbPressed()) buttons.Add(GamepadButton.LeftThumb);
        if (IsRightThumbPressed()) buttons.Add(GamepadButton.RightThumb);
        if (IsDPadUpPressed()) buttons.Add(GamepadButton.DPadUp);
        if (IsDPadDownPressed()) buttons.Add(GamepadButton.DPadDown);
        if (IsDPadLeftPressed()) buttons.Add(GamepadButton.DPadLeft);
        if (IsDPadRightPressed()) buttons.Add(GamepadButton.DPadRight);
        
        return buttons;
    }
    
    /// <summary>
    /// 获取当前按下的按钮（用于快捷键检测）
    /// </summary>
    public GamepadButton GetPressedButton()
    {
        if (IsAPressed()) return GamepadButton.A;
        if (IsBPressed()) return GamepadButton.B;
        if (IsXPressed()) return GamepadButton.X;
        if (IsYPressed()) return GamepadButton.Y;
        if (IsLBPressed()) return GamepadButton.LB;
        if (IsRBPressed()) return GamepadButton.RB;
        if (IsLTPressed()) return GamepadButton.LT;
        if (IsRTPressed()) return GamepadButton.RT;
        if (IsStartPressed()) return GamepadButton.Start;
        if (IsBackPressed()) return GamepadButton.Back;
        if (IsLeftThumbPressed()) return GamepadButton.LeftThumb;
        if (IsRightThumbPressed()) return GamepadButton.RightThumb;
        if (IsDPadUpPressed()) return GamepadButton.DPadUp;
        if (IsDPadDownPressed()) return GamepadButton.DPadDown;
        if (IsDPadLeftPressed()) return GamepadButton.DPadLeft;
        if (IsDPadRightPressed()) return GamepadButton.DPadRight;
        return GamepadButton.None;
    }
    
    /// <summary>
    /// 检测指定按钮是否被按下
    /// </summary>
    public bool IsButtonPressed(GamepadButton button)
    {
        return button switch
        {
            GamepadButton.A => IsAPressed(),
            GamepadButton.B => IsBPressed(),
            GamepadButton.X => IsXPressed(),
            GamepadButton.Y => IsYPressed(),
            GamepadButton.LB => IsLBPressed(),
            GamepadButton.RB => IsRBPressed(),
            GamepadButton.LT => IsLTPressed(),
            GamepadButton.RT => IsRTPressed(),
            GamepadButton.Start => IsStartPressed(),
            GamepadButton.Back => IsBackPressed(),
            GamepadButton.LeftThumb => IsLeftThumbPressed(),
            GamepadButton.RightThumb => IsRightThumbPressed(),
            GamepadButton.DPadUp => IsDPadUpPressed(),
            GamepadButton.DPadDown => IsDPadDownPressed(),
            GamepadButton.DPadLeft => IsDPadLeftPressed(),
            GamepadButton.DPadRight => IsDPadRightPressed(),
            _ => false
        };
    }
    
    /// <summary>
    /// 是否有手柄连接
    /// </summary>
    public bool IsConnected => _isConnected;
}

/// <summary>
/// 手柄按钮枚举
/// </summary>
public enum GamepadButton
{
    None = 0,
    A = 1,
    B = 2,
    X = 3,
    Y = 4,
    LB = 5,
    RB = 6,
    LT = 7,
    RT = 8,
    Start = 9,
    Back = 10,
    LeftThumb = 11,
    RightThumb = 12,
    DPadUp = 13,
    DPadDown = 14,
    DPadLeft = 15,
    DPadRight = 16
}
