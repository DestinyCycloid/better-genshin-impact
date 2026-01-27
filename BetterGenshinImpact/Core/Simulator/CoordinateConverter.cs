using System;

namespace BetterGenshinImpact.Core.Simulator;

/// <summary>
/// 坐标转换工具类，用于在不同输入方式之间转换坐标
/// </summary>
public static class CoordinateConverter
{
    /// <summary>
    /// 将鼠标相对移动量转换为摇杆坐标
    /// </summary>
    /// <param name="dx">鼠标 X 轴相对移动量</param>
    /// <param name="dy">鼠标 Y 轴相对移动量</param>
    /// <param name="sensitivity">灵敏度参数，用于缩放转换比例</param>
    /// <returns>摇杆坐标 (x, y)，范围 [-32768, 32767]</returns>
    public static (short x, short y) MouseDeltaToStick(int dx, int dy, float sensitivity)
    {
        // 应用灵敏度缩放
        float scaledX = dx * sensitivity;
        float scaledY = dy * sensitivity;
        
        // 转换到摇杆范围并限幅
        short stickX = (short)Math.Clamp(scaledX, short.MinValue, short.MaxValue);
        short stickY = (short)Math.Clamp(scaledY, short.MinValue, short.MaxValue);
        
        #if DEBUG
        // 在调试模式下记录限幅事件
        if (Math.Abs(scaledX) > short.MaxValue || Math.Abs(scaledY) > short.MaxValue)
        {
            System.Diagnostics.Debug.WriteLine($"摇杆坐标限幅: ({scaledX}, {scaledY}) -> ({stickX}, {stickY})");
        }
        #endif
        
        return (stickX, stickY);
    }
    
    /// <summary>
    /// 将 WASD 按键状态转换为左摇杆坐标
    /// </summary>
    /// <param name="w">W 键是否按下（向前）</param>
    /// <param name="a">A 键是否按下（向左）</param>
    /// <param name="s">S 键是否按下（向后）</param>
    /// <param name="d">D 键是否按下（向右）</param>
    /// <returns>左摇杆坐标 (x, y)，范围 [-32768, 32767]</returns>
    public static (short x, short y) WASDToStick(bool w, bool a, bool s, bool d)
    {
        short x = 0;
        short y = 0;
        
        // 根据按键状态设置摇杆方向
        if (w) y = short.MaxValue;      // 向前
        if (s) y = short.MinValue;      // 向后
        if (a) x = short.MinValue;      // 向左
        if (d) x = short.MaxValue;      // 向右
        
        // 对角线移动时归一化，避免速度过快
        // 使用 1/√2 ≈ 0.7071 作为归一化因子
        if ((w || s) && (a || d))
        {
            float factor = 1.0f / (float)Math.Sqrt(2);
            x = (short)(x * factor);
            y = (short)(y * factor);
        }
        
        return (x, y);
    }
}
