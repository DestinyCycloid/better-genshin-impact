using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Simulator.Extensions;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace BetterGenshinImpact.UnitTest.CoreTests;

/// <summary>
/// GamepadBindingsConfig 配置类的单元测试
/// </summary>
public class GamepadBindingsConfigTests
{
    [Fact]
    public void GetButton_ShouldReturnCorrectMapping_ForCommonActions()
    {
        // Arrange
        var config = new GamepadBindingsConfig();

        // Act & Assert
        Assert.Equal(Xbox360Button.X, config.NormalAttack.Button);
        Assert.Equal(Xbox360Button.Y, config.ElementalSkill.Button);
        Assert.Equal(Xbox360Button.B, config.ElementalBurst.Button);
        Assert.Equal(Xbox360Button.A, config.Jump.Button);
        Assert.Equal(Xbox360Button.RightShoulder, config.Sprint.Button);
    }

    [Fact]
    public void GetButton_ShouldReturnNone_ForMovementActions()
    {
        // Arrange
        var config = new GamepadBindingsConfig();

        // Act & Assert - 移动动作应该由摇杆控制，不映射到按钮
        Assert.Equal(default(Xbox360Button), config.GetButton(GIActions.MoveForward));
        Assert.Equal(default(Xbox360Button), config.GetButton(GIActions.MoveBackward));
        Assert.Equal(default(Xbox360Button), config.GetButton(GIActions.MoveLeft));
        Assert.Equal(default(Xbox360Button), config.GetButton(GIActions.MoveRight));
    }

    [Fact]
    public void GetButton_ShouldReturnNone_ForUnmappedActions()
    {
        // Arrange
        var config = new GamepadBindingsConfig();

        // Act & Assert - 未映射的动作应该返回 default
        Assert.Equal(default(Xbox360Button), config.GetButton(GIActions.OpenMap));
        Assert.Equal(default(Xbox360Button), config.GetButton(GIActions.OpenCharacterScreen));
    }

    [Fact]
    public void IsStickAction_ShouldReturnTrue_ForMovementActions()
    {
        // Arrange
        var config = new GamepadBindingsConfig
        {
            UseLeftStickForMovement = true
        };

        // Act & Assert
        Assert.True(config.IsStickAction(GIActions.MoveForward));
        Assert.True(config.IsStickAction(GIActions.MoveBackward));
        Assert.True(config.IsStickAction(GIActions.MoveLeft));
        Assert.True(config.IsStickAction(GIActions.MoveRight));
    }

    [Fact]
    public void IsStickAction_ShouldReturnFalse_WhenStickDisabled()
    {
        // Arrange
        var config = new GamepadBindingsConfig
        {
            UseLeftStickForMovement = false
        };

        // Act & Assert
        Assert.False(config.IsStickAction(GIActions.MoveForward));
        Assert.False(config.IsStickAction(GIActions.MoveBackward));
    }

    [Fact]
    public void IsStickAction_ShouldReturnFalse_ForNonMovementActions()
    {
        // Arrange
        var config = new GamepadBindingsConfig();

        // Act & Assert
        Assert.False(config.IsStickAction(GIActions.NormalAttack));
        Assert.False(config.IsStickAction(GIActions.Jump));
    }

    [Fact]
    public void DefaultConfiguration_ShouldHaveReasonableValues()
    {
        // Arrange & Act
        var config = new GamepadBindingsConfig();

        // Assert - 验证默认配置的合理性
        Assert.True(config.UseLeftStickForMovement);
        Assert.True(config.UseRightStickForCamera);
        Assert.Equal(1.0f, config.CameraSensitivity);
        Assert.Equal(0.1f, config.MovementDeadZone);
        Assert.Equal(0.1f, config.CameraDeadZone);
    }

    [Fact]
    public void PropertyChanged_ShouldBeRaised_WhenButtonMappingChanges()
    {
        // Arrange
        var config = new GamepadBindingsConfig();
        var propertyChangedRaised = false;
        config.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(GamepadBindingsConfig.NormalAttack))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        config.NormalAttack = GamepadButtonMapping.FromButton(Xbox360Button.A);

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal(Xbox360Button.A, config.NormalAttack.Button);
    }

    [Fact]
    public void CustomMapping_ShouldBeReflectedInGetButton()
    {
        // Arrange
        var config = new GamepadBindingsConfig
        {
            NormalAttack = GamepadButtonMapping.FromButton(Xbox360Button.B),
            Jump = GamepadButtonMapping.FromButton(Xbox360Button.X)
        };

        // Act & Assert - 自定义映射应该生效
        Assert.Equal(Xbox360Button.B, config.GetButton(GIActions.NormalAttack));
        Assert.Equal(Xbox360Button.X, config.GetButton(GIActions.Jump));
    }
}
