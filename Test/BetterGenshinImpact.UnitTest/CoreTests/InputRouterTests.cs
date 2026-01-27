using BetterGenshinImpact.Core.Simulator;

namespace BetterGenshinImpact.UnitTest.CoreTests;

/// <summary>
/// InputRouter 路由器的单元测试
/// </summary>
public class InputRouterTests
{
    [Fact]
    public void Instance_ShouldReturnSameInstance()
    {
        // Act
        var instance1 = InputRouter.Instance;
        var instance2 = InputRouter.Instance;

        // Assert - 应该返回同一个实例
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithKeyboardMouseMode()
    {
        // Act
        var router = InputRouter.Instance;

        // Assert - 默认应该是键鼠模式
        Assert.Equal(InputMode.KeyboardMouse, router.CurrentMode);
        Assert.NotNull(router.GetOutput());
        Assert.Equal(InputMode.KeyboardMouse, router.GetOutput().Mode);
    }

    [Fact]
    public void SwitchMode_ShouldReturnTrue_WhenAlreadyInTargetMode()
    {
        // Arrange
        var router = InputRouter.Instance;

        // Act - 切换到当前已经是的模式
        var result = router.SwitchMode(InputMode.KeyboardMouse);

        // Assert
        Assert.True(result);
        Assert.Equal(InputMode.KeyboardMouse, router.CurrentMode);
    }

    [Fact]
    public void SwitchMode_ShouldChangeMode_FromKeyboardMouseToXInput()
    {
        // Arrange
        var router = InputRouter.Instance;
        router.SwitchMode(InputMode.KeyboardMouse); // 确保从键鼠模式开始

        // Act - 切换到 XInput 模式
        var result = router.SwitchMode(InputMode.XInput);

        // Assert - 如果驱动已安装应该成功，否则会回退到键鼠模式
        if (result)
        {
            Assert.Equal(InputMode.XInput, router.CurrentMode);
            Assert.Equal(InputMode.XInput, router.GetOutput().Mode);
        }
        else
        {
            // 驱动未安装，应该回退到键鼠模式
            Assert.Equal(InputMode.KeyboardMouse, router.CurrentMode);
        }
    }

    [Fact]
    public void SwitchMode_ShouldFallbackToKeyboardMouse_WhenXInputInitializationFails()
    {
        // Arrange
        var router = InputRouter.Instance;

        // Act - 尝试切换到 XInput（可能失败）
        var result = router.SwitchMode(InputMode.XInput);

        // Assert - 无论如何都应该有一个有效的输出
        Assert.NotNull(router.GetOutput());
        
        // 如果初始化失败，应该回退到键鼠模式
        if (!result)
        {
            Assert.Equal(InputMode.KeyboardMouse, router.CurrentMode);
            Assert.Equal(InputMode.KeyboardMouse, router.GetOutput().Mode);
        }
    }

    [Fact]
    public void GetOutput_ShouldNeverReturnNull()
    {
        // Arrange
        var router = InputRouter.Instance;

        // Act
        var output = router.GetOutput();

        // Assert
        Assert.NotNull(output);
    }

    [Fact]
    public void GetOutput_ShouldReturnCorrectImplementation_ForCurrentMode()
    {
        // Arrange
        var router = InputRouter.Instance;
        router.SwitchMode(InputMode.KeyboardMouse);

        // Act
        var output = router.GetOutput();

        // Assert
        Assert.IsType<KeyboardMouseOutput>(output);
        Assert.Equal(InputMode.KeyboardMouse, output.Mode);
    }

    [Fact]
    public void SwitchMode_ShouldDisposeOldOutput_BeforeCreatingNew()
    {
        // Arrange
        var router = InputRouter.Instance;
        router.SwitchMode(InputMode.KeyboardMouse);
        var oldOutput = router.GetOutput();

        // Act - 切换到另一个模式（即使是同一个模式也会重新创建）
        router.SwitchMode(InputMode.XInput);
        router.SwitchMode(InputMode.KeyboardMouse); // 切换回来

        var newOutput = router.GetOutput();

        // Assert - 应该是不同的实例（旧的已被释放）
        Assert.NotSame(oldOutput, newOutput);
    }

    [Fact]
    public void CurrentMode_ShouldReflectActualMode()
    {
        // Arrange
        var router = InputRouter.Instance;

        // Act & Assert - 键鼠模式
        router.SwitchMode(InputMode.KeyboardMouse);
        Assert.Equal(InputMode.KeyboardMouse, router.CurrentMode);

        // Act & Assert - XInput 模式（或回退到键鼠）
        var switchResult = router.SwitchMode(InputMode.XInput);
        if (switchResult)
        {
            Assert.Equal(InputMode.XInput, router.CurrentMode);
        }
        else
        {
            Assert.Equal(InputMode.KeyboardMouse, router.CurrentMode);
        }
    }

    [Fact]
    public void Dispose_ShouldReleaseCurrentOutput()
    {
        // Arrange
        var router = InputRouter.Instance;
        router.SwitchMode(InputMode.KeyboardMouse);

        // Act
        router.Dispose();

        // Assert - 调用 GetOutput 应该重新初始化
        var output = router.GetOutput();
        Assert.NotNull(output);
        Assert.Equal(InputMode.KeyboardMouse, output.Mode);
    }
}
