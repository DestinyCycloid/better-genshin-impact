using System;
using System.Diagnostics;
using System.IO;
using BetterGenshinImpact.Helpers;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.Core.Simulator;

/// <summary>
/// ViGEmBus 驱动检测和安装辅助类
/// </summary>
public static class ViGEmDriverHelper
{
    private static readonly ILogger _logger = App.GetLogger<InputRouter>();
    
    /// <summary>
    /// 检测 ViGEmBus 驱动是否已安装
    /// </summary>
    /// <returns>驱动是否已安装</returns>
    public static bool IsDriverInstalled()
    {
        try
        {
            _logger.LogDebug("检测 ViGEmBus 驱动安装状态...");
            
            // 尝试创建 ViGEm 客户端来检测驱动
            using var client = new ViGEmClient();
            
            _logger.LogDebug("✓ ViGEmBus 驱动已安装");
            return true;
        }
        catch (VigemBusNotFoundException)
        {
            _logger.LogWarning("ViGEmBus 驱动未安装");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检测 ViGEmBus 驱动时发生错误: {Message}", ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// 显示驱动安装指引
    /// </summary>
    public static void ShowInstallationGuide()
    {
        _logger.LogInformation("显示 ViGEmBus 驱动安装指引");
        
        UIDispatcherHelper.Invoke(() =>
        {
            var message = "检测到 ViGEmBus 驱动未安装\n\n" +
                         "要使用 XInput 手柄模式，需要安装 ViGEmBus 驱动程序。\n\n" +
                         "安装步骤：\n" +
                         "1. 点击「确定」打开下载页面\n" +
                         "2. 下载最新版本的 ViGEmBus_Setup_x64.exe\n" +
                         "3. 运行安装程序并按提示完成安装\n" +
                         "4. 重启应用程序\n\n" +
                         "是否现在打开下载页面？";
            
            var result = System.Windows.MessageBox.Show(
                message,
                "ViGEmBus 驱动未安装",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);
            
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                OpenDownloadPage();
            }
        });
    }
    
    /// <summary>
    /// 打开 ViGEmBus 驱动下载页面
    /// </summary>
    public static void OpenDownloadPage()
    {
        const string downloadUrl = "https://github.com/nefarius/ViGEmBus/releases";
        
        try
        {
            _logger.LogInformation("打开 ViGEmBus 下载页面: {Url}", downloadUrl);
            
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开下载页面失败: {Message}", ex.Message);
            
            UIDispatcherHelper.Invoke(() =>
            {
                Toast.Error($"无法打开浏览器\n请手动访问: {downloadUrl}");
            });
        }
    }
    
    /// <summary>
    /// 检查驱动并在未安装时显示提示
    /// </summary>
    /// <returns>驱动是否已安装</returns>
    public static bool CheckAndPromptInstallation()
    {
        if (IsDriverInstalled())
        {
            return true;
        }
        
        ShowInstallationGuide();
        return false;
    }
}
