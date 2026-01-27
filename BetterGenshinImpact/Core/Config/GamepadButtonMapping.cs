using System;
using System.Text.Json.Serialization;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace BetterGenshinImpact.Core.Config;

/// <summary>
/// 手柄按键映射，支持按钮和扳机
/// </summary>
[Serializable]
public class GamepadButtonMapping
{
    /// <summary>
    /// 按钮类型
    /// </summary>
    public Xbox360Button Button { get; set; }
    
    /// <summary>
    /// 是否是扳机
    /// </summary>
    public bool IsTrigger { get; set; }
    
    /// <summary>
    /// 是否是左扳机（仅当 IsTrigger=true 时有效）
    /// </summary>
    public bool IsLeftTrigger { get; set; }
    
    /// <summary>
    /// 显示名称（用于 UI 绑定）
    /// </summary>
    [JsonIgnore]
    public string DisplayName => GetDisplayName();
    
    /// <summary>
    /// 创建按钮映射
    /// </summary>
    public static GamepadButtonMapping FromButton(Xbox360Button button)
    {
        return new GamepadButtonMapping
        {
            Button = button,
            IsTrigger = false,
            IsLeftTrigger = false
        };
    }
    
    /// <summary>
    /// 创建扳机映射
    /// </summary>
    public static GamepadButtonMapping FromTrigger(bool isLeft)
    {
        return new GamepadButtonMapping
        {
            Button = default(Xbox360Button),
            IsTrigger = true,
            IsLeftTrigger = isLeft
        };
    }
    
    /// <summary>
    /// 获取显示名称
    /// </summary>
    public string GetDisplayName()
    {
        if (IsTrigger)
        {
            return IsLeftTrigger ? "LT (左扳机)" : "RT (右扳机)";
        }
        
        if (Button == Xbox360Button.A) return "A";
        if (Button == Xbox360Button.B) return "B";
        if (Button == Xbox360Button.X) return "X";
        if (Button == Xbox360Button.Y) return "Y";
        if (Button == Xbox360Button.LeftShoulder) return "LB (左肩键)";
        if (Button == Xbox360Button.RightShoulder) return "RB (右肩键)";
        if (Button == Xbox360Button.Back) return "Back";
        if (Button == Xbox360Button.Start) return "Start";
        if (Button == Xbox360Button.Guide) return "Guide";
        if (Button == Xbox360Button.Up) return "十字键上";
        if (Button == Xbox360Button.Down) return "十字键下";
        if (Button == Xbox360Button.Left) return "十字键左";
        if (Button == Xbox360Button.Right) return "十字键右";
        if (Button == Xbox360Button.LeftThumb) return "LS (左摇杆按下)";
        if (Button == Xbox360Button.RightThumb) return "RS (右摇杆按下)";
        
        return "未知";
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is GamepadButtonMapping other)
        {
            if (IsTrigger != other.IsTrigger) return false;
            if (IsTrigger)
            {
                return IsLeftTrigger == other.IsLeftTrigger;
            }
            return Button == other.Button;
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Button, IsTrigger, IsLeftTrigger);
    }
    
    public override string ToString()
    {
        return DisplayName;
    }
}
