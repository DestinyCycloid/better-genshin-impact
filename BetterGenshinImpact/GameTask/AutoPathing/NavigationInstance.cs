using System;
using System.IO;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Common.Map.Maps;
using BetterGenshinImpact.GameTask.Common.Map.Maps.Base;
using BetterGenshinImpact.GameTask.Model.Area;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace BetterGenshinImpact.GameTask.AutoPathing;

public class NavigationInstance
{
    private float _prevX = -1;
    private float _prevY = -1;
    private DateTime _captureTime = DateTime.MinValue;
    
    // 位置稳定性检查
    private float _stableX = -1;
    private float _stableY = -1;
    private int _stableCount = 0;
    private const int STABLE_THRESHOLD = 5; // 连续5次识别到相同位置才认为稳定
    private const float STABLE_DISTANCE = 1.5f; // 1.5米以内认为是同一位置
    
    public void Reset()
    {
        (_prevX, _prevY) = (-1, -1);
        (_stableX, _stableY) = (-1, -1);
        _stableCount = 0;
    }
    
    public void SetPrevPosition(float x, float y)
    {
        (_prevX, _prevY) = (x, y);
        (_stableX, _stableY) = (x, y);
        _stableCount = STABLE_THRESHOLD;
    }

    public Point2f GetPosition(ImageRegion imageRegion, string mapName, string mapMatchMethod)
    {
        var colorMat = new Mat(imageRegion.SrcMat, MapAssets.Instance.MimiMapRect);
        
        var captureTime = DateTime.UtcNow;
        
        // 尝试使用更宽松的匹配参数（rank=0）
        var sceneMap = MapManager.GetMap(mapName, mapMatchMethod);
        Point2f p;
        
        if (_prevX <= 0 || _prevY <= 0)
        {
            // 第一次识别，使用全地图匹配
            p = sceneMap.GetMiniMapPosition(colorMat);
        }
        else
        {
            // 后续识别，先尝试局部匹配（rank=0，更宽松）
            p = (sceneMap as SceneBaseMapByTemplateMatch)?.GetMiniMapPosition(colorMat, _prevX, _prevY, 0)
                ?? sceneMap.GetMiniMapPosition(colorMat, _prevX, _prevY);
        }
        
        // 位置稳定性检查：只有连续多次识别到相同位置才使用
        if (p.X > 0 && p.Y > 0)
        {
            // 检查是否与上次稳定位置接近
            if (_stableX > 0 && _stableY > 0)
            {
                var dx = p.X - _stableX;
                var dy = p.Y - _stableY;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                
                if (distance < STABLE_DISTANCE)
                {
                    // 位置接近，增加稳定计数
                    _stableCount++;
                    if (_stableCount >= STABLE_THRESHOLD)
                    {
                        // 位置已稳定，更新稳定位置为当前位置
                        (_stableX, _stableY) = (p.X, p.Y);
                    }
                    else
                    {
                        // 还不够稳定，继续使用上次的稳定位置
                        p = new Point2f(_stableX, _stableY);
                    }
                }
                else
                {
                    // 位置跳变超过阈值
                    // 如果跳变距离很大（>10米），可能是识别错误，使用上次位置
                    if (distance > 10)
                    {
                        TaskControl.Logger.LogDebug($"⚠️ 位置跳变过大({distance:F1}米)，保持上次位置");
                        p = new Point2f(_prevX > 0 ? _prevX : _stableX, _prevY > 0 ? _prevY : _stableY);
                    }
                    else
                    {
                        // 小幅跳变，重新开始计数
                        (_stableX, _stableY) = (p.X, p.Y);
                        _stableCount = 1;
                    }
                }
            }
            else
            {
                // 第一次有效位置
                (_stableX, _stableY) = (p.X, p.Y);
                _stableCount = 1;
            }
        }
        
        if (p != default && captureTime > _captureTime)
        {
            (_prevX, _prevY) = (p.X, p.Y);
            _captureTime = captureTime;
        }
        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<object>(typeof(Navigation),
            "SendCurrentPosition", new object(), p));
        return p;
    }

    /// <summary>
    /// 稳定获取当前位置坐标，优先使用全地图匹配，适用于不需要高效率但需要高稳定性的场景
    /// </summary>
    /// <param name="imageRegion">图像区域</param>
    /// <param name="mapName">地图名字</param>
    /// <param name="mapMatchMethod">地图匹配方式</param>
    /// <returns>当前位置坐标</returns>
    public Point2f GetPositionStable(ImageRegion imageRegion, string mapName, string mapMatchMethod)
    {
        var colorMat = new Mat(imageRegion.SrcMat, MapAssets.Instance.MimiMapRect);
        var captureTime = DateTime.UtcNow;

        // 先尝试使用局部匹配
        var sceneMap = MapManager.GetMap(mapName, mapMatchMethod);
        //提高局部匹配的阈值，以解决在沙漠录制点位时，移动过远不会触发全局匹配的情况
        var p = (sceneMap as SceneBaseMapByTemplateMatch)?.GetMiniMapPosition(colorMat, _prevX, _prevY, 0)
                ?? sceneMap.GetMiniMapPosition(colorMat, _prevX, _prevY);

        // 如果局部匹配失败或者点位跳跃过大，再尝试全地图匹配
        if (p == default || (_prevX > 0 && _prevY >0 && p.DistanceTo(new Point2f(_prevX,_prevY)) > 150))
        {
            Reset();
            p = MapManager.GetMap(mapName, mapMatchMethod).GetMiniMapPosition(colorMat, _prevX, _prevY);
        }
        if (p != default && captureTime > _captureTime)
        {
            (_prevX, _prevY) = (p.X, p.Y);
            _captureTime = captureTime;
        }

        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<object>(typeof(Navigation),
            "SendCurrentPosition", new object(), p));
        return p;
    }

    public Point2f GetPositionStableByCache(ImageRegion imageRegion, string mapName, string mapMatchingMethod, int cacheTimeMs = 900)
    {
        var captureTime = DateTime.UtcNow;
        if (captureTime - _captureTime < TimeSpan.FromMilliseconds(cacheTimeMs) && _prevX > 0 && _prevY > 0)
        {
            return new Point2f(_prevX, _prevY);
        }

        return GetPositionStable(imageRegion, mapName, mapMatchingMethod);
    }
}