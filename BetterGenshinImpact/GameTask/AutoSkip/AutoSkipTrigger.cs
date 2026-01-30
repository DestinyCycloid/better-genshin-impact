using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Recognition;
using BetterGenshinImpact.Core.Recognition.OCR;
using BetterGenshinImpact.Core.Recognition.OpenCv;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.Core.Simulator.Extensions;
using BetterGenshinImpact.GameTask.AutoSkip.Assets;
using BetterGenshinImpact.GameTask.AutoSkip.Model;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Model.Area;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Service;
using BetterGenshinImpact.View.Drawable;
using BetterGenshinImpact.View.Windows;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using BetterGenshinImpact.GameTask.AutoPick.Assets;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using Vanara.PInvoke;
using Region = BetterGenshinImpact.GameTask.Model.Area.Region;

namespace BetterGenshinImpact.GameTask.AutoSkip;

/// <summary>
/// 自动剧情有选项点击
/// </summary>
public partial class AutoSkipTrigger : ITaskTrigger
{
    private readonly ILogger<AutoSkipTrigger> _logger = App.GetLogger<AutoSkipTrigger>();

    public string Name => "自动剧情";
    public bool IsEnabled { get; set; }
    public int Priority => 20;
    public bool IsExclusive => false;
    
    // 改回 Talk，让触发器在对话界面时运行
    public GameUiCategory SupportedGameUiCategory => GameUiCategory.Talk;


    public bool IsBackgroundRunning { get; private set; }
    
    public bool UseBackgroundOperation { get; private set; }

    public bool IsUseInteractionKey { get; set; } = false;

    private readonly AutoSkipAssets _autoSkipAssets;

    private readonly AutoSkipConfig _config;

    /// <summary>
    /// 不自动点击的选项，优先级低于橙色文字点击
    /// </summary>
    private List<string> _defaultPauseList = [];

    /// <summary>
    /// 不自动点击的选项
    /// </summary>
    private List<string> _pauseList = [];

    /// <summary>
    /// 优先自动点击的选项
    /// </summary>
    private List<string> _selectList = [];

    private PostMessageSimulator? _postMessageSimulator;
    
    private readonly bool _isCustomConfiguration;

