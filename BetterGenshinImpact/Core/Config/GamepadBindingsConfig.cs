using CommunityToolkit.Mvvm.ComponentModel;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using BetterGenshinImpact.Core.Simulator.Extensions;

namespace BetterGenshinImpact.Core.Config;

/// <summary>
/// 手柄按键绑定配置
/// 定义游戏动作到 XInput 手柄按钮的映射关系
/// </summary>
[Serializable]
public partial class GamepadBindingsConfig : ObservableObject
{
    #region 战斗动作映射

    /// <summary>
    /// 普通攻击
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _normalAttack = GamepadButtonMapping.FromButton(Xbox360Button.X);

    /// <summary>
    /// 元素战技
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _elementalSkill = GamepadButtonMapping.FromButton(Xbox360Button.RightShoulder);

    /// <summary>
    /// 元素爆发
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _elementalBurst = GamepadButtonMapping.FromTrigger(false); // RT (右扳机)

    /// <summary>
    /// 跳跃
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _jump = GamepadButtonMapping.FromButton(Xbox360Button.A);

    /// <summary>
    /// 冲刺
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _sprint = GamepadButtonMapping.FromButton(Xbox360Button.B);

    /// <summary>
    /// 拾取/交互
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _pickUpOrInteract = GamepadButtonMapping.FromButton(Xbox360Button.Y);

    /// <summary>
    /// 切换瞄准模式
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _switchAimingMode = GamepadButtonMapping.FromTrigger(true); // LT (左扳机)

    /// <summary>
    /// 落下
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _drop = GamepadButtonMapping.FromButton(Xbox360Button.B);

    /// <summary>
    /// 快捷使用小道具
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _quickUseGadget = GamepadButtonMapping.FromButton(Xbox360Button.RightShoulder);

    #endregion

    #region 角色切换映射

    /// <summary>
    /// 切换小队角色1
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _switchMember1 = GamepadButtonMapping.FromButton(Xbox360Button.Up);

    /// <summary>
    /// 切换小队角色2
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _switchMember2 = GamepadButtonMapping.FromButton(Xbox360Button.Right);

    /// <summary>
    /// 切换小队角色3
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _switchMember3 = GamepadButtonMapping.FromButton(Xbox360Button.Left);

    /// <summary>
    /// 切换小队角色4
    /// </summary>
    [ObservableProperty]
    private GamepadButtonMapping _switchMember4 = GamepadButtonMapping.FromButton(Xbox360Button.Down);

    #endregion

    #region 菜单操作映射

    /// <summary>
    /// 呼出快捷轮盘
    /// </summary>
    [ObservableProperty]
    private Xbox360Button _shortcutWheel = Xbox360Button.LeftShoulder;

    /// <summary>
    /// 打开派蒙菜单
    /// </summary>
    [ObservableProperty]
    private Xbox360Button _openPaimonMenu = Xbox360Button.Start;

    /// <summary>
    /// 打开背包
    /// </summary>
    [ObservableProperty]
    private Xbox360Button _openInventory = Xbox360Button.Guide;

    #endregion

    #region 摇杆和扳机配置

    /// <summary>
    /// 使用左摇杆控制移动
    /// </summary>
    [ObservableProperty]
    private bool _useLeftStickForMovement = true;

    /// <summary>
    /// 使用右摇杆控制镜头
    /// </summary>
    [ObservableProperty]
    private bool _useRightStickForCamera = true;

    /// <summary>
    /// 使用左扳机进行瞄准
    /// </summary>
    [ObservableProperty]
    private bool _useLeftTriggerForAiming = false;

    /// <summary>
    /// 使用右扳机使用小道具
    /// </summary>
    [ObservableProperty]
    private bool _useRightTriggerForGadget = false;

    /// <summary>
    /// 鼠标移动到右摇杆的灵敏度
    /// </summary>
    [ObservableProperty]
    private float _cameraSensitivity = 1.0f;

    /// <summary>
    /// 移动摇杆的死区大小（0-1）
    /// </summary>
    [ObservableProperty]
    private float _movementDeadZone = 0.1f;

    /// <summary>
    /// 镜头摇杆的死区大小（0-1）
    /// </summary>
    [ObservableProperty]
    private float _cameraDeadZone = 0.1f;

    #endregion

    /// <summary>
    /// 根据游戏动作获取对应的手柄按钮映射
    /// </summary>
    /// <param name="action">游戏动作</param>
    /// <returns>对应的手柄按钮映射</returns>
    public GamepadButtonMapping? GetButtonMapping(GIActions action)
    {
        return action switch
        {
            GIActions.NormalAttack => NormalAttack,
            GIActions.ElementalSkill => ElementalSkill,
            GIActions.ElementalBurst => ElementalBurst,
            GIActions.Jump => Jump,
            GIActions.SprintKeyboard => Sprint,
            GIActions.SprintMouse => Sprint,
            GIActions.PickUpOrInteract => PickUpOrInteract,
            GIActions.SwitchAimingMode => SwitchAimingMode,
            GIActions.Drop => Drop,
            GIActions.QuickUseGadget => QuickUseGadget,
            GIActions.SwitchMember1 => SwitchMember1,
            GIActions.SwitchMember2 => SwitchMember2,
            GIActions.SwitchMember3 => SwitchMember3,
            GIActions.SwitchMember4 => SwitchMember4,
            GIActions.ShortcutWheel => null,
            GIActions.OpenPaimonMenu => null,
            GIActions.OpenInventory => null,
            // 移动相关动作由摇杆控制，不映射到按钮
            GIActions.MoveForward => null,
            GIActions.MoveBackward => null,
            GIActions.MoveLeft => null,
            GIActions.MoveRight => null,
            // 其他未映射的动作
            _ => null
        };
    }
    
    /// <summary>
    /// 根据游戏动作获取对应的手柄按钮（兼容旧代码）
    /// </summary>
    /// <param name="action">游戏动作</param>
    /// <returns>对应的手柄按钮，如果没有映射或使用扳机则返回默认值</returns>
    public Xbox360Button GetButton(GIActions action)
    {
        var mapping = GetButtonMapping(action);
        if (mapping == null || mapping.IsTrigger)
        {
            return default(Xbox360Button);
        }
        return mapping.Button;
    }

    /// <summary>
    /// 检查指定的游戏动作是否应该使用摇杆控制
    /// </summary>
    /// <param name="action">游戏动作</param>
    /// <returns>如果应该使用摇杆控制则返回 true</returns>
    public bool IsStickAction(GIActions action)
    {
        return action switch
        {
            GIActions.MoveForward => UseLeftStickForMovement,
            GIActions.MoveBackward => UseLeftStickForMovement,
            GIActions.MoveLeft => UseLeftStickForMovement,
            GIActions.MoveRight => UseLeftStickForMovement,
            _ => false
        };
    }
}
