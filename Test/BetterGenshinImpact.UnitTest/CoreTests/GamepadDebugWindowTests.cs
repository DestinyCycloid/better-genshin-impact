using System;
using System.Threading;
using System.Windows;
using BetterGenshinImpact.View.Windows;
using BetterGenshinImpact.ViewModel.Windows;
using Xunit;

namespace BetterGenshinImpact.UnitTest.CoreTests;

/// <summary>
/// 手柄调试窗口测试
/// </summary>
public class GamepadDebugWindowTests
{
    [Fact]
    [Trait("Category", "UI")]
    public void GamepadDebugViewModel_Initialization_ShouldSucceed()
    {
        // Arrange & Act
        var viewModel = new GamepadDebugViewModel();
        
        // Assert
        Assert.NotNull(viewModel);
        Assert.NotNull(viewModel.ButtonStates);
        Assert.NotEmpty(viewModel.ButtonStates);
        Assert.Equal(14, viewModel.ButtonStates.Count); // 14 个按钮
        Assert.NotNull(viewModel.CurrentMode);
    }
    
    [Fact]
    [Trait("Category", "UI")]
    public void GamepadDebugViewModel_InitialState_ShouldBeZero()
    {
        // Arrange & Act
        var viewModel = new GamepadDebugViewModel();
        
        // Assert
        Assert.Equal(0, viewModel.LeftStickX);
        Assert.Equal(0, viewModel.LeftStickY);
        Assert.Equal(0, viewModel.RightStickX);
        Assert.Equal(0, viewModel.RightStickY);
        Assert.Equal(0, viewModel.LeftTrigger);
        Assert.Equal(0, viewModel.RightTrigger);
    }
    
    [Fact]
    [Trait("Category", "UI")]
    public void GamepadDebugViewModel_ButtonStates_ShouldHaveCorrectNames()
    {
        // Arrange & Act
        var viewModel = new GamepadDebugViewModel();
        
        // Assert
        Assert.Contains(viewModel.ButtonStates, b => b.ButtonName == "A");
        Assert.Contains(viewModel.ButtonStates, b => b.ButtonName == "B");
        Assert.Contains(viewModel.ButtonStates, b => b.ButtonName == "X");
        Assert.Contains(viewModel.ButtonStates, b => b.ButtonName == "Y");
        Assert.Contains(viewModel.ButtonStates, b => b.ButtonName == "LB");
        Assert.Contains(viewModel.ButtonStates, b => b.ButtonName == "RB");
        Assert.Contains(viewModel.ButtonStates, b => b.ButtonName == "Start");
        Assert.Contains(viewModel.ButtonStates, b => b.ButtonName == "Back");
    }
    
    [Fact]
    [Trait("Category", "UI")]
    public void GamepadDebugViewModel_ResetAll_ShouldResetAllStates()
    {
        // Arrange
        var viewModel = new GamepadDebugViewModel();
        
        // 设置一些非零值
        viewModel.LeftStickX = 1000;
        viewModel.LeftStickY = 2000;
        viewModel.RightStickX = 3000;
        viewModel.RightStickY = 4000;
        viewModel.LeftTrigger = 100;
        viewModel.RightTrigger = 200;
        
        foreach (var button in viewModel.ButtonStates)
        {
            button.IsPressed = true;
        }
        
        // Act
        viewModel.ResetAllCommand.Execute(null);
        
        // Assert
        Assert.Equal(0, viewModel.LeftStickX);
        Assert.Equal(0, viewModel.LeftStickY);
        Assert.Equal(0, viewModel.RightStickX);
        Assert.Equal(0, viewModel.RightStickY);
        Assert.Equal(0, viewModel.LeftTrigger);
        Assert.Equal(0, viewModel.RightTrigger);
        
        foreach (var button in viewModel.ButtonStates)
        {
            Assert.False(button.IsPressed);
        }
    }
    
    [Fact]
    [Trait("Category", "UI")]
    public void ButtonStateItem_PropertyChanges_ShouldNotifyObservers()
    {
        // Arrange
        var buttonState = new ButtonStateItem
        {
            ButtonName = "TestButton"
        };
        
        bool propertyChanged = false;
        buttonState.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ButtonStateItem.IsPressed))
            {
                propertyChanged = true;
            }
        };
        
        // Act
        buttonState.IsPressed = true;
        
        // Assert
        Assert.True(propertyChanged);
        Assert.True(buttonState.IsPressed);
    }
}
