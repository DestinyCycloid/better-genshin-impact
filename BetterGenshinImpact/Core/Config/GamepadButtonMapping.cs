using System;
using System.Text.Json.Serialization;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace BetterGenshinImpact.Core.Config;

/// <summary>
/// 手柄按键映射，支持按钮、扳机和组合键
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
    /// 是否是组合键
    /// </summary>
    public bool IsCombo { get; set; }
    
    /// <summary>
    /// 组合键的修饰键（如 LB）
    /// </summary>
    public Xbox360Button ModifierButton { get; set; }
    
    /// <summary>
    /// 组合键的主键（如 Y 或 B）
    /// </summary>
    public Xbox360Button MainButton { get; set; }
    
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
            IsLeftTrigger = false,
            IsCombo = false
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
            IsLeftTrigger = isLeft,
            IsCombo = false
        };
    }
    
    /// <summary>
    /// 创建组合键映射
    /// </summary>
    /// <param name="modifier">修饰键（如 LB）</param>
    /// <param name="main">主键（如 Y 或 B）</param>
    public static GamepadButtonMapping FromCombo(Xbox360Button modifier, Xbox360Button main)
    {
        return new GamepadButtonMapping
        {
            Button = default(Xbox360Button),
            IsTrigger = false,
            IsLeftTrigger = false,
            IsCombo = true,
            ModifierButton = modifier,
            MainButton = main
        };
    }
    
    /// <summary>
    /// 获取显示名称
    /// </summary>
    public string GetDisplayName()
    {
        if (IsCombo)
        {
            return $"{GetButtonName(ModifierButton)}+{GetButtonName(MainButton)}";
        }
        
        if (IsTrigger)
        {
            return IsLeftTrigger ? "LT (左扳机)" : "RT (右扳机)";
        }
        
        return GetButtonName(Button);
    }
    
    /// <summary>
    /// 获取按钮名称
    /// </summary>
    private static string GetButtonName(Xbox360Button button)
    {
        if (button == Xbox360Button.A) return "A";
        if (button == Xbox360Button.B) return "B";
        if (button == Xbox360Button.X) return "X";
        if (button == Xbox360Button.Y) return "Y";
        if (button == Xbox360Button.LeftShoulder) return "LB";
        if (button == Xbox360Button.RightShoulder) return "RB";
        if (button == Xbox360Button.Back) return "Back";
        if (button == Xbox360Button.Start) return "Start";
        if (button == Xbox360Button.Guide) return "Guide";
        if (button == Xbox360Button.Up) return "方向键上";
        if (button == Xbox360Button.Down) return "方向键下";
        if (button == Xbox360Button.Left) return "方向键左";
        if (button == Xbox360Button.Right) return "方向键右";
        if (button == Xbox360Button.LeftThumb) return "LS";
        if (button == Xbox360Button.RightThumb) return "RS";
        
        return "未知";
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is GamepadButtonMapping other)
        {
            if (IsCombo != other.IsCombo) return false;
            if (IsCombo)
            {
                return ModifierButton == other.ModifierButton && MainButton == other.MainButton;
            }
            
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
        if (IsCombo)
        {
            return HashCode.Combine(IsCombo, ModifierButton, MainButton);
        }
        return HashCode.Combine(Button, IsTrigger, IsLeftTrigger);
    }
    
    public override string ToString()
    {
        return DisplayName;
    }
}
