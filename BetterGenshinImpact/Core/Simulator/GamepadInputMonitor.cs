using System;
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
    
    /// <summary>
    /// 是否有手柄连接
    /// </summary>
    public bool IsConnected => _isConnected;
}
