using System;
using System.Threading;
using System.Windows;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Service.Interface;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.Core.Simulator;

/// <summary>
/// 输入路由器，负责管理和切换不同的输入输出模式
/// 使用单例模式确保全局只有一个输入路由器实例
/// </summary>
public class InputRouter
{
    private static InputRouter? _instance;
    private static readonly object _lock = new();
    
    private readonly ILogger<InputRouter> _logger = App.GetLogger<InputRouter>();
    private readonly object _switchLock = new();
    
    private IInputOutput? _currentOutput;
    private InputMode _currentMode = InputMode.KeyboardMouse;
    
    /// <summary>
    /// 获取 InputRouter 的单例实例
    /// </summary>
    public static InputRouter Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new InputRouter();
                }
            }
            return _instance;
        }
    }
    
    /// <summary>
    /// 私有构造函数，防止外部实例化
    /// </summary>
    private InputRouter()
    {
        // 从配置读取初始模式
        var configService = App.GetService<IConfigService>();
        var initialMode = configService?.Get()?.InputMode ?? InputMode.KeyboardMouse;
        
        _logger.LogInformation("InputRouter 正在初始化，目标模式: {Mode}", initialMode);
        
        // 根据配置初始化对应的输出
        IInputOutput output;
        try
        {
            output = initialMode switch
            {
                InputMode.XInput => new XInputOutput(),
                _ => new KeyboardMouseOutput()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建 {Mode} 输出时发生错误，回退到键鼠模式", initialMode);
            output = new KeyboardMouseOutput();
            initialMode = InputMode.KeyboardMouse;
        }
        
        // 初始化输出
        if (!output.Initialize())
        {
            _logger.LogWarning("{Mode} 模式初始化失败，回退到键鼠模式", initialMode);
            
            // 清理失败的输出
            try
            {
                output.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理失败的输出时发生错误");
            }
            
            // 回退到键鼠模式
            output = new KeyboardMouseOutput();
            if (!output.Initialize())
            {
                _logger.LogCritical("键鼠模式初始化也失败了，这不应该发生");
                throw new InvalidOperationException("无法初始化任何输入模式");
            }
            initialMode = InputMode.KeyboardMouse;
        }
        
        _currentOutput = output;
        _currentMode = initialMode;
        
        _logger.LogInformation("✓ InputRouter 已初始化为 {Mode} 模式", _currentMode);
    }
    
    /// <summary>
    /// 安全地显示 Toast 消息（在测试环境中不会抛出异常）
    /// </summary>
    private void SafeShowToast(Action toastAction)
    {
        try
        {
            // 检查是否在 WPF 应用程序环境中
            if (Application.Current != null)
            {
                UIDispatcherHelper.Invoke(toastAction);
            }
            else
            {
                // 在测试环境中，只记录日志
                _logger.LogDebug("跳过 Toast 显示（非 UI 环境）");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "显示 Toast 消息时发生错误");
        }
    }
    
    /// <summary>
    /// 切换输入模式
    /// </summary>
    /// <param name="mode">目标输入模式</param>
    /// <returns>切换是否成功</returns>
    public bool SwitchMode(InputMode mode)
    {
        // 如果已经是目标模式，直接返回成功
        if (_currentMode == mode && _currentOutput != null)
        {
            _logger.LogDebug("已经处于 {Mode} 模式，无需切换", mode);
            return true;
        }
        
        // 使用超时锁机制保护模式切换，防止并发问题
        if (!System.Threading.Monitor.TryEnter(_switchLock, TimeSpan.FromSeconds(5)))
        {
            _logger.LogWarning("模式切换超时（5秒），强制执行切换");
            System.Threading.Monitor.Enter(_switchLock);
        }
        
        try
        {
            _logger.LogInformation("正在切换输入模式: {CurrentMode} -> {TargetMode}", _currentMode, mode);
            
            // 释放当前输出的资源
            if (_currentOutput != null)
            {
                _logger.LogDebug("释放当前模式 ({Mode}) 的资源", _currentMode);
                try
                {
                    _currentOutput.Dispose();
                    _logger.LogDebug("✓ 当前模式资源已释放");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "释放 {Mode} 模式资源时发生错误: {Message}", _currentMode, ex.Message);
                }
                _currentOutput = null;
            }
            
            // 创建新的输出实现
            IInputOutput newOutput;
            try
            {
                newOutput = mode switch
                {
                    InputMode.KeyboardMouse => new KeyboardMouseOutput(),
                    InputMode.XInput => new XInputOutput(),
                    _ => throw new ArgumentException($"不支持的输入模式: {mode}", nameof(mode))
                };
                _logger.LogDebug("✓ 新输出实例已创建: {Mode}", mode);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "不支持的输入模式: {Mode}", mode);
                
                SafeShowToast(() =>
                {
                    Toast.Error($"不支持的输入模式: {mode}");
                });
                
                // 回退到键鼠模式
                return FallbackToKeyboardMouse();
            }
            
            // 初始化新输出
            _logger.LogDebug("初始化新模式: {Mode}", mode);
            if (!newOutput.Initialize())
            {
                // 初始化失败，回退到键鼠模式
                _logger.LogWarning("{Mode} 模式初始化失败，回退到键鼠模式", mode);
                
                SafeShowToast(() =>
                {
                    Toast.Warning($"{mode} 模式初始化失败\n已自动切换到键鼠模式");
                });
                
                // 清理失败的输出
                try
                {
                    newOutput.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理失败的输出时发生错误");
                }
                
                // 回退到键鼠模式
                return FallbackToKeyboardMouse();
            }
            
            // 切换成功
            _currentOutput = newOutput;
            _currentMode = mode;
            
            _logger.LogInformation("✓ 输入模式切换成功: {Mode}", mode);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换输入模式时发生未预期的错误: {Message}", ex.Message);
            
            SafeShowToast(() =>
            {
                Toast.Error($"切换输入模式失败: {ex.Message}");
            });
            
            // 尝试回退到键鼠模式
            return FallbackToKeyboardMouse();
        }
        finally
        {
            System.Threading.Monitor.Exit(_switchLock);
        }
    }
    
    /// <summary>
    /// 回退到键鼠模式
    /// </summary>
    /// <returns>回退是否成功</returns>
    private bool FallbackToKeyboardMouse()
    {
        try
        {
            _logger.LogInformation("尝试回退到键鼠模式...");
            
            _currentOutput = new KeyboardMouseOutput();
            if (!_currentOutput.Initialize())
            {
                _logger.LogCritical("键鼠模式初始化也失败了，这不应该发生");
                
                SafeShowToast(() =>
                {
                    Toast.Error("无法初始化任何输入模式\n请重启应用程序");
                });
                
                throw new InvalidOperationException("无法初始化任何输入模式");
            }
            
            _currentMode = InputMode.KeyboardMouse;
            _logger.LogInformation("✓ 已回退到键鼠模式");
            
            return false; // 返回 false 表示原始切换失败
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "回退到键鼠模式失败: {Message}", ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// 获取当前的输入输出实例
    /// </summary>
    /// <returns>当前激活的 IInputOutput 实例</returns>
    public IInputOutput GetOutput()
    {
        if (_currentOutput == null)
        {
            _logger.LogWarning("当前输出为 null，尝试重新初始化键鼠模式");
            
            lock (_switchLock)
            {
                if (_currentOutput == null)
                {
                    try
                    {
                        _currentOutput = new KeyboardMouseOutput();
                        if (!_currentOutput.Initialize())
                        {
                            _logger.LogError("重新初始化键鼠模式失败");
                            throw new InvalidOperationException("无法初始化键鼠模式");
                        }
                        _currentMode = InputMode.KeyboardMouse;
                        _logger.LogInformation("✓ 已重新初始化键鼠模式");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex, "重新初始化键鼠模式时发生错误: {Message}", ex.Message);
                        
                        SafeShowToast(() =>
                        {
                            Toast.Error($"输入系统初始化失败: {ex.Message}");
                        });
                        
                        throw;
                    }
                }
            }
        }
        
        return _currentOutput;
    }
    
    /// <summary>
    /// 获取当前的输入模式
    /// </summary>
    public InputMode CurrentMode => _currentMode;
    
    /// <summary>
    /// 释放所有资源
    /// </summary>
    public void Dispose()
    {
        lock (_switchLock)
        {
            if (_currentOutput != null)
            {
                _logger.LogInformation("释放 InputRouter 资源");
                try
                {
                    _currentOutput.Dispose();
                    _logger.LogDebug("✓ InputRouter 资源已释放");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "释放 InputRouter 资源时发生错误: {Message}", ex.Message);
                }
                _currentOutput = null;
            }
        }
    }
}
