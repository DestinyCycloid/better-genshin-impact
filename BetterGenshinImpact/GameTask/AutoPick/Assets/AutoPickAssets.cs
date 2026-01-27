using System;
using BetterGenshinImpact.Core.Recognition;
using BetterGenshinImpact.GameTask.Model;
using BetterGenshinImpact.Helpers;
using OpenCvSharp;
using System.Drawing;
using Vanara.PInvoke;
using Microsoft.Extensions.Logging;

namespace BetterGenshinImpact.GameTask.AutoPick.Assets;

public class AutoPickAssets : BaseAssets<AutoPickAssets>
{
    private readonly ILogger<AutoPickAssets> _logger = App.GetLogger<AutoPickAssets>();

    public RecognitionObject FRo;
    public RecognitionObject YRo; // 手柄Y按钮
    public RecognitionObject ChatIconRo;
    public RecognitionObject SettingsIconRo;
    public RecognitionObject LRo;


    public User32.VK PickVk = User32.VK.VK_F;
    public RecognitionObject PickRo;
    public RecognitionObject ChatPickRo;

    private AutoPickAssets()
    {
        FRo = new RecognitionObject
        {
            Name = "F",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoPick", "F.png"),
            RegionOfInterest = new Rect((int)(1090 * AssetScale),
                (int)(330 * AssetScale),
                (int)(60 * AssetScale),
                (int)(420 * AssetScale)),
            DrawOnWindow = false
        }.InitTemplate();

        // 手柄Y按钮识别对象（位于屏幕中间偏右，拾取提示位置）
        YRo = new RecognitionObject
        {
            Name = "Y",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoPick", "Y.png"),
            RegionOfInterest = new Rect((int)(900 * AssetScale),   // X=900开始（1080p坐标，对应4K的1800）
                (int)(400 * AssetScale),  // Y=400开始
                (int)(400 * AssetScale),  // 宽度400
                (int)(300 * AssetScale)), // 高度300
            DrawOnWindow = true,  // 开启调试显示
            DrawOnWindowPen = new Pen(Color.Yellow, 2),
            Threshold = 0.7  // 使用真实截图，阈值可以适中
        }.InitTemplate();

        ChatIconRo = new RecognitionObject
        {
            Name = "ChatIcon",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoSkip", "icon_option.png"),
            DrawOnWindow = false,
            DrawOnWindowPen = new Pen(Color.Chocolate, 2)
        }.InitTemplate();
        SettingsIconRo = new RecognitionObject
        {
            Name = "SettingsIcon",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoPick", "icon_settings.png"),
            DrawOnWindow = false,
            DrawOnWindowPen = new Pen(Color.Chocolate, 2)
        }.InitTemplate();
        
        LRo = new RecognitionObject
        {
            Name = "L",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoPick", "L.png"),
            RegionOfInterest = new Rect(CaptureRect.Width-(int)(110 * AssetScale),
                (int)(550 * AssetScale),
                (int)(70 * AssetScale),
                (int)(100 * AssetScale)),
        }.InitTemplate();


        PickRo = FRo;
        var keyName = TaskContext.Instance().Config.AutoPickConfig.PickKey;
        if (!string.IsNullOrEmpty(keyName))
        {
            try
            {
                PickRo = LoadCustomPickKey(keyName);
                PickVk = User32Helper.ToVk(keyName);
                TaskContext.Instance().Config.KeyBindingsConfig.PickUpOrInteract = (Core.Config.KeyId)(int)PickVk;
                ChatPickRo = LoadCustomChatPickKey(keyName);
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "加载自定义拾取按键时发生异常");
                _logger.LogError("加载自定义拾取按键失败，继续使用默认的F键");
                TaskContext.Instance().Config.AutoPickConfig.PickKey = "F";
                return;
            }

            if (keyName != "F")
            {
                _logger.LogInformation("自定义拾取按键：{Key}", keyName);
            }
        }
    }

    public RecognitionObject LoadCustomPickKey(string key)
    {
        return new RecognitionObject
        {
            Name = key,
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoPick", key + ".png"),
            RegionOfInterest = new Rect((int)(1090 * AssetScale),
                (int)(330 * AssetScale),
                (int)(60 * AssetScale),
                (int)(420 * AssetScale)),
            DrawOnWindow = false
        }.InitTemplate();
    }

    public RecognitionObject LoadCustomChatPickKey(string key)
    {
        return new RecognitionObject
        {
            Name = "chatPick" + key,
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoPick", key + ".png"),
            RegionOfInterest = new Rect((int)(1200 * AssetScale),
                (int)(350 * AssetScale),
                (int)(50 * AssetScale),
                CaptureRect.Height - (int)(220 * AssetScale) - (int)(350 * AssetScale)),
            DrawOnWindow = false
        }.InitTemplate();
    }
}