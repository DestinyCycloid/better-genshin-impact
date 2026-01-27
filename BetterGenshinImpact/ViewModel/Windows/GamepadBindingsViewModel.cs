using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Service.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace BetterGenshinImpact.ViewModel.Windows;

public partial class GamepadBindingsViewModel : ObservableObject
{
    public AllConfig Config { get; set; }

    public ObservableCollection<GamepadButtonMapping> AvailableButtons { get; } = new()
    {
        GamepadButtonMapping.FromButton(Xbox360Button.A),
        GamepadButtonMapping.FromButton(Xbox360Button.B),
        GamepadButtonMapping.FromButton(Xbox360Button.X),
        GamepadButtonMapping.FromButton(Xbox360Button.Y),
        GamepadButtonMapping.FromButton(Xbox360Button.LeftShoulder),
        GamepadButtonMapping.FromButton(Xbox360Button.RightShoulder),
        GamepadButtonMapping.FromTrigger(true),  // LT
        GamepadButtonMapping.FromTrigger(false), // RT
        GamepadButtonMapping.FromButton(Xbox360Button.Back),
        GamepadButtonMapping.FromButton(Xbox360Button.Start),
        GamepadButtonMapping.FromButton(Xbox360Button.Guide),
        GamepadButtonMapping.FromButton(Xbox360Button.Up),
        GamepadButtonMapping.FromButton(Xbox360Button.Down),
        GamepadButtonMapping.FromButton(Xbox360Button.Left),
        GamepadButtonMapping.FromButton(Xbox360Button.Right),
        GamepadButtonMapping.FromButton(Xbox360Button.LeftThumb),
        GamepadButtonMapping.FromButton(Xbox360Button.RightThumb)
    };

    // 包装属性，确保每次设置时创建新对象
    public GamepadButtonMapping NormalAttack
    {
        get => Config.GamepadBindingsConfig.NormalAttack;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.NormalAttack = newMapping;
            OnPropertyChanged();
        }
    }

    public GamepadButtonMapping ElementalSkill
    {
        get => Config.GamepadBindingsConfig.ElementalSkill;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.ElementalSkill = newMapping;
            OnPropertyChanged();
        }
    }

    public GamepadButtonMapping ElementalBurst
    {
        get => Config.GamepadBindingsConfig.ElementalBurst;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.ElementalBurst = newMapping;
            OnPropertyChanged();
        }
    }

    public GamepadButtonMapping Jump
    {
        get => Config.GamepadBindingsConfig.Jump;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.Jump = newMapping;
            OnPropertyChanged();
        }
    }

    public GamepadButtonMapping Sprint
    {
        get => Config.GamepadBindingsConfig.Sprint;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.Sprint = newMapping;
            OnPropertyChanged();
        }
    }

    public GamepadButtonMapping PickUpOrInteract
    {
        get => Config.GamepadBindingsConfig.PickUpOrInteract;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.PickUpOrInteract = newMapping;
            OnPropertyChanged();
        }
    }

    public GamepadButtonMapping SwitchMember1
    {
        get => Config.GamepadBindingsConfig.SwitchMember1;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.SwitchMember1 = newMapping;
            OnPropertyChanged();
        }
    }

    public GamepadButtonMapping SwitchMember2
    {
        get => Config.GamepadBindingsConfig.SwitchMember2;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.SwitchMember2 = newMapping;
            OnPropertyChanged();
        }
    }

    public GamepadButtonMapping SwitchMember3
    {
        get => Config.GamepadBindingsConfig.SwitchMember3;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.SwitchMember3 = newMapping;
            OnPropertyChanged();
        }
    }

    public GamepadButtonMapping SwitchMember4
    {
        get => Config.GamepadBindingsConfig.SwitchMember4;
        set
        {
            var newMapping = CloneMapping(value);
            Config.GamepadBindingsConfig.SwitchMember4 = newMapping;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 克隆 GamepadButtonMapping 对象，避免共享引用
    /// </summary>
    private GamepadButtonMapping CloneMapping(GamepadButtonMapping source)
    {
        if (source.IsTrigger)
        {
            return GamepadButtonMapping.FromTrigger(source.IsLeftTrigger);
        }
        else
        {
            return GamepadButtonMapping.FromButton(source.Button);
        }
    }

    public GamepadBindingsViewModel()
    {
        Config = App.GetService<IConfigService>()!.Get();
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        var result = MessageBox.Show(
            "确定要恢复默认按键映射吗？",
            "确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Config.GamepadBindingsConfig.NormalAttack = GamepadButtonMapping.FromButton(Xbox360Button.X);
            Config.GamepadBindingsConfig.ElementalSkill = GamepadButtonMapping.FromButton(Xbox360Button.Y);
            Config.GamepadBindingsConfig.ElementalBurst = GamepadButtonMapping.FromButton(Xbox360Button.B);
            Config.GamepadBindingsConfig.Jump = GamepadButtonMapping.FromButton(Xbox360Button.A);
            Config.GamepadBindingsConfig.Sprint = GamepadButtonMapping.FromButton(Xbox360Button.RightShoulder);
            Config.GamepadBindingsConfig.PickUpOrInteract = GamepadButtonMapping.FromButton(Xbox360Button.X);
            Config.GamepadBindingsConfig.SwitchMember1 = GamepadButtonMapping.FromButton(Xbox360Button.Up);
            Config.GamepadBindingsConfig.SwitchMember2 = GamepadButtonMapping.FromButton(Xbox360Button.Right);
            Config.GamepadBindingsConfig.SwitchMember3 = GamepadButtonMapping.FromButton(Xbox360Button.Down);
            Config.GamepadBindingsConfig.SwitchMember4 = GamepadButtonMapping.FromButton(Xbox360Button.Left);
        }
    }

    [RelayCommand]
    private void CloseWindow()
    {
        Application.Current.Windows
            .OfType<View.Windows.GamepadBindingsWindow>()
            .FirstOrDefault()?.Close();
    }
}
