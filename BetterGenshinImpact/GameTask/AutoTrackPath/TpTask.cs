using BetterGenshinImpact.Core.Recognition;
using BetterGenshinImpact.Core.Recognition.OpenCv;
using BetterGenshinImpact.Core.Script.Dependence;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.Core.Simulator.Extensions;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation.Exception;
using BetterGenshinImpact.GameTask.AutoPathing;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using BetterGenshinImpact.GameTask.AutoPathing.Model.Enum;
using BetterGenshinImpact.GameTask.AutoTrackPath.Model;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Common.Exceptions;
using BetterGenshinImpact.GameTask.Common.Job;
using BetterGenshinImpact.GameTask.Common.Map.Maps;
using BetterGenshinImpact.GameTask.Common.Map.Maps.Base;
using BetterGenshinImpact.GameTask.Model.Area;
using BetterGenshinImpact.GameTask.QuickTeleport.Assets;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Helpers.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using static BetterGenshinImpact.GameTask.Common.TaskControl;

namespace BetterGenshinImpact.GameTask.AutoTrackPath;

/// <summary>
/// 传送任务
/// </summary>
public class TpTask
{
    private readonly QuickTeleportAssets _assets = QuickTeleportAssets.Instance;
    private readonly Rect _captureRect = TaskContext.Instance().SystemInfo.ScaleMax1080PCaptureRect;
    private readonly double _zoomOutMax1080PRatio = TaskContext.Instance().SystemInfo.ZoomOutMax1080PRatio;
    private readonly TpConfig _tpConfig = TaskContext.Instance().Config.TpConfig;
    private readonly string _mapMatchingMethod = TaskContext.Instance().Config.PathingConditionConfig.MapMatchingMethod;
    private readonly BlessingOfTheWelkinMoonTask _blessingOfTheWelkinMoonTask = new();

    private readonly CancellationToken ct;
    private readonly CultureInfo cultureInfo;
    private readonly IStringLocalizer stringLocalizer;

    /// <summary>
    /// 直接通过缩放比例按钮计算放大按钮的Y坐标
    /// </summary>
    private readonly int _zoomInButtonY = TaskContext.Instance().Config.TpConfig.ZoomStartY - 24; //  y-coordinate for zoom-in button  = _zoomStartY - 24

    /// <summary>
    /// 直接通过缩放比例按钮计算缩小按钮的Y坐标
    /// </summary>
    private readonly int _zoomOutButtonY = TaskContext.Instance().Config.TpConfig.ZoomEndY + 24; //  y-coordinate for zoom-out button = _zoomEndY + 24

    private const double DisplayTpPointZoomLevel = 4.4; // 传送点显示的时候的地图比例

    public TpTask(CancellationToken ct)
    {
        this.ct = ct;
        TpTaskParam param = new TpTaskParam();
        this.cultureInfo = param.GameCultureInfo;
        this.stringLocalizer = param.StringLocalizer;
    }

    /// <summary>
    /// 传送到七天神像
    /// </summary>
    public async Task TpToStatueOfTheSeven()
    {
        await CheckInBigMapUi();

        // 提前调整至恰当的缩放以更快的传送
        if (_tpConfig.MapZoomEnabled)
        {
            double currentZoomLevel = GetBigMapZoomLevel(CaptureToRectArea());
            if (currentZoomLevel > DisplayTpPointZoomLevel)
            {
                await AdjustMapZoomLevel(currentZoomLevel, DisplayTpPointZoomLevel);
            }
            else if (currentZoomLevel < 3)
            {
                await AdjustMapZoomLevel(currentZoomLevel, 3);
            }
        }

        string? country = _tpConfig.ReviveStatueOfTheSevenCountry;
        string? area = _tpConfig.ReviveStatueOfTheSevenArea;
        double x = _tpConfig.ReviveStatueOfTheSevenPointX;
        double y = _tpConfig.ReviveStatueOfTheSevenPointY;
        GiTpPosition revivePoint = _tpConfig.ReviveStatueOfTheSeven ?? GetNearestGoddess(x, y);
        if (_tpConfig.IsReviveInNearestStatueOfTheSeven)
        {
            var center = GetBigMapCenterPoint(MapTypes.Teyvat.ToString());
            var giTpPoint = GetNearestGoddess(center.X, center.Y);
            country = giTpPoint.Country;
            area = giTpPoint.Level1Area;
            x = giTpPoint.X;
            y = giTpPoint.Y;
            revivePoint = giTpPoint;
        }

        Logger.LogInformation("将传送至 {country} {area} 七天神像", country, area);
        await Tp(x, y, MapTypes.Teyvat.ToString(), false);
        if (_tpConfig.ShouldMove || _tpConfig.IsReviveInNearestStatueOfTheSeven)
        {
            (x, y) = GetClosestPoint(revivePoint.TranX, revivePoint.TranY, x, y, 5);
            var waypoint = new Waypoint
            {
                X = x,
                Y = y,
                Type = WaypointType.Path.Code,
                MoveMode = MoveModeEnum.Walk.Code
            };
            var waypointForTrack = new WaypointForTrack(waypoint, nameof(MapTypes.Teyvat), _mapMatchingMethod);
            await new PathExecutor(ct).MoveTo(waypointForTrack);
            Simulation.SendInput.SimulateAction(GIActions.Drop);
        }

        await Delay((int)(_tpConfig.HpRestoreDuration * 1000), ct);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tranX"> 传送后实际到达的点X坐标 </param>
    /// <param name="tranY"> 传送后实际到达的点Y坐标 </param>
    /// <param name="x"> 传送点 X 坐标 </param>
    /// <param name="y"> 传送点 Y 坐标 </param>
    /// <param name="d"> 期望最终离传送点的距离 </param>
    /// <returns>  </returns>
    private static (double X, double Y) GetClosestPoint(double tranX, double tranY, double x, double y, double d)
    {
        double dx = x - tranX;
        double dy = y - tranY;
        double distanceSquared = dx * dx + dy * dy;
        double distance = Math.Sqrt(distanceSquared);
        d = d > 0 ? d : 0;
        if (distance < d)
        {
            return (tranX, tranY);
        }

        double ratio = d / distance;
        double px = (x - dx * ratio);
        double py = (y - dy * ratio);
        return (px, py);
    }

    /// <summary>
    /// 获取离 x,y 最近的七天神像
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    private GiTpPosition GetNearestGoddess(double x, double y)
    {
        GiTpPosition? nearestGiTpPosition = null;
        double minDistance = double.MaxValue;
        foreach (var (_, goddessPosition) in MapLazyAssets.Instance.GoddessPositions)
        {
            var distance = Math.Sqrt(Math.Pow(goddessPosition.X - x, 2) + Math.Pow(goddessPosition.Y - y, 2));
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestGiTpPosition = goddessPosition;
            }
        }

        // 获取最近的神像位置
        return nearestGiTpPosition ?? throw new InvalidOperationException("没找到最近的七天神像");
    }

    /// <summary>
    ///释放所有按键，并打开大地图界面
    /// </summary>
    /// <param name="retryCount">重试次数</param>
    public async Task OpenBigMapUi(int retryCount = 3)
    {
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                // 打开地图前释放所有按键
                Simulation.ReleaseAllKey();
                await Delay(20, ct);
                await CheckInBigMapUi();
                return;
            }
            catch (Exception e) when (e is NormalEndException || e is TaskCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (retryCount > 1)
                {
                    Logger.LogError("打开大地图失败，重试 {I} 次", i + 1);
                    Logger.LogDebug(e, "打开大地图失败，重试 {I} 次", i + 1);
                    await _blessingOfTheWelkinMoonTask.Start(ct);
                }

                if (i + 1 >= retryCount)
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// 通过大地图传送到指定坐标最近的传送点，然后移动到指定坐标
    /// </summary>
    /// <param name="tpX"></param>
    /// <param name="tpY"></param>
    /// <param name="mapName">独立地图名称</param>
    /// <param name="force">强制以当前的tpX,tpY坐标进行自动传送</param>
    private async Task<(double, double)> TpOnce(double tpX, double tpY, string mapName = "Teyvat", bool force = false)
    {
        // 1. 确认在地图界面
        await OpenBigMapUi(1);
        // 2. 传送前的计算准备
        // 获取离目标传送点最近的两个传送点，按距离排序
        var nTpPoints = GetNearestNTpPoints(tpX, tpY, mapName, 2);
        // 获取最近的传送点与区域
        var (x, y, country) = force ? (tpX, tpY, null) : (nTpPoints[0].X, nTpPoints[0].Y, nTpPoints[0].Country);
        var disBetweenTpPoints = Math.Sqrt(Math.Pow(nTpPoints[0].X - nTpPoints[1].X, 2) +
                                           Math.Pow(nTpPoints[0].Y - nTpPoints[1].Y, 2));
        // 确保不会点错传送点的最小缩放，保证至少为 1.0
        var minZoomLevel = Math.Max(disBetweenTpPoints / 20, 1.0);
        // 切换地区
        if (mapName == MapTypes.Teyvat.ToString())
        {
            // 计算传送点位置离哪张地图切换后的中心点最近，切换到该地图
            await SwitchRecentlyCountryMap(x, y, country);
        }
        else
        {
            // 直接切换地区
            await SwitchArea(MapTypesExtensions.ParseFromName(mapName).GetDescription());
        }
        await Delay(50, ct);


        // 3. 调整初始缩放等级，避免识别中心点失败
        var zoomLevel = GetBigMapZoomLevel(CaptureToRectArea());
        if (_tpConfig.MapZoomEnabled)
        {
            /* 动态调整缩放逻辑：
                1. 如果当前缩放大于显示传送点级别 -> 缩小
                2. 如果小于配置的最小级别 -> 放大 */
            if (zoomLevel > DisplayTpPointZoomLevel + _tpConfig.PrecisionThreshold)
            {
                await AdjustMapZoomLevel(zoomLevel, DisplayTpPointZoomLevel);
                zoomLevel = DisplayTpPointZoomLevel;
                Logger.LogInformation("当前缩放等级过大，调整为 {zoomLevel:0.00}", DisplayTpPointZoomLevel);
            }
            else if (zoomLevel < _tpConfig.MinZoomLevel - _tpConfig.PrecisionThreshold)
            {
                await AdjustMapZoomLevel(zoomLevel, _tpConfig.MinZoomLevel);
                zoomLevel = _tpConfig.MinZoomLevel;
                Logger.LogInformation("当前缩放等级过小，调整为 {zoomLevel:0.00}", _tpConfig.MinZoomLevel);
            }
        }

        // 4. zoomLevel不满足条件，强制进行一次 MoveMapTo，避免传送点相近导致误点
        if (zoomLevel > minZoomLevel)
        {
            if (_tpConfig.MapZoomEnabled)
            {
                Logger.LogInformation("目标传送点有相近传送点，到目标传送点附近将缩放到{zoomLevel:0.00}", minZoomLevel);
                await MoveMapTo(x, y, mapName, minZoomLevel);
                await Delay(300, ct); // 等待地图移动完成
            }
            else
            {
                Logger.LogInformation("目标传送点有相近传送点，可能传送失败。如果失败请到设置-大地图地图传送设置开启地图缩放");
                // TODO 部分无法区分点位强制缩放，即使没有zoomEnabled。
            }
        }

        // 5. 判断传送点是否在当前界面，若否则移动地图
        var bigMapInAllMapRect = GetBigMapRect(mapName);
        var retryCount = 0;
        do
        {
            if (IsPointInBigMapWindow(mapName, bigMapInAllMapRect, x, y)) break;
            if (retryCount++ >= 5) // 防止死循环
            {
                Logger.LogWarning("多次尝试未移动到目标传送点，传送失败");
                throw new Exception("多次尝试未移动到目标传送点，传送失败");
            }

            Logger.LogInformation("传送点不在当前大地图范围内，重新调整地图位置");
            await MoveMapTo(x, y, mapName);
            await Delay(300, ct);
            bigMapInAllMapRect = GetBigMapRect(mapName);
        } while (true);

        // 6. 计算传送点位置并点击
        // Debug.WriteLine($"({x},{y}) 在 {bigMapInAllMapRect} 内，计算它在窗体内的位置");
        // 注意这个坐标的原点是中心区域某个点，所以要转换一下点击坐标（点击坐标是左上角为原点的坐标系），不能只是缩放
        var (clickX, clickY) = ConvertToGameRegionPosition(mapName, bigMapInAllMapRect, x, y);
        Logger.LogInformation("点击传送点");
        
        // 根据输入模式选择不同的点击方式
        if (Simulation.CurrentInputMode == InputMode.XInput)
        {
            // 手柄模式：使用左摇杆移动光标到传送点
            Logger.LogInformation("🎮 手柄模式：使用左摇杆移动光标到传送点");
            
            // 计算从屏幕中心到传送点的偏移
            int centerX = _captureRect.Width / 2;
            int centerY = _captureRect.Height / 2;
            int deltaX = (int)(clickX - centerX);
            int deltaY = (int)(clickY - centerY);
            
            Logger.LogInformation("  → 屏幕中心: ({CenterX}, {CenterY})", centerX, centerY);
            Logger.LogInformation("  → 传送点位置: ({ClickX:F0}, {ClickY:F0})", clickX, clickY);
            Logger.LogInformation("  → 移动偏移: ΔX={DeltaX}, ΔY={DeltaY}", deltaX, deltaY);
            
            // 使用左摇杆移动光标
            Simulation.MoveLeftStickForCursor(deltaX, deltaY, 800); // 800ms移动时间
            await Delay(200, ct);
            
            // 按A键选中传送点
            Logger.LogInformation("  → 按A键选中传送点");
            Simulation.SimulateAction(GIActions.Jump); // A键映射到Jump
            await Delay(300, ct);
        }
        else
        {
            // 键鼠模式：直接点击传送点
            CaptureToRectArea().ClickTo((int)clickX, (int)clickY);
        }

        // 7. 触发一次快速传送功能
        await Delay(500, ct);
        await ClickTpPoint(CaptureToRectArea());

        // 8. 等待传送完成
        await WaitForTeleportCompletion(50, 1200);
        return (x, y);
    }

    /// <summary>
    ///     检查传送是否完成，未完成则等待
    /// </summary>
    /// <param name="maxAttempts">最大检查延时的次数</param>
    /// <param name="delayMs">如果未完成加载，检查加载页面的延时。</param>
    private async Task WaitForTeleportCompletion(int maxAttempts, int delayMs)
    {
        Logger.LogInformation("⏳ 开始等待传送完成...");
        await Delay(delayMs, ct);
        
        for (var i = 0; i < maxAttempts; i++)
        {
            using var capture = CaptureToRectArea();
            var isInMainUi = Bv.IsInMainUi(capture);
            Logger.LogDebug("第{Attempt}次检查: IsInMainUi={IsInMainUi}", i + 1, isInMainUi);
            
            if (isInMainUi)
            {
                Logger.LogInformation("✅ 传送完成，返回主界面");
                return;
            }
            //增加容错，小概率情况下碰到，前面点击传送失败
            capture.Find(_assets.TeleportButtonRo, rg =>
            {
                // 根据输入模式选择不同的确认方式
                if (Simulation.CurrentInputMode == InputMode.XInput)
                {
                    // 手柄模式：按A键
                    Logger.LogInformation("🎮 手柄模式：容错 - 按A键确认传送");
                    Simulation.SimulateAction(GIActions.Jump);
                }
                else
                {
                    // 键鼠模式：点击按钮
                    rg.Click();
                }
            });
            await Delay(delayMs, ct);
            // 打开大地图期间推送的月卡会在传送之后直接显示，导致检测不到传送完成。
            await _blessingOfTheWelkinMoonTask.Start(ct);
        }

        Logger.LogWarning("⚠️ 传送等待超时，换台电脑吧");
    }

    /// <summary>
    /// 传送点是否在大地图窗口内
    /// </summary>
    /// <param name="mapName"></param>
    /// <param name="bigMapInAllMapRect">大地图在整个游戏地图中的矩形位置（原神坐标系）</param>
    /// <param name="x">传送点x坐标（原神坐标系）</param>
    /// <param name="y">传送点y坐标（原神坐标系）</param>
    /// <returns></returns>
    private bool IsPointInBigMapWindow(string mapName, Rect bigMapInAllMapRect, double x, double y)
    {
        // 坐标不包含直接返回
        if (!bigMapInAllMapRect.Contains(x, y))
        {
            return false;
        }

        var (clickX, clickY) = ConvertToGameRegionPosition(mapName, bigMapInAllMapRect, x, y);
        // 屏蔽左上角360x400区域
        if (clickX < 360 * _zoomOutMax1080PRatio && clickY < 400 * _zoomOutMax1080PRatio)
        {
            return false;
        }

        // 屏蔽周围 115 一圈的区域
        if (clickX < 115 * _zoomOutMax1080PRatio
            || clickY < 115 * _zoomOutMax1080PRatio
            || clickX > _captureRect.Width - 115 * _zoomOutMax1080PRatio
            || clickY > _captureRect.Height - 115 * _zoomOutMax1080PRatio)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 转换传送点坐标到窗体内需要点击的坐标
    /// </summary>
    /// <param name="mapName"></param>
    /// <param name="bigMapInAllMapRect">大地图在整个游戏地图中的矩形位置（原神坐标系）</param>
    /// <param name="x">传送点x坐标（原神坐标系）</param>
    /// <param name="y">传送点y坐标（原神坐标系）</param>
    /// <returns></returns>
    private (double clickX, double clickY) ConvertToGameRegionPosition(string mapName, Rect bigMapInAllMapRect, double x, double y)
    {
        var (picX, picY) = MapManager.GetMap(mapName, _mapMatchingMethod).ConvertGenshinMapCoordinatesToImageCoordinates(new Point2f((float)x, (float)y));
        var picRect = MapManager.GetMap(mapName, _mapMatchingMethod).ConvertGenshinMapCoordinatesToImageCoordinates(bigMapInAllMapRect);
        Debug.WriteLine($"({picX},{picY}) 在 {picRect} 内，计算它在窗体内的位置");
        var clickX = (picX - picRect.X) / picRect.Width * _captureRect.Width;
        var clickY = (picY - picRect.Y) / picRect.Height * _captureRect.Height;
        return (clickX, clickY);
    }

    public async Task CheckInBigMapUi()
    {
        // 尝试打开地图失败后，先回到主界面后再次尝试打开地图
        if (!await TryToOpenBigMapUi())
        {
            await new ReturnMainUiTask().Start(ct);
            await Delay(500, ct);
            if (!await TryToOpenBigMapUi())
            {
                throw new RetryException("打开大地图失败，请检查按键绑定中「打开地图」按键设置是否和原神游戏中一致！");
            }
        }
    }

    /// <summary>
    /// 尝试打开地图界面
    /// </summary>
    private async Task<bool> TryToOpenBigMapUi()
    {
        var ra1 = CaptureToRectArea();
        var isInMapBefore = Bv.IsInBigMapUi(ra1);
        
        if (!isInMapBefore)
        {
            Simulation.SimulateAction(GIActions.OpenMap);
            // 手柄模式下打开地图需要更长时间（组合键执行约1.4秒 + 地图打开动画约1秒）
            await Delay(2500, ct);
            
            ra1 = CaptureToRectArea();
            return Bv.IsInBigMapUi(ra1);
        }
        
        return true;
    }


    public async Task<(double, double)> Tp(double tpX, double tpY, string mapName = "Teyvat", bool force = false)
    {
        // 临时禁用重试机制，避免手柄模式下重试时按A键导致进入其他页面
        for (var i = 0; i < 1; i++)
        {
            try
            {
                return await TpOnce(tpX, tpY, mapName, force);
            }
            catch (TpPointNotActivate e)
            {
                // 传送点未激活或不存在 按ESC回到大地图界面
                Simulation.SendInput.Keyboard.KeyPress(User32.VK.VK_ESCAPE);
                await Delay(300, ct);
                // throw; // 不抛出异常，继续重试
                Logger.LogWarning(e.Message + "  重试");
            }
            catch (Exception e) when (e is NormalEndException || e is TaskCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.LogError("传送失败，重试 {I} 次", i + 1);
                Logger.LogDebug(e, "传送失败，重试 {I} 次", i + 1);
            }
        }

        throw new InvalidOperationException("传送失败");
    }

    /// <summary>
    /// 移动地图到指定传送点位置
    /// 可能会移动不对，所以可以重试此方法
    /// </summary>
    /// <param name="x">目标x坐标</param>
    /// <param name="y">目标y坐标</param>
    /// <param name="mapName">地图名称</param>
    /// <param name="finalZoomLevel">到达目标点的最小缩放等级，只在 MapZoomEnabled 为 True 生效</param>
    public async Task MoveMapTo(double x, double y, string mapName, double finalZoomLevel = 2)
    {
        // 参数初始化
        double minZoomLevel = Math.Min(finalZoomLevel, _tpConfig.MinZoomLevel);
        double maxZoomLevel = _tpConfig.MaxZoomLevel;
        double currentZoomLevel = GetBigMapZoomLevel(CaptureToRectArea());
        int exceptionTimes = 0;
        Point2f mapCenterPoint;
        try
        {
            mapCenterPoint = GetPositionFromBigMap(mapName); // 初始中心
        }
        catch (Exception e)
        {
            ++exceptionTimes;
            mapCenterPoint = new Point2f(0f, 0f); // 其他恰当的初始值?
        }

        var (xOffset, yOffset) = (x - mapCenterPoint.X, y - mapCenterPoint.Y);
        double totalMoveMouseX = _tpConfig.MapScaleFactor * Math.Abs(xOffset) / currentZoomLevel;
        double totalMoveMouseY = _tpConfig.MapScaleFactor * Math.Abs(yOffset) / currentZoomLevel;
        double mouseDistance = Math.Sqrt(totalMoveMouseX * totalMoveMouseX + totalMoveMouseY * totalMoveMouseY);
        // 缩小地图到恰当的缩放
        if (_tpConfig.MapZoomEnabled)
        {
            if (mouseDistance > _tpConfig.MapZoomOutDistance)
            {
                double targetZoomLevel = currentZoomLevel * mouseDistance / _tpConfig.MapZoomOutDistance;
                targetZoomLevel = Math.Min(targetZoomLevel, maxZoomLevel);
                await AdjustMapZoomLevel(currentZoomLevel, targetZoomLevel);
                double nextZoomLevel = GetBigMapZoomLevel(CaptureToRectArea());
                totalMoveMouseX *= currentZoomLevel / nextZoomLevel;
                totalMoveMouseY *= currentZoomLevel / nextZoomLevel;
                mouseDistance *= currentZoomLevel / nextZoomLevel;
                currentZoomLevel = nextZoomLevel;
            }
        }

        // 开始移动并放大地图
        for (var iteration = 0; iteration < _tpConfig.MaxIterations; iteration++)
        {
            if (_tpConfig.MapZoomEnabled)
            {
                if (mouseDistance < _tpConfig.MapZoomInDistance)
                {
                    double targetZoomLevel = currentZoomLevel * mouseDistance / _tpConfig.MapZoomInDistance;
                    targetZoomLevel = Math.Max(targetZoomLevel, minZoomLevel);
                    if (currentZoomLevel > minZoomLevel + _tpConfig.PrecisionThreshold)
                    {
                        await AdjustMapZoomLevel(currentZoomLevel, targetZoomLevel);
                        double nextZoomLevel = GetBigMapZoomLevel(CaptureToRectArea());
                        totalMoveMouseX *= currentZoomLevel / nextZoomLevel;
                        totalMoveMouseY *= currentZoomLevel / nextZoomLevel;
                        mouseDistance *= currentZoomLevel / nextZoomLevel;
                        currentZoomLevel = nextZoomLevel;
                    }
                }
            }

            // 非常接近目标点，不再进一步调整
            if (mouseDistance < _tpConfig.Tolerance)
            {
                Logger.LogDebug("移动 {I} 次鼠标后，已经接近目标点，不再移动地图。", iteration + 1);
                break;
            }

            int moveMouseX = (int)Math.Min(totalMoveMouseX, _tpConfig.MaxMouseMove * totalMoveMouseX / mouseDistance) * Math.Sign(xOffset);
            int moveMouseY = (int)Math.Min(totalMoveMouseY, _tpConfig.MaxMouseMove * totalMoveMouseY / mouseDistance) * Math.Sign(yOffset);
            double moveMouseLength = Math.Sqrt(moveMouseX * moveMouseX + moveMouseY * moveMouseY);
            int moveSteps = Math.Max((int)moveMouseLength / 10, 3); // 每次移动的步数最小为 3，避免除 0 错误

            await MouseMoveMap(moveMouseX, moveMouseY, moveSteps);
            try
            {
                exceptionTimes = 0;
                mapCenterPoint = GetPositionFromBigMap(mapName); // 随循环更新的地图中心
            }
            catch (Exception)
            {
                if (++exceptionTimes > 2)
                {
                    throw new Exception("多次中心点识别失败，重新传送");
                }

                Logger.LogWarning("中心点识别失败，预测移动的距离");
                mapCenterPoint += new Point2f((float)(moveMouseX * currentZoomLevel / _tpConfig.MapScaleFactor),
                    (float)(moveMouseY * currentZoomLevel / _tpConfig.MapScaleFactor));
            }

            (xOffset, yOffset) = (x - mapCenterPoint.X, y - mapCenterPoint.Y);
            totalMoveMouseX = _tpConfig.MapScaleFactor * Math.Abs(xOffset) / currentZoomLevel;
            totalMoveMouseY = _tpConfig.MapScaleFactor * Math.Abs(yOffset) / currentZoomLevel;
            mouseDistance = Math.Sqrt(totalMoveMouseX * totalMoveMouseX + totalMoveMouseY * totalMoveMouseY);
        }
    }

    /// <summary>
    /// 点击并移动鼠标
    /// </summary>
    /// <param name="x1">鼠标初始位置x</param>
    /// <param name="y1">鼠标初始位置y</param>
    /// <param name="x2">鼠标移动后位置x</param> 
    /// <param name="y2">鼠标移动后位置y</param>
    public async Task MouseClickAndMove(int x1, int y1, int x2, int y2)
    {
        // GlobalMethod.MoveMouseTo(x1, y1);
        GameCaptureRegion.GameRegionMove((rect, scale) => (x1 * scale, y1 * scale));
        await Delay(50, ct);
        GlobalMethod.LeftButtonDown();
        await Delay(50, ct);
        // GlobalMethod.MoveMouseTo(x2, y2);
        GameCaptureRegion.GameRegionMove((rect, scale) => (x2 * scale, y2 * scale));
        await Delay(50, ct);
        GlobalMethod.LeftButtonUp();
        await Delay(50, ct);
        GameCaptureRegion.GameRegionMove((rect, scale) => (rect.Width / 2d, rect.Width / 2d));
    }

    /// <summary>
    /// 调整地图缩放级别以加速移动
    /// </summary>
    /// <param name="zoomIn">是否放大地图</param>
    [Obsolete]
    private async Task AdjustMapZoomLevel(bool zoomIn)
    {
        if (zoomIn)
        {
            GameCaptureRegion.GameRegionClick((rect, scale) => (_tpConfig.ZoomButtonX * scale, _zoomInButtonY * scale));
        }
        else
        {
            GameCaptureRegion.GameRegionClick((rect, scale) => (_tpConfig.ZoomButtonX * scale, _zoomOutButtonY * scale));
        }

        await Delay(100, ct);
    }


    /// <summary>
    /// 调整地图的缩放等级（整数缩放级别）。
    /// </summary>
    /// <param name="zoomLevel">目标等级：1-6。整数。随着数字变大地图越小，细节越少。</param>
    [Obsolete]
    public async Task AdjustMapZoomLevel(int zoomLevel)
    {
        for (int i = 0; i < 5; i++)
        {
            await AdjustMapZoomLevel(false);
        }

        await Delay(200, ct);
        for (int i = 0; i < 6 - zoomLevel; i++)
        {
            await AdjustMapZoomLevel(true);
        }
    }

    /// <summary>
    /// 将大地图缩放等级设置为指定值
    /// </summary>
    /// <remarks>
    /// 缩放等级说明：
    /// - 数值范围：1.0(最大地图) 到 6.0(最小地图)
    /// - 缩放效果：数值越大，地图显示范围越广，细节越少
    /// - 缩放位置：1.0 对应缩放条最上方，6.0 对应缩放条最下方
    /// - 推荐范围：建议在 2.0 到 5.0 之间调整，过大或过小可能影响操作
    /// </remarks>
    /// <param name="zoomLevel">当前缩放等级：1.0-6.0，浮点数。</param>
    /// <param name="targetZoomLevel">目标缩放等级：1.0-6.0，浮点数。</param>
    public async Task AdjustMapZoomLevel(double zoomLevel, double targetZoomLevel)
    {
        // Logger.LogInformation("调整地图缩放等级：{zoomLevel:0.000} -> {targetZoomLevel:0.000}", zoomLevel, targetZoomLevel);
        
        // 根据输入模式选择不同的缩放方式
        if (Simulation.CurrentInputMode == InputMode.XInput)
        {
            // 手柄模式：使用LT/RT扳机缩放
            Logger.LogInformation("🎮 手柄模式：使用扳机缩放地图 {ZoomLevel:0.00} -> {TargetZoomLevel:0.00}", 
                zoomLevel, targetZoomLevel);
            
            // 计算需要缩放的次数（每次按扳机大约改变0.5级）
            double zoomDiff = targetZoomLevel - zoomLevel;
            int zoomCount = (int)Math.Abs(zoomDiff * 2); // 每0.5级按一次
            
            if (zoomDiff > 0)
            {
                // 需要缩小地图（增大缩放等级）-> 使用RT（右扳机）
                Logger.LogInformation("  → 使用RT缩小地图，按{Count}次", zoomCount);
                for (int i = 0; i < zoomCount; i++)
                {
                    Simulation.SimulateAction(GIActions.ElementalBurst); // RT映射到ElementalBurst
                    await Delay(100, ct);
                }
            }
            else if (zoomDiff < 0)
            {
                // 需要放大地图（减小缩放等级）-> 使用LT（左扳机）
                Logger.LogInformation("  → 使用LT放大地图，按{Count}次", zoomCount);
                for (int i = 0; i < zoomCount; i++)
                {
                    Simulation.SetLeftTrigger(255);
                    await Delay(50, ct);
                    Simulation.SetLeftTrigger(0);
                    await Delay(100, ct);
                }
            }
            
            await Delay(200, ct); // 等待缩放完成
        }
        else
        {
            // 键鼠模式：拖动缩放滑块
            int initialY = (int)(_tpConfig.ZoomStartY + (_tpConfig.ZoomEndY - _tpConfig.ZoomStartY) * (zoomLevel - 1) / 5d);
            int targetY = (int)(_tpConfig.ZoomStartY + (_tpConfig.ZoomEndY - _tpConfig.ZoomStartY) * (targetZoomLevel - 1) / 5d);
            await MouseClickAndMove(_tpConfig.ZoomButtonX, initialY, _tpConfig.ZoomButtonX, targetY);
            await Delay(100, ct);
        }
    }

    private async Task MouseMoveMap(int pixelDeltaX, int pixelDeltaY, int steps = 10)
    {
        // 根据输入模式选择不同的移动方式
        if (Simulation.CurrentInputMode == InputMode.XInput)
        {
            // 手柄模式：使用左摇杆移动地图
            Logger.LogInformation("🎮 手柄模式：使用左摇杆移动地图 ΔX={DeltaX}, ΔY={DeltaY}", 
                pixelDeltaX, pixelDeltaY);
            
            // 计算移动距离和方向
            double distance = Math.Sqrt(pixelDeltaX * pixelDeltaX + pixelDeltaY * pixelDeltaY);
            if (distance < 1)
            {
                Logger.LogDebug("移动距离太小，跳过");
                return;
            }
            
            // 归一化方向
            double dirX = pixelDeltaX / distance;
            double dirY = pixelDeltaY / distance;
            
            // 计算移动时间（距离越大，时间越长）
            int moveDurationMs = (int)Math.Min(distance * 5, 2000); // 最多2秒
            
            // 计算摇杆强度（固定使用较大的强度以加快移动）
            short stickX = (short)(dirX * 25000); // 使用75%的最大强度
            short stickY = (short)(-dirY * 25000); // Y轴反向
            
            Logger.LogInformation("  → 摇杆方向: ({DirX:F2}, {DirY:F2}), 持续时间: {Duration}ms", 
                dirX, dirY, moveDurationMs);
            Logger.LogInformation("  → 摇杆坐标: X={StickX}, Y={StickY}", stickX, stickY);
            
            // 推动摇杆
            Simulation.SetLeftStick(stickX, stickY);
            await Delay(moveDurationMs, ct);
            
            // 释放摇杆
            Simulation.SetLeftStick(0, 0);
            await Delay(100, ct);
        }
        else
        {
            // 键鼠模式：拖动鼠标移动地图
            double dpi = TaskContext.Instance().DpiScale;
            int[] stepX = GenerateSteps((int)(pixelDeltaX / dpi), steps);
            int[] stepY = GenerateSteps((int)(pixelDeltaY / dpi), steps);

            // 随机起点以避免地图移动无效
            GameCaptureRegion.GameRegionMove((rect, _) =>
                (rect.Width / 2d + Random.Shared.Next(-rect.Width / 6, rect.Width / 6),
                    rect.Height / 2d + Random.Shared.Next(-rect.Height / 6, rect.Height / 6)));

            Simulation.SendInput.Mouse.LeftButtonDown();
            for (var i = 0; i < steps; i++)
            {
                var i1 = i;
                await Delay(_tpConfig.StepIntervalMilliseconds, ct);
                // Simulation.SendInput.Mouse.MoveMouseBy(stepX[i], stepY[i]);
                GameCaptureRegion.GameRegionMoveBy((_, scale) => (stepX[i1] * scale, stepY[i1] * scale));
            }

            Simulation.SendInput.Mouse.LeftButtonUp();
        }
    }

    private int[] GenerateSteps(int delta, int steps)
    {
        double[] factors = new double[steps];
        double sum = 0;
        for (int i = 0; i < steps; i++)
        {
            factors[i] = Math.Cos(i * Math.PI / (2 * steps));
            sum += factors[i];
        }

        int[] stepsArr = new int[steps];
        int remaining = delta;

        // 两阶段分配：基础值 + 余数补偿
        for (int i = 0; i < steps; i++)
        {
            double ratio = factors[i] / sum;
            stepsArr[i] = (int)(delta * ratio); // 基础值
            remaining -= stepsArr[i];
        }

        int center = steps / 2;
        for (int r = 0; r < Math.Abs(remaining); r++)
        {
            int target = (center + r) % steps; // 从中点开始螺旋分配
            stepsArr[target] += remaining > 0 ? 1 : -1;
        }

        return stepsArr;
    }

    public Point2f GetPositionFromBigMap(string mapName)
    {
        return GetBigMapCenterPoint(mapName);
    }

    public Point2f? GetPositionFromBigMapNullable(string mapName)
    {
        try
        {
            return GetBigMapCenterPoint(mapName);
        }
        catch
        {
            return null;
        }
    }

    public Rect GetBigMapRect(string mapName)
    {
        var rect = new Rect();
        NewRetry.Do(() =>
        {
            // 判断是否在地图界面
            using var ra = CaptureToRectArea();
            using var mapScaleButtonRa = ra.Find(QuickTeleportAssets.Instance.MapScaleButtonRo);
            if (mapScaleButtonRa.IsExist())
            {
                rect = MapManager.GetMap(mapName, _mapMatchingMethod).GetBigMapRect(ra.CacheGreyMat);
                if (rect == default)
                {
                    // 滚轮调整后再次识别
                    Simulation.SendInput.Mouse.VerticalScroll(2);
                    Sleep(500);
                    throw new RetryException("识别大地图位置失败");
                }
            }
            else
            {
                throw new RetryException("当前不在地图界面");
            }
        }, TimeSpan.FromMilliseconds(500), 5);

        if (rect == default)
        {
            throw new InvalidOperationException("多次重试后，识别大地图位置失败");
        }

        Debug.WriteLine("识别大地图在全地图位置矩形：" + rect);
        // 提瓦特大陆由于用的256的图，需要做特殊逻辑
        if (mapName == MapTypes.Teyvat.ToString())
        {
            const int s = TeyvatMap.BigMap256ScaleTo2048; // 相对2048做8倍缩放
            rect = new Rect(rect.X * s, rect.Y * s, rect.Width * s, rect.Height * s);
        }

        return MapManager.GetMap(mapName, _mapMatchingMethod).ConvertImageCoordinatesToGenshinMapCoordinates(rect)!.Value;
    }

    public Point2f GetBigMapCenterPoint(string mapName)
    {
        // 判断是否在地图界面
        using var ra = CaptureToRectArea();
        using var mapScaleButtonRa = ra.Find(QuickTeleportAssets.Instance.MapScaleButtonRo);
        if (mapScaleButtonRa.IsExist())
        {
            var p = MapManager.GetMap(mapName, _mapMatchingMethod).GetBigMapPosition(ra.CacheGreyMat);
            if (p.IsEmpty())
            {
                throw new InvalidOperationException("识别大地图位置失败");
            }

            Debug.WriteLine("识别大地图在全地图位置：" + p);
            // 提瓦特大陆由于用的256的图，需要做特殊逻辑
            var (x, y) = (p.X, p.Y);
            if (mapName == MapTypes.Teyvat.ToString())
            {
                (x, y) = (p.X * TeyvatMap.BigMap256ScaleTo2048, p.Y * TeyvatMap.BigMap256ScaleTo2048);
            }

            return MapManager.GetMap(mapName, _mapMatchingMethod).ConvertImageCoordinatesToGenshinMapCoordinates(new Point2f(x, y))!.Value;
        }
        else
        {
            throw new InvalidOperationException("当前不在地图界面");
        }
    }

    /// <summary>
    /// 获取最接近的N个传送点坐标和所处区域
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="n">获取最近的 n 个传送点</param>
    /// <returns></returns>
    public List<GiTpPosition> GetNearestNTpPoints(double x, double y, string mapName, int n = 1)
    {
        // 检查 n 的合法性
        if (n < 1)
        {
            throw new ArgumentException("The value of n must be greater than or equal to 1.", nameof(n));
        }

        // 按距离排序并选择前 n 个点
        return MapLazyAssets.Instance.ScenesDic[mapName].Points
            .OrderBy(tp => Math.Pow(tp.X - x, 2) + Math.Pow(tp.Y - y, 2))
            .Take(n)
            .ToList();
    }

    public async Task<bool> SwitchRecentlyCountryMap(double x, double y, string? forceCountry = null)
    {
        // 可能是地下地图，切换到地上地图
        using var ra2 = CaptureToRectArea();
        if (Bv.BigMapIsUnderground(ra2))
        {
            ra2.Find(_assets.MapUndergroundToGroundButtonRo).Click();
            await Delay(200, ct);
        }

        // 识别当前位置
        var minDistance = double.MaxValue;
        var bigMapCenterPointNullable = GetPositionFromBigMapNullable(MapTypes.Teyvat.ToString());

        if (bigMapCenterPointNullable != null)
        {
            var bigMapCenterPoint = bigMapCenterPointNullable.Value;
            Logger.LogDebug("识别当前大地图位置：{Pos}", bigMapCenterPoint);
            minDistance = Math.Sqrt(Math.Pow(bigMapCenterPoint.X - x, 2) + Math.Pow(bigMapCenterPoint.Y - y, 2));
            if (minDistance < 50)
            {
                // 点位很近的情况下不切换
                return false;
            }
        }

        string minCountry = "当前位置";
        foreach (var (country, position) in MapLazyAssets.Instance.CountryPositions)
        {
            var distance = Math.Sqrt(Math.Pow(position[0] - x, 2) + Math.Pow(position[1] - y, 2));
            if (distance < minDistance)
            {
                minDistance = distance;
                minCountry = country;
            }
        }

        Logger.LogDebug("离目标传送点最近的区域是：{Country}", minCountry);
        if (minCountry != "当前位置")
        {
            if (forceCountry != null)
            {
                minCountry = forceCountry;
            }

            await SwitchArea(minCountry);
            return true;
        }

        return false;
    }

    internal async Task SwitchArea(string areaName)
    {
        // 根据输入模式选择不同的打开方式
        if (Simulation.CurrentInputMode == InputMode.XInput)
        {
            // 手柄模式：按Y键打开地区选择菜单
            Logger.LogInformation("🎮 手柄模式：按Y键打开地区选择菜单");
            Simulation.SimulateAction(GIActions.PickUpOrInteract); // Y键映射到PickUpOrInteract
            await Delay(500, ct);
            
            // 先移动到左上角第一个位置（蒙德）
            Logger.LogInformation("  → 重置到左上角起始位置（蒙德）");
            for (int i = 0; i < 10; i++)
            {
                Simulation.SetLeftStick(0, 32767); // 向上
                await Delay(80, ct);
                Simulation.SetLeftStick(0, 0);
                await Delay(50, ct);
            }
            for (int i = 0; i < 10; i++)
            {
                Simulation.SetLeftStick(-32767, 0); // 向左
                await Delay(80, ct);
                Simulation.SetLeftStick(0, 0);
                await Delay(50, ct);
            }
            await Delay(200, ct);
        }
        else
        {
            // 键鼠模式：点击右下角按钮
            GameCaptureRegion.GameRegionClick((rect, scale) => (rect.Width - 160 * scale, rect.Height - 60 * scale));
            await Delay(300, ct);
        }
        
        using var ra = CaptureToRectArea();
        var list = ra.FindMulti(new RecognitionObject
        {
            RecognitionType = RecognitionTypes.Ocr,
            RegionOfInterest = new Rect(ra.Width * 2 / 3, 0, ra.Width / 3, ra.Height),
            ReplaceDictionary = new Dictionary<string, string[]>
            {
                ["渊下宫"] = ["渊下宮"],
            },
        });
        
        string minCountryLocalized = this.stringLocalizer.WithCultureGet(this.cultureInfo, areaName);
        Region? matchRect = list.FirstOrDefault(r => r.Text.Contains(minCountryLocalized));
        
        if (matchRect == null)
        {
            Logger.LogWarning("切换区域失败：{Country}", areaName);
            if (areaName == MapTypes.TheChasm.GetDescription() || areaName == MapTypes.Enkanomiya.GetDescription() || areaName == MapTypes.SeaOfBygoneEras.GetDescription() || areaName == MapTypes.AncientSacredMountain.GetDescription())
            {
                throw new Exception($"切换独立地图区域[{areaName}]失败");
            }
        }
        else
        {
            if (Simulation.CurrentInputMode == InputMode.XInput)
            {
                // 手柄模式：从蒙德位置导航到目标地区
                Logger.LogInformation("🎮 手柄模式：从蒙德导航到地区 {AreaName}", areaName);
                
                // 定义地区网格布局（从左到右，从上到下）
                // 第1行：蒙德(0,0), 璃月(1,0)
                // 第2行：稻妻(0,1), 须弥(1,1)
                // 第3行：枫丹(0,2), 纳塔(1,2)
                // 第4行：挪德卡莱(0,3)
                // 后面是独立地图...
                var regionGrid = new Dictionary<string, (int x, int y)>
                {
                    ["蒙德"] = (0, 0),
                    ["璃月"] = (1, 0),
                    ["稻妻"] = (0, 1),
                    ["须弥"] = (1, 1),
                    ["枫丹"] = (0, 2),
                    ["纳塔"] = (1, 2),
                    ["挪德卡莱"] = (0, 3),
                    ["渊下宫"] = (0, 4),
                    ["层岩巨渊·地下矿区"] = (1, 4),
                    ["旧日之海"] = (0, 5),
                    ["远古圣山"] = (1, 5),
                };
                
                if (regionGrid.TryGetValue(areaName, out var targetPos))
                {
                    Logger.LogInformation("  → 目标地区网格位置: ({X}, {Y})", targetPos.x, targetPos.y);
                    
                    // 从蒙德(0,0)移动到目标位置
                    // 先向右移动
                    for (int i = 0; i < targetPos.x; i++)
                    {
                        Logger.LogDebug("  → 向右移动");
                        Simulation.SetLeftStick(32767, 0); // 向右
                        await Delay(150, ct);
                        Simulation.SetLeftStick(0, 0);
                        await Delay(100, ct);
                    }
                    
                    // 再向下移动
                    for (int i = 0; i < targetPos.y; i++)
                    {
                        Logger.LogDebug("  → 向下移动");
                        Simulation.SetLeftStick(0, -32767); // 向下
                        await Delay(150, ct);
                        Simulation.SetLeftStick(0, 0);
                        await Delay(100, ct);
                    }
                    
                    Logger.LogInformation("  → 已到达目标地区，按A键确认");
                }
                else
                {
                    Logger.LogWarning("  → 未知地区网格位置，尝试直接确认");
                }
                
                // 按A键确认选择
                Simulation.SimulateAction(GIActions.Jump);
                await Delay(300, ct);
            }
            else
            {
                // 键鼠模式：直接点击
                matchRect.Click();
            }
            Logger.LogInformation("切换到区域：{Country}", areaName);
        }

        await Delay(500, ct);
    }

    public async Task Tp(string name)
    {
        // 通过大地图传送到指定传送点
        await Delay(500, ct);
    }

    public async Task TpByF1(string name)
    {
        // 传送到指定传送点
        await Delay(500, ct);
    }

    public async Task ClickTpPoint(ImageRegion imageRegion)
    {
        // 1.判断是否在地图界面
        if (!Bv.IsInBigMapUi(imageRegion)) throw new RetryException("不在地图界面");

        // 2. 判断是否已经点出传送按钮
        var hasTeleportButton = CheckTeleportButton(imageRegion);
        await Delay(50, ct);
        if (hasTeleportButton) return;   // 可以传送了，结束
        
        // 3. 没点出传送按钮，且不存在外部地图关闭按钮
        // 说明只有两种可能，a. 点出来的是未激活传送点或者标点 b. 选择传送点选项列表
        var mapCloseRa1 = imageRegion.Find(_assets.MapCloseButtonRo);
        if (!mapCloseRa1.IsEmpty()) throw new TpPointNotActivate("传送点未激活或不存在");

        // 手柄模式下，按A键后需要等待选项列表弹出
        if (Simulation.CurrentInputMode == InputMode.XInput)
        {
            Logger.LogDebug("🎮 手柄模式：等待选项列表弹出...");
            await Delay(500, ct);
        }

        // 4. 循环判断选项列表是否有传送点(未激活点位也在里面)
        var hasMapChooseIcon = CheckMapChooseIcon(imageRegion);
        // 没有传送点说明不是传送点
        // 临时注释：手柄模式下可能自动选中第一个选项，跳过图标识别检查
        if (!hasMapChooseIcon)
        {
            if (Simulation.CurrentInputMode == InputMode.XInput)
            {
                Logger.LogWarning("⚠️ 手柄模式：未识别到图标，但继续尝试等待传送按钮（可能已自动选中）");
            }
            else
            {
                throw new TpPointNotActivate("选项列表不存在传送点");
            }
        }
        
        Logger.LogInformation("🔍 开始等待传送按钮出现...");
        
        // 等待传送点详情页面完全打开
        await Delay(1000, ct);
        
        // 重新获取当前画面
        using var currentRa = CaptureToRectArea();
        
        var teleportButtonFound = await NewRetry.WaitForElementAppear(
            _assets.TeleportButtonRo,
            () => { },
            ct,
            6,
            300
        );
        
        if (teleportButtonFound)
        {
            Logger.LogInformation("✅ 成功识别到传送按钮");
        }
        else
        {
            Logger.LogWarning("❌ 未识别到传送按钮");
        }
        
        if (!teleportButtonFound) throw new TpPointNotActivate("选项列表的传送点未激活");
        
        // 根据输入模式选择不同的确认方式
        if (Simulation.CurrentInputMode == InputMode.XInput)
        {
            // 手柄模式：按A键确认传送
            Logger.LogInformation("🎮 手柄模式：按A键确认传送");
            await Delay(300, ct);
            Simulation.SimulateAction(GIActions.Jump); // A键
            await Delay(300, ct);
        }
        else
        {
            // 键鼠模式：点击传送按钮
            await NewRetry.WaitForElementDisappear(
                _assets.TeleportButtonRo,
                screen =>
                {
                    screen.Find(_assets.TeleportButtonRo, ra =>
                    {
                        ra.Click();
                        ra.Dispose();
                    });
                },
                ct,
                6,
                300
            );
        }
    }

    private bool CheckTeleportButton(ImageRegion imageRegion)
    {
        var hasTeleportButton = false;
        imageRegion.Find(_assets.TeleportButtonRo, ra =>
        {
            // 根据输入模式选择不同的确认方式
            if (Simulation.CurrentInputMode == InputMode.XInput)
            {
                // 手柄模式：按A键确认
                Logger.LogInformation("🎮 手柄模式：检测到传送按钮，按A键确认");
                Simulation.SimulateAction(GIActions.Jump); // A键
            }
            else
            {
                // 键鼠模式：点击按钮
                ra.Click();
            }
            hasTeleportButton = true;
        });
        return hasTeleportButton;
    }

    /// <summary>
    /// 全匹配一遍并进行文字识别
    /// 60ms ~200ms
    /// </summary>
    /// <param name="imageRegion"></param>
    /// <returns></returns>
    private bool CheckMapChooseIcon(ImageRegion imageRegion)
    {
        var hasMapChooseIcon = false;

        // 全匹配一遍
        var rResultList = MatchTemplateHelper.MatchMultiPicForOnePic(imageRegion.CacheGreyMat[_assets.MapChooseIconRoi], _assets.MapChooseIconGreyMatList);
        
        Logger.LogDebug("CheckMapChooseIcon: 识别到 {Count} 个图标", rResultList.Count);
        
        // 按高度排序
        if (rResultList.Count > 0)
        {
            rResultList = [.. rResultList.OrderBy(x => x.Y)];
            // 点击最高的
            foreach (var iconRect in rResultList)
            {
                Logger.LogDebug("  → 图标位置: X={X}, Y={Y}, W={W}, H={H}", 
                    iconRect.X, iconRect.Y, iconRect.Width, iconRect.Height);
                
                // 200宽度的文字区域
                using var ra = imageRegion.DeriveCrop(_assets.MapChooseIconRoi.X + iconRect.X + iconRect.Width, _assets.MapChooseIconRoi.Y + iconRect.Y - 8, 200, iconRect.Height + 16);
                using var textRegion = ra.Find(new RecognitionObject
                {
                    // RecognitionType = RecognitionTypes.Ocr,
                    RecognitionType = RecognitionTypes.ColorRangeAndOcr,
                    LowerColor = new Scalar(249, 249, 249), // 只取白色文字
                    UpperColor = new Scalar(255, 255, 255),
                });
                
                Logger.LogDebug("  → OCR识别文字: '{Text}'", textRegion.Text);
                
                if (string.IsNullOrEmpty(textRegion.Text) || textRegion.Text.Length == 1)
                {
                    continue;
                }

                Logger.LogInformation("传送：点击 {Option}", textRegion.Text.Replace(">", ""));
                var time = TaskContext.Instance().Config.QuickTeleportConfig.TeleportListClickDelay;
                time = time < 500 ? 500 : time;
                Thread.Sleep(time);
                
                // 根据输入模式选择不同的点击方式
                if (Simulation.CurrentInputMode == InputMode.XInput)
                {
                    // 手柄模式：按A键选择
                    Logger.LogInformation("🎮 手柄模式：按A键选择传送选项");
                    Simulation.SimulateAction(GIActions.Jump);
                }
                else
                {
                    // 键鼠模式：点击选项
                    ra.Click();
                }
                
                hasMapChooseIcon = true;
                break;
            }
        }
        else
        {
            Logger.LogWarning("CheckMapChooseIcon: 未识别到任何图标！");
        }

        return hasMapChooseIcon;
    }

    /// <summary>
    /// 给定的映射关系可以表示成 (x, y) 对的形式，其中 x 是输入值，y 是输出值
    ///    1 - 1
    ///  0.8 - 2
    ///  0.6 - 3
    ///  0.4 - 4
    ///  0.2 - 5
    ///    0 - 6
    /// y=−5x+6
    /// </summary>
    /// <param name="region"></param>
    /// <returns></returns>
    public double GetBigMapZoomLevel(ImageRegion region)
    {
        var s = Bv.GetBigMapScale(region);
        // 1~6 的缩放等级
        return (-5 * s) + 6;
    }
}