    /// <summary>
    /// 辅助方法：模拟手柄按键按下并松开
    /// </summary>
    /// <param name="button">手柄按钮</param>
    /// <param name="delayMs">按下后延迟的毫秒数（默认50ms）</param>
    private void GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button button, int delayMs = 50)
    {
        Simulation.SetGamepadButtonDown(button);
        Thread.Sleep(delayMs);
        Simulation.SetGamepadButtonUp(button);
    }

    public AutoSkipTrigger()
    {
        _autoSkipAssets = AutoSkipAssets.Instance;
        _config = TaskContext.Instance().Config.AutoSkipConfig;
    }
    
    /// <summary>
    /// 用于内部的其他方法调用
    /// </summary>
    /// <param name="config"></param>
    public AutoSkipTrigger(AutoSkipConfig config)
    {
        _autoSkipAssets = AutoSkipAssets.Instance;
        _config = config;
        _isCustomConfiguration = true;
    }

    public void Init()
    {
        IsEnabled = _config.Enabled;
        IsBackgroundRunning = _config.RunBackgroundEnabled;
        // IsUseInteractionKey = _config.SelectChatOptionType == SelectChatOptionTypes.UseInteractionKey;
        _postMessageSimulator = TaskContext.Instance().PostMessageSimulator;

        if (!_isCustomConfiguration)
        {
            InitKeyword();
        }
    }

    private void InitKeyword()
    {
        try
        {
            var defaultPauseListJson = Global.ReadAllTextIfExist(@"Assets\Config\Skip\default_pause_options.json");
            if (!string.IsNullOrEmpty(defaultPauseListJson))
            {
                _defaultPauseList = JsonSerializer.Deserialize<List<string>>(defaultPauseListJson, ConfigService.JsonOptions) ?? [];
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "读取自动剧情默认暂停点击关键词列表失败");
            ThemedMessageBox.Error("读取自动剧情默认暂停点击关键词列表失败，请确认修改后的自动剧情默认暂停点击关键词内容格式是否正确！");
        }

        try
        {
            var pauseListJson = Global.ReadAllTextIfExist(@"Assets\Config\Skip\pause_options.json");
            if (!string.IsNullOrEmpty(pauseListJson))
            {
                _pauseList = JsonSerializer.Deserialize<List<string>>(pauseListJson, ConfigService.JsonOptions) ?? [];
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "读取自动剧情暂停点击关键词列表失败");
            ThemedMessageBox.Error("读取自动剧情暂停点击关键词列表失败，请确认修改后的自动剧情暂停点击关键词内容格式是否正确！");
        }

        try
        {
            var selectListJson = Global.ReadAllTextIfExist(@"Assets\Config\Skip\select_options.json");
            if (!string.IsNullOrEmpty(selectListJson))
            {
                _selectList = JsonSerializer.Deserialize<List<string>>(selectListJson, ConfigService.JsonOptions) ?? [];
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "读取自动剧情优先点击选项列表失败");
            ThemedMessageBox.Error("读取自动剧情优先点击选项列表失败，请确认修改后的自动剧情优先点击选项内容格式是否正确！");
        }
    }

    /// <summary>
    /// 上一次播放中的帧
    /// </summary>
    private DateTime _prevPlayingTime = DateTime.MinValue;

    private DateTime _prevExecute = DateTime.MinValue;
    private DateTime _prevHangoutExecute = DateTime.MinValue;

    private DateTime _prevGetDailyRewardsTime = DateTime.MinValue;

    private DateTime _prevClickTime = DateTime.MinValue;

    public void OnCapture(CaptureContent content)
    {
        if ((DateTime.Now - _prevExecute).TotalMilliseconds <= 200)
        {
            return;
        }
        UseBackgroundOperation = IsBackgroundRunning && !SystemControl.IsGenshinImpactActive();

        _prevExecute = DateTime.Now;

        GetDailyRewardsEsc(_config, content);

        // 找左上角剧情自动的按钮

        var isPlaying = content.CurrentGameUiCategory == GameUiCategory.Talk
                        || Bv.IsInTalkUi(content.CaptureRectArea); // 播放中
        
        // 如果没有识别到对话界面，尝试识别对话选项气泡
        if (!isPlaying)
        {
            // 根据输入模式选择不同的识别对象
            var optionIconRo = Simulation.CurrentInputMode == InputMode.XInput 
                ? _autoSkipAssets.OptionIconGamepadRo 
                : _autoSkipAssets.OptionIconRo;
            
            using var optionIconRa = content.CaptureRectArea.Find(optionIconRo);
            if (optionIconRa.IsExist())
            {
                isPlaying = true;
                _logger.LogInformation("✅ 识别到对话选项（手柄模式）");
            }
        }

        if (!isPlaying && (DateTime.Now - _prevPlayingTime).TotalSeconds <= 5)
        {
            // 关闭弹出页
            if (_config.ClosePopupPagedEnabled)
            {
                ClosePopupPage(content);
                CloseItemPopup(content);
                CloseCharacterPopup(content);
            }

            // 自动剧情点击3s内判断
            if ((DateTime.Now - _prevPlayingTime).TotalMilliseconds < 3000)
            {
                if (!TaskContext.Instance().Config.AutoSkipConfig.SubmitGoodsEnabled)
                {
                    return;
                }

                // 提交物品
                if (SubmitGoods(content))
                {
                    return;
                }
            }
        }

        if (isPlaying)
        {
            _prevPlayingTime = DateTime.Now;
            
            // 先检查对话选项，如果有对话选项就不要按A键跳过对话
            bool hasOption;
            if (UseBackgroundOperation || IsUseInteractionKey)
            {
                hasOption = ChatOptionChooseUseKey(content.CaptureRectArea);
            }
            else
            {
                hasOption = ChatOptionChoose(content.CaptureRectArea);
            }
            
            // 只有在没有对话选项时才按A键跳过对话
            if (!hasOption && TaskContext.Instance().Config.AutoSkipConfig.QuicklySkipConversationsEnabled)
            {
                if (_config.BeforeClickConfirmDelay > 0)
                {
                    // 在触发点击动作之前延迟时间
                    Thread.Sleep(_config.BeforeClickConfirmDelay);
                }
                if (IsUseInteractionKey)
                {
                    _postMessageSimulator? .SimulateActionBackground(GIActions.PickUpOrInteract); // 注意这里不是交互键 NOTE By Ayu0K: 这里确实是交互键
                }
                else
                {
                    // 根据当前输入模式选择按键：键盘模式按空格，手柄模式按A键
                    if (Simulation.CurrentInputMode == InputMode.XInput)
                    {
                        GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A);
                    }
                    else
                    {
                        _postMessageSimulator?.KeyPressBackground(User32.VK.VK_SPACE);
                    }
                }
            }


            // 邀约选项选择 1s 1次
            if (_config.AutoHangoutEventEnabled && !hasOption)
            {
                if ((DateTime.Now - _prevHangoutExecute).TotalMilliseconds < 1200)
                {
                    return;
                }

                _prevHangoutExecute = DateTime.Now;
                HangoutOptionChoose(content.CaptureRectArea);
            }
        }
        else
        {
            ClickBlackGameScreen(content);
        }
    }

    /// <summary>
    /// 黑屏点击判断
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    private bool ClickBlackGameScreen(CaptureContent content)
    {
        // 黑屏剧情要点击鼠标（多次） 几乎全黑的时候不用点击
        if ((DateTime.Now - _prevClickTime).TotalMilliseconds > 1200)
        {
            using var grayMat = new Mat(content.CaptureRectArea.CacheGreyMat, new Rect(0, content.CaptureRectArea.CacheGreyMat.Height / 3, content.CaptureRectArea.CacheGreyMat.Width, content.CaptureRectArea.CacheGreyMat.Height / 3));
            var blackCount = OpenCvCommonHelper.CountGrayMatColor(grayMat, 0);
            var rate = blackCount * 1d / (grayMat.Width * grayMat.Height);
            if (rate is >= 0.5 and < 0.98999)
            {
                if (UseBackgroundOperation)
                {
                    TaskContext.Instance().PostMessageSimulator?.LeftButtonClickBackground();
                }
                else
                {
                    Simulation.SendInput.Mouse.LeftButtonClick();
                }

                _logger.LogInformation("自动剧情：{Text} 比例 {Rate}", "点击黑屏", rate.ToString("F"));

                _prevClickTime = DateTime.Now;
                return true;
            }
        }

        return false;
    }

    private void HangoutOptionChoose(ImageRegion captureRegion)
    {
        var selectedRects = captureRegion.FindMulti(_autoSkipAssets.HangoutSelectedRo);
        var unselectedRects = captureRegion.FindMulti(_autoSkipAssets.HangoutUnselectedRo);
        if (selectedRects.Count > 0 || unselectedRects.Count > 0)
        {
            List<HangoutOption> hangoutOptionList =
            [
                .. selectedRects.Select(selectedRect => new HangoutOption(selectedRect, true)),
                .. unselectedRects.Select(unselectedRect => new HangoutOption(unselectedRect, false)),
            ];
            // 只有一个选项直接点击
            // if (hangoutOptionList.Count == 1)
            // {
            //     hangoutOptionList[0].Click(clickOffset);
            //     AutoHangoutSkipLog("点击唯一邀约选项");
            //     return;
            // }

            hangoutOptionList = hangoutOptionList.Where(hangoutOption => hangoutOption.TextRect != null).ToList();
            if (hangoutOptionList.Count == 0)
            {
                return;
            }

            // OCR识别选项文字
            foreach (var hangoutOption in hangoutOptionList)
            {
                var text = OcrFactory.Paddle.Ocr(hangoutOption.TextRect!.SrcMat);
                hangoutOption.OptionTextSrc = StringUtils.RemoveAllEnter(text);
            }

            // 优先选择分支选项
            if (!string.IsNullOrEmpty(_config.AutoHangoutEndChoose))
            {
                var chooseList = HangoutConfig.Instance.HangoutOptions[_config.AutoHangoutEndChoose];
                foreach (var hangoutOption in hangoutOptionList)
                {
                    foreach (var str in chooseList)
                    {
                        if (hangoutOption.OptionTextSrc.Contains(str))
                        {
                            HangoutOptionClick(hangoutOption);
                            _logger.LogInformation("邀约分支[{Text}]关键词[{Str}]命中", _config.AutoHangoutEndChoose, str);
                            AutoHangoutSkipLog(hangoutOption.OptionTextSrc);
                            VisionContext.Instance().DrawContent.RemoveRect("HangoutSelected");
                            VisionContext.Instance().DrawContent.RemoveRect("HangoutUnselected");
                            return;
                        }
                    }
                }
            }

            // 没有停留的选项 优先选择未点击的的选项
            foreach (var hangoutOption in hangoutOptionList)
            {
                if (!hangoutOption.IsSelected)
                {
                    HangoutOptionClick(hangoutOption);
                    AutoHangoutSkipLog(hangoutOption.OptionTextSrc);
                    VisionContext.Instance().DrawContent.RemoveRect("HangoutSelected");
                    VisionContext.Instance().DrawContent.RemoveRect("HangoutUnselected");
                    return;
                }
            }

            // 没有未点击的选项 选择第一个已点击选项
            HangoutOptionClick(hangoutOptionList[0]);
            AutoHangoutSkipLog(hangoutOptionList[0].OptionTextSrc);
            VisionContext.Instance().DrawContent.RemoveRect("HangoutSelected");
            VisionContext.Instance().DrawContent.RemoveRect("HangoutUnselected");
        }
        else
        {
            // 没有邀约选项 寻找跳过按钮
            if (_config.AutoHangoutPressSkipEnabled)
            {
                using var skipRa = captureRegion.Find(_autoSkipAssets.HangoutSkipRo);
                if (skipRa.IsExist())
                {
                    if (UseBackgroundOperation && !SystemControl.IsGenshinImpactActive())
                    {
                        skipRa.BackgroundClick();
                    }
                    else
                    {
                        skipRa.Click();
                    }

                    AutoHangoutSkipLog("点击跳过按钮");
                }
            }
        }
    }

    private bool IsOrangeOption(Mat textMat)
    {
        // 只提取橙色
        // Cv2.ImWrite($"log/text{DateTime.Now:yyyyMMddHHmmssffff}.png", textMat);
        using var bMat = OpenCvCommonHelper.Threshold(textMat, new Scalar(243, 195, 48), new Scalar(255, 205, 55));
        var whiteCount = OpenCvCommonHelper.CountGrayMatColor(bMat, 255);
        var rate = whiteCount * 1.0 / (bMat.Width * bMat.Height);
        Debug.WriteLine($"识别到橙色文字区域占比:{rate}");
        if (rate > 0.06)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 领取每日委托奖励 后 10s 寻找原石是否出现，出现则点击(960, 900)坐标处
    /// </summary>
    private void GetDailyRewardsEsc(AutoSkipConfig config, CaptureContent content)
    {
        if (!config.AutoGetDailyRewardsEnabled)
        {
            return;
        }

        if ((DateTime.Now - _prevGetDailyRewardsTime).TotalSeconds > 10)
        {
            return;
        }

        content.CaptureRectArea.Find(_autoSkipAssets.PrimogemRo, primogemRa =>
        {
            Thread.Sleep(100);
            // 根据当前输入模式选择按键：键盘模式按ESC，手柄模式按B键
            if (Simulation.CurrentInputMode == InputMode.XInput)
            {
                GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.B);
            }
            else
            {
                Simulation.SendInput.Keyboard.KeyPress(User32.VK.VK_ESCAPE);
            }
            _prevGetDailyRewardsTime = DateTime.MinValue;
            primogemRa.Dispose();
        });
    }

    [GeneratedRegex(@"^[a-zA-Z0-9]+$")]
    private static partial Regex EnOrNumRegex();

    /// <summary>
    /// 5.2 版本直接交互键就能使用的对话选择
    /// </summary>
    /// <param name="region"></param>
    /// <returns></returns>
    private bool ChatOptionChooseUseKey(ImageRegion region)
    {
        if (_config.IsClickNoneChatOption())
        {
            return false;
        }
        
        // 根据输入模式选择不同的识别对象
        var optionIconRo = Simulation.CurrentInputMode == InputMode.XInput 
            ? _autoSkipAssets.OptionIconGamepadRo 
            : _autoSkipAssets.OptionIconRo;
        
        using var chatOptionResult = region.Find(optionIconRo);
        var isInChat = false;
        isInChat = chatOptionResult.IsExist();
        if (!isInChat)
        {
            using var pickRa = region.Find(AutoPickAssets.Instance.ChatPickRo);
            isInChat = pickRa.IsExist();
        }

        if (isInChat)
        {
            var fKey = AutoPickAssets.Instance.PickVk;
            if (_config.IsClickFirstChatOption())
            {
                // 根据当前输入模式选择按键
                if (Simulation.CurrentInputMode == InputMode.XInput)
                {
                    GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A);
                }
                else
                {
                    _postMessageSimulator?.KeyPressBackground(fKey);
                }
            }
            else if (_config.IsClickRandomChatOption())
            {
                var random = new Random();
                // 随机 0~4 的数字
                var r = random.Next(0, 5);
                
                if (Simulation.CurrentInputMode == InputMode.XInput)
                {
                    // 手柄模式：使用方向键下
                    for (var j = 0; j < r; j++)
                    {
                        GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Down, 100);
                    }
                    Thread.Sleep(50);
                    GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A);
                }
                else
                {
                    // 键盘模式：使用S键
                    for (var j = 0; j < r; j++)
                    {
                        _postMessageSimulator?.KeyPressBackground(User32.VK.VK_S);
                        Thread.Sleep(100);
                    }
                    Thread.Sleep(50);
                    _postMessageSimulator?.KeyPressBackground(fKey);
                }
            }
            else
            {
                if (Simulation.CurrentInputMode == InputMode.XInput)
                {
                    // 手柄模式：使用方向键上
                    GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Up, 100);
                    GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A);
                }
                else
                {
                    // 键盘模式：使用W键
                    _postMessageSimulator?.KeyPressBackground(User32.VK.VK_W);
                    Thread.Sleep(100);
                    _postMessageSimulator?.KeyPressBackground(fKey);
                }
            }
            
            AutoSkipLog("交互键点击(后台)");

            return true;
        }

        return false;
    }

    /// <summary>
    /// 新的对话选项选择
    ///
    /// 返回 true 表示存在对话选项，但是不一定点击了
    /// </summary>
    private bool ChatOptionChoose(ImageRegion region)
    {
        var assetScale = TaskContext.Instance().SystemInfo.AssetScale;
        
        if (!_config.IsClickNoneChatOption())
        {
            // 感叹号识别 遇到直接点击
            using var exclamationIconRa = region.Find(_autoSkipAssets.ExclamationIconRo);
            if (!exclamationIconRa.IsEmpty())
            {
                Thread.Sleep(_config.AfterChooseOptionSleepDelay);
                exclamationIconRa.Click();
                AutoSkipLog("点击感叹号选项");
                return true;
            }
        }

        // 气泡识别 - 根据输入模式选择不同的识别对象
        var optionIconRoForMulti = Simulation.CurrentInputMode == InputMode.XInput 
            ? _autoSkipAssets.OptionIconGamepadRo 
            : _autoSkipAssets.OptionIconRo;
        
        var chatOptionResultList = region.FindMulti(optionIconRoForMulti);
        
        if (chatOptionResultList.Count > 0)
        {
            // 第一个元素就是最下面的
            chatOptionResultList = [.. chatOptionResultList.OrderByDescending(r => r.Y)];

            // 通过最下面的气泡框来文字识别
            var lowest = chatOptionResultList[0];
            var highest = chatOptionResultList[^1];
            
            // OCR区域：从最上面的气泡到最下面的气泡，再往下延伸一些
            var ocrRect = new Rect(
                (int)(lowest.X + lowest.Width + 8 * assetScale), 
                (int)(highest.Y - 10 * assetScale),  // 从最上面气泡开始，稍微往上一点
                (int)(535 * assetScale), 
                (int)(lowest.Y + lowest.Height + 100 * assetScale - (highest.Y - 10 * assetScale))  // 到最下面气泡结束，再往下延伸100像素
            );
            
            var ocrResList = region.FindMulti(new RecognitionObject
            {
                RecognitionType = RecognitionTypes.Ocr,
                RegionOfInterest = ocrRect
            });
            //using var ocrMat = new Mat(region.SrcGreyMat, ocrRect);
            //// Cv2.ImWrite("log/ocrMat.png", ocrMat);
            //var ocrRes = OcrFactory.Paddle.OcrResult(ocrMat);

            // 删除为空的结果 和 纯英文的结果
            var rs = new List<Region>();
            // 按照y坐标排序
            ocrResList = [.. ocrResList.OrderBy(r => r.Y)];
            
            for (var i = 0; i < ocrResList.Count; i++)
            {
                var item = ocrResList[i];
                if (string.IsNullOrEmpty(item.Text) || (item.Text.Length < 5 && EnOrNumRegex().IsMatch(item.Text)))
                {
                    continue;
                }

                if (i != ocrResList.Count - 1)
                {
                    if (ocrResList[i + 1].Y - ocrResList[i].Y > 150)
                    {
                        Debug.WriteLine($"存在Y轴偏差过大的结果，忽略:{item.Text}");
                        continue;
                    }
                }

                rs.Add(item);
            }

            if (rs.Count > 0)
            {
                // 自定义优先选项匹配
                if (_config.CustomPriorityOptionsEnabled && !string.IsNullOrEmpty(_config.CustomPriorityOptions))  
                {  
                    var customOptions = _config.CustomPriorityOptions  
                        .Split(new[] { '\r', '\n', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)  
                        .Select(s => s.Trim())  
                        .Where(s => !string.IsNullOrEmpty(s))  
                        .ToList();  
      
                    foreach (var item in rs)  
                    {
                        foreach (var customOption in customOptions)  
                        {
                            if (item.Text.Contains(customOption))  
                            {
                                ClickChatOption(rs, item);
                                return true;  
                            }  
                        }  
                    }  
                }
                
                if(_config.IsClickNoneChatOption()){
                    return false;
                }
                
                
                if (!_config.SkipBuiltInClickOptions)
                {
                    // 内置关键词 匹配
                    foreach (var item in rs)
                    {
                        // 选择关键词
                        if (_selectList.Any(s => item.Text.Contains(s)))
                        {
                            ClickChatOption(rs, item);
                            return true;
                        }

                        // 不选择关键词
                        if (_pauseList.Any(s => item.Text.Contains(s)))
                        {
                            return true;
                        }
                    }

                    // 橙色选项
                    foreach (var item in rs)
                    {
                        var textMat = item.ToImageRegion().SrcMat;
                        if (IsOrangeOption(textMat))
                        {
                            if (_config.AutoGetDailyRewardsEnabled && (item.Text.Contains("每日") || item.Text.Contains("委托")))
                            {
                                ClickChatOption(rs, item, "每日委托");
                                TaskControl.Sleep(800);
                                
                                // 6.2 每日提示确认
                                var ra1 = TaskControl.CaptureToRectArea();
                                if (Bv.ClickBlackConfirmButton(ra1))
                                {
                                    _logger.LogInformation("存在提示并确认");
                                }
                                ra1.Dispose();
                                
                                _prevGetDailyRewardsTime = DateTime.Now; // 记录领取时间
                            }
                            else if (_config.AutoReExploreEnabled && (item.Text.Contains("探索") || item.Text.Contains("派遣")))
                            {
                                ClickChatOption(rs, item, "探索派遣");
                                Thread.Sleep(800); // 等待探索派遣界面打开
                                new OneKeyExpeditionTask().Run(_autoSkipAssets);
                            }
                            else if (!item.Text.Contains("每日")
                                && !item.Text.Contains("委托")
                                && !item.Text.Contains("探索")
                                && !item.Text.Contains("派遣"))
                            {
                                ClickChatOption(rs, item);
                            }

                            return true;
                        }
                    }

                    // 默认不选择关键词
                    foreach (var item in rs)
                    {
                        // 不选择关键词
                        if (_defaultPauseList.Any(s => item.Text.Contains(s)))
                        {
                            return true;
                        }
                    }
                }

                // 最后，选择默认选项
                var clickRegion = rs[^1];
                if (_config.IsClickFirstChatOption())
                {
                    clickRegion = rs[0];
                }
                else if (_config.IsClickRandomChatOption())
                {
                    var random = new Random();
                    clickRegion = rs[random.Next(0, rs.Count)];
                }

                ClickChatOption(rs, clickRegion);
                AutoSkipLog(clickRegion.Text);
            }
            else
            {
                var clickRect = lowest;
                if (_config.IsClickFirstChatOption())
                {
                    clickRect = chatOptionResultList[^1];
                }

                // 没OCR到文字，直接选择气泡选项
                // 手柄模式：使用方向键+A键
                if (Simulation.CurrentInputMode == InputMode.XInput)
                {
                    var targetIndex = chatOptionResultList.IndexOf(clickRect);
                    
                    // 如果目标是第一个选项，直接按A键（游戏默认选中第一个）
                    if (targetIndex == 0)
                    {
                        GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A);
                    }
                    else
                    {
                        // 按方向键下移动到目标选项（不需要先按上，游戏默认在第一个）
                        for (var i = 0; i < targetIndex; i++)
                        {
                            GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Down, 100);
                            Thread.Sleep(50);
                        }
                        
                        // 按A键确认选择
                        Thread.Sleep(100);
                        GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A);
                    }
                }
                // 键盘模式：直接点击
                else
                {
                    Thread.Sleep(_config.AfterChooseOptionSleepDelay);
                    ClickOcrRegion(clickRect);
                }
                
                var msg = _config.IsClickFirstChatOption() ? "第一个" : "最后一个";
                AutoSkipLog($"点击{msg}气泡选项");
            }

            return true;
        }
        else
        {
            // 没有气泡的时候识别 F 选项
            using var pickRa = region.Find(AutoPickAssets.Instance.ChatPickRo);
            if (pickRa.IsExist())
            {
                // 根据当前输入模式选择按键
                if (Simulation.CurrentInputMode == InputMode.XInput)
                {
                    GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Y);
                }
                else
                {
                    _postMessageSimulator?.KeyPressBackground(AutoPickAssets.Instance.PickVk);
                }
                AutoSkipLog("无气泡图标，但存在交互键，直接按下交互键");
            }
        }

        return false;
    }

    private void ClickOcrRegion(Region region, string optionType = "")
    {
        if (string.IsNullOrEmpty(optionType))
        {
            Thread.Sleep(_config.AfterChooseOptionSleepDelay);
        }

        if (UseBackgroundOperation && !SystemControl.IsGenshinImpactActive())
        {
            region.BackgroundClick();
        }
        else
        {
            region.Click();
        }

        AutoSkipLog(region.Text);
    }

    /// <summary>
    /// 点击对话选项（支持手柄模式和键盘模式）
    /// </summary>
    /// <param name="allOptions">所有对话选项列表（从上到下排序）</param>
    /// <param name="targetOption">要点击的目标选项</param>
    /// <param name="optionType">选项类型（用于日志）</param>
    private void ClickChatOption(List<Region> allOptions, Region targetOption, string optionType = "")
    {
        if (string.IsNullOrEmpty(optionType))
        {
            Thread.Sleep(_config.AfterChooseOptionSleepDelay);
        }

        // 手柄模式：使用方向键+A键选择
        if (Simulation.CurrentInputMode == InputMode.XInput)
        {
            // 找到目标选项在列表中的索引
            var targetIndex = allOptions.IndexOf(targetOption);
            if (targetIndex == -1)
            {
                _logger.LogWarning("未找到目标选项在列表中的位置");
                return;
            }

            // 判断是第一个还是最后一个
            var positionText = targetIndex == 0 ? "第一个" : (targetIndex == allOptions.Count - 1 ? "最后一个" : $"第{targetIndex + 1}个");

            // 如果目标是第一个选项，直接按A键（游戏默认选中第一个）
            if (targetIndex == 0)
            {
                _logger.LogInformation($"🎮 手柄模式：按A键选择{positionText}选项：{targetOption.Text}");
                GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A);
            }
            else
            {
                // 按方向键下移动到目标选项（不需要先按上，游戏默认在第一个）
                _logger.LogInformation($"🎮 手柄模式：按方向键下{targetIndex}次，调整到{positionText}选项");
                for (var i = 0; i < targetIndex; i++)
                {
                    GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Down, 100);
                    Thread.Sleep(50);
                }

                // 按A键确认选择
                Thread.Sleep(100);
                _logger.LogInformation($"🎮 手柄模式：按A键选择选项：{targetOption.Text}");
                GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A);
            }
        }
        // 键盘模式：直接点击
        else
        {
            if (UseBackgroundOperation && !SystemControl.IsGenshinImpactActive())
            {
                targetOption.BackgroundClick();
            }
            else
            {
                targetOption.Click();
            }
        }

        AutoSkipLog(targetOption.Text);
    }

    private void HangoutOptionClick(HangoutOption option)
    {
        if (_config.AutoHangoutChooseOptionSleepDelay > 0)
        {
            Thread.Sleep(_config.AutoHangoutChooseOptionSleepDelay);
        }

        if (UseBackgroundOperation && !SystemControl.IsGenshinImpactActive())
        {
            option.BackgroundClick();
        }
        else
        {
            option.Click();
        }
    }

    private void AutoHangoutSkipLog(string text)
    {
        if ((DateTime.Now - _prevClickTime).TotalMilliseconds > 1000)
        {
            _logger.LogInformation("自动邀约：{Text}", text);
        }

        _prevClickTime = DateTime.Now;
    }

    private void AutoSkipLog(string text)
    {
        if (text.Contains("每日委托") || text.Contains("探索派遣"))
        {
            _logger.LogInformation("自动剧情：{Text}", text);
        }
        else if ((DateTime.Now - _prevClickTime).TotalMilliseconds > 1000)
        {
            _logger.LogInformation("自动剧情：{Text}", text);
        }

        _prevClickTime = DateTime.Now;
    }

    /// <summary>
    /// 关闭弹出页
    /// </summary>
    /// <param name="content"></param>
    private void ClosePopupPage(CaptureContent content)
    {
        if (!_config.ClosePopupPagedEnabled)
        {
            return;
        }
        
        content.CaptureRectArea.Find(_autoSkipAssets.PageCloseRo, pageCloseRoRa =>
        {
            if (!Bv.IsInBigMapUi(content.CaptureRectArea))
            {
                // 根据当前输入模式选择按键：键盘模式按ESC，手柄模式按B键
                if (Simulation.CurrentInputMode == InputMode.XInput)
                {
                    GamepadButtonPress(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.B);
                }
                else
                {
                    TaskContext.Instance().PostMessageSimulator.KeyPress(User32.VK.VK_ESCAPE);
                }

                AutoSkipLog("关闭弹出页");
                pageCloseRoRa.Dispose();
            }
        });
    }
    
    private DateTime _prevCloseItemTime = DateTime.MinValue;
    /// <summary>
    /// 关闭剧情中弹出的道具页面
    /// </summary>
    /// <param name="content"></param>
    private void CloseItemPopup(CaptureContent content)
    {
        if ((DateTime.Now - _prevCloseItemTime).TotalMilliseconds < 1000)
        {
            return; 
        }
        
        if (Bv.IsInMainUi(content.CaptureRectArea))  
        {  
            return;  
        }  
        //屏幕底部中间，实心三角的位置
        var scale = TaskContext.Instance().SystemInfo.AssetScale;
        using var croppedRegion = content.CaptureRectArea.DeriveCrop(900 * scale, 960 * scale, 120 * scale, 120 * scale);

        using var hsv = new Mat();
        Cv2.CvtColor(croppedRegion.SrcMat, hsv, ColorConversionCodes.BGR2HSV);

        using var yellowMask = new Mat();
        using var buleMask = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 222, 173), new Scalar(33, 255, 255), yellowMask);
        Cv2.InRange(hsv, new Scalar(87, 131, 142), new Scalar(124, 255, 255), buleMask);  //活动玩法介绍会有出现蓝色三角，但不一定在对话流程中出现，先加上

        Cv2.FindContours(yellowMask, out var yellowContours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        Cv2.FindContours(buleMask, out var buleMaskContours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var mergedContours = yellowContours.Concat(buleMaskContours).ToArray();
        foreach (var contour in mergedContours)
        {
            var area = Cv2.ContourArea(contour);
            var approx = Cv2.ApproxPolyDP(contour, 0.04 * Cv2.ArcLength(contour, true), true);
            
            if (area < 10 || area > 50 || approx.Length != 3) continue; 

            if (UseBackgroundOperation && !SystemControl.IsGenshinImpactActive())
            {
                croppedRegion.Derive(Cv2.BoundingRect(approx)).BackgroundClick();
            }
            else
            {
                croppedRegion.Derive(Cv2.BoundingRect(approx)).Click();
            }
            _prevCloseItemTime = DateTime.Now;
            _logger.LogInformation("自动剧情：{Text} 面积 {Area}", "点击底部三角形",area);
            return;
        }
    }

    /// <summary>
    /// 关闭剧情中弹出的初见角色信息弹窗
    /// </summary>
    /// <param name="content"></param>
    private void CloseCharacterPopup(CaptureContent content)
    {
        using var srcMat = content.CaptureRectArea.SrcMat.Clone();
        var scale = TaskContext.Instance().SystemInfo.AssetScale;
        // 把被角色头像遮挡的矩形闭合（假设矩形存在）
        Cv2.Rectangle(srcMat, new Rect((int)(240 * scale), (int)(395 * scale), (int)(300 * scale), (int)(50 * scale)), new Scalar(229, 241, 245), -1);
        Cv2.Rectangle(srcMat, new Rect((int)(290 * scale), (int)(660 * scale), (int)(210 * scale), (int)(40 * scale)), new Scalar(101, 82, 74), -1);
        
        using var hsv = new Mat();
        Cv2.CvtColor(srcMat, hsv, ColorConversionCodes.BGR2HSV);

        // 颜色阈值分割 - 背景色中的黄跟藏青
        using var maskLight = new Mat();
        using var maskDark = new Mat();
        Cv2.InRange(hsv, new Scalar(18, 16, 234), new Scalar(27, 19, 250), maskLight);
        Cv2.InRange(hsv, new Scalar(101, 57, 95), new Scalar(118, 85, 106), maskDark);

        // 合并掩码并进行形态学操作 - 减少背景中的噪点
        using var combinedMask = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(21, 21));
        Cv2.BitwiseOr(maskLight, maskDark, combinedMask);
        Cv2.MorphologyEx(combinedMask, combinedMask, MorphTypes.Close, kernel);
        Cv2.MorphologyEx(combinedMask, combinedMask, MorphTypes.Open, kernel);

        // 查找轮廓  
        Cv2.FindContours(combinedMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var imgHeight = srcMat.Height;
        var imgWidth = srcMat.Width;

        // 筛选弹窗轮廓 
        foreach (var contour in contours)
        {
            var bbox = Cv2.BoundingRect(contour);
            if (bbox.Height == 0) continue;

            // 面积检查
            var areaRatio = (double)(bbox.Width * bbox.Height) / (imgWidth * imgHeight);
            if (areaRatio <= 0.24 || areaRatio >= 0.3) continue; // 弹窗高约300，面积比约等于0.27
            _logger.LogDebug("自动剧情：关闭角色弹窗-面积检查通过");

            // 宽高比检查
            var aspectRatio = (double)bbox.Width / bbox.Height;
            if (aspectRatio < 5.6 || aspectRatio > 7.2) continue;
            _logger.LogDebug("自动剧情：关闭角色弹窗-宽高比检查通过");

            // 位置检查
            if (bbox.Y <= imgHeight * 0.3 || bbox.Y + bbox.Height >= imgHeight * 0.7) continue;
            _logger.LogDebug("自动剧情：关闭角色弹窗-位置检查通过");


            // 检查是否包含两种颜色  
            var lightCount = Cv2.CountNonZero(new Mat(maskLight, bbox));
            var darkCount = Cv2.CountNonZero(new Mat(maskDark, bbox));
            if (lightCount <= 0 || darkCount <= 0) continue;

            if (UseBackgroundOperation && !SystemControl.IsGenshinImpactActive())
            {
                content.CaptureRectArea.Derive(bbox).BackgroundClick();
            }
            else
            {
                content.CaptureRectArea.ClickTo(100, 100); // 点击角色横幅外的区域才能跳过
            }

            _logger.LogInformation("自动剧情：关闭角色弹窗");
            return;
        }
    }

    private bool SubmitGoods(CaptureContent content)
    {
        using var exclamationRa = content.CaptureRectArea.Find(_autoSkipAssets.SubmitExclamationIconRo);
        if (!exclamationRa.IsEmpty())
        {
            // var rects = MatchTemplateHelper.MatchOnePicForOnePic(content.CaptureRectArea.SrcMat.CvtColor(ColorConversionCodes.BGRA2BGR),
            //     _autoSkipAssets.SubmitGoodsMat, TemplateMatchModes.SqDiffNormed, null, 0.9, 4);
            var param = new MorphologyParam(new Size(5,5), MorphTypes.Close, 2);
            var rects = ContoursHelper.FindSpecifyColorRects(content.CaptureRectArea.SrcMat, new Scalar(233, 229, 220), 100, 20, param);
            if (rects.Count == 0)
            {
                return false;
            }

            // 画矩形并保存
            // foreach (var rect in rects)
            // {
            //     Cv2.Rectangle(content.CaptureRectArea.SrcMat, rect, Scalar.Red, 1);
            // }
            // Cv2.ImWrite("log/提交物品.png", content.CaptureRectArea.SrcMat);

            for (var i = 0; i < rects.Count; i++)
            {
                content.CaptureRectArea.Derive(rects[i]).Click();
                _logger.LogInformation("提交物品：{Text}", "1. 选择物品" + i);
                TaskControl.Sleep(800);

                var btnBlackConfirmRa = TaskControl.CaptureToRectArea(forceNew: true).Find(ElementAssets.Instance.BtnBlackConfirm);
                if (!btnBlackConfirmRa.IsEmpty())
                {
                    btnBlackConfirmRa.Click();
                    _logger.LogInformation("提交物品：{Text}", "2. 放入" + i);
                    TaskControl.Sleep(200);
                }
            }

            TaskControl.Sleep(500);

            using var ra = TaskControl.CaptureToRectArea(forceNew: true);
            using var btnWhiteConfirmRa = ra.Find(ElementAssets.Instance.BtnWhiteConfirm);
            if (!btnWhiteConfirmRa.IsEmpty())
            {
                btnWhiteConfirmRa.Click();
                _logger.LogInformation("提交物品：{Text}", "3. 交付");

                VisionContext.Instance().DrawContent.ClearAll();
            }

            // 最多4个物品 现在就支持一个
            // var prevGoodsRect = Rect.Empty;
            // for (var i = 1; i <= 4; i++)
            // {
            //     // 不断的截取出右边的物品
            //     TaskControl.Sleep(200);
            //     content = TaskControl.CaptureToContent();
            //     var gameArea = content.CaptureRectArea;
            //     if (prevGoodsRect != Rect.Empty)
            //     {
            //         var r = content.CaptureRectArea.ToRect();
            //         var newX = prevGoodsRect.X + prevGoodsRect.Width;
            //         gameArea = content.CaptureRectArea.Crop(new Rect(newX, 0, r.Width - newX, r.Height));
            //         Cv2.ImWrite($"log/物品{i}.png", gameArea.SrcMat);
            //     }
            //
            //     var goods = gameArea.Find(_autoSkipAssets.SubmitGoodsRo);
            //     if (!goods.IsEmpty())
            //     {
            //         prevGoodsRect = goods.ConvertRelativePositionToCaptureArea();
            //         goods.ClickCenter();
            //         _logger.LogInformation("提交物品：{Text}", "1. 选择物品" + i);
            //
            //         TaskControl.Sleep(800);
            //         content = TaskControl.CaptureToContent();
            //
            //         var btnBlackConfirmRa = content.CaptureRectArea.Find(ElementAssets.Instance().BtnBlackConfirm);
            //         if (!btnBlackConfirmRa.IsEmpty())
            //         {
            //             btnBlackConfirmRa.ClickCenter();
            //             _logger.LogInformation("提交物品：{Text}", "2. 放入" + i);
            //
            //             TaskControl.Sleep(800);
            //             content = TaskControl.CaptureToContent();
            //
            //             btnBlackConfirmRa = content.CaptureRectArea.Find(ElementAssets.Instance().BtnBlackConfirm);
            //             if (!btnBlackConfirmRa.IsEmpty())
            //             {
            //                 _logger.LogInformation("提交物品：{Text}", "2. 仍旧存在物品");
            //                 continue;
            //             }
            //             else
            //             {
            //                 var btnWhiteConfirmRa = content.CaptureRectArea.Find(ElementAssets.Instance().BtnWhiteConfirm);
            //                 if (!btnWhiteConfirmRa.IsEmpty())
            //                 {
            //                     btnWhiteConfirmRa.ClickCenter();
            //                     _logger.LogInformation("提交物品：{Text}", "3. 交付");
            //
            //                     VisionContext.Instance().DrawContent.ClearAll();
            //                     return true;
            //                 }
            //                 break;
            //             }
            //         }
            //     }
            //     else
            //     {
            //         break;
            //     }
            // }
        }

        return false;
    }
}
