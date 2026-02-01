using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.GameTask.Common.Map;
using BetterGenshinImpact.GameTask.Model.Area;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static BetterGenshinImpact.GameTask.Common.TaskControl;

namespace BetterGenshinImpact.GameTask.AutoPathing;

public class CameraRotateTask(CancellationToken ct)
{
    private readonly double _dpi = TaskContext.Instance().DpiScale;

    /// <summary>
    /// 向目标角度旋转
    /// </summary>
    /// <param name="targetOrientation"></param>
    /// <param name="imageRegion"></param>
    /// <returns></returns>
    public float RotateToApproach(float targetOrientation, ImageRegion imageRegion)
    {
        var cao = CameraOrientation.Compute(imageRegion.SrcMat);
        
        // 计算角度差，确保在 -180 到 180 之间（选择短路径）
        var diff = targetOrientation - cao;
        
        // 标准化到 -180 到 180 范围
        while (diff > 180) diff -= 360;
        while (diff < -180) diff += 360;
        
        // 每3次循环打印一次日志（减少日志量）
        if (_rotateCallCount++ % 3 == 0)
        {
            Logger.LogDebug($"🎯 视角: 当前={cao:F1}°, 目标={targetOrientation:F1}°, 差={diff:F1}°");
        }
        
        if (Math.Abs(diff) < 0.5)
        {
            return diff;
        }

        // 根据输入模式选择不同的视角控制方式
        if (Simulation.CurrentInputMode == InputMode.XInput)
        {
            // 手柄模式：使用右摇杆控制视角
            // 增大死区，避免频繁微调导致抖动
            if (Math.Abs(diff) < 12)
            {
                // 角度差很小，释放摇杆，不再调整
                Simulation.SetRightStick(0, 0);
                return diff;
            }
            
            // 根据角度差选择固定的摇杆强度（降低强度，减少过冲）
            short stickStrength;
            if (Math.Abs(diff) > 90)
            {
                stickStrength = 28000; // 大角度：约85%速度（降低）
            }
            else if (Math.Abs(diff) > 45)
            {
                stickStrength = 22000; // 中等角度：约67%速度
            }
            else if (Math.Abs(diff) > 20)
            {
                stickStrength = 16000; // 小角度：约49%速度
            }
            else
            {
                stickStrength = 10000;  // 微调：约30%速度
            }
            
            // diff为正：需要向右转（摇杆向右推，正值）
            // diff为负：需要向左转（摇杆向左推，负值）
            short stickX = diff > 0 ? stickStrength : (short)-stickStrength;
            
            // 使用右摇杆控制视角（只控制X轴，Y轴保持0）
            Simulation.SetRightStick(stickX, 0);
        }
        else
        {
            // 键鼠模式：使用鼠标移动控制视角
            double controlRatio = 1;
            if (Math.Abs(diff) > 90)
            {
                controlRatio = 4;
            }
            else if (Math.Abs(diff) > 30)
            {
                controlRatio = 3;
            }
            else if (Math.Abs(diff) > 5)
            {
                controlRatio = 2;
            }

            Simulation.SendInput.Mouse.MoveMouseBy((int)Math.Round(-controlRatio * diff * _dpi), 0);
        }
        
        return diff;
    }
    
    private int _rotateCallCount = 0;

    /// <summary>
    /// 转动视角到目标角度
    /// </summary>
    /// <param name="targetOrientation">目标角度</param>
    /// <param name="maxDiff">最大误差</param>
    /// <param name="maxTryTimes">最大尝试次数（超时时间）</param>
    /// <returns></returns>
    public async Task<bool> WaitUntilRotatedTo(int targetOrientation, int maxDiff, int maxTryTimes = 60)
    {
        bool isSuccessful = false;
        int count = 0;
        while (!ct.IsCancellationRequested)
        {
            var screen = CaptureToRectArea();
            if (Math.Abs(RotateToApproach(targetOrientation, screen)) < maxDiff)
            {
                isSuccessful = true;
                break;
            }

            if (count > maxTryTimes)
            {
                Logger.LogWarning("视角转动到目标角度超时，停止转动");
                break;
            }

            await Delay(50, ct);
            count++;
        }
        
        // 手柄模式：释放右摇杆
        if (Simulation.CurrentInputMode == InputMode.XInput)
        {
            Logger.LogDebug("🎮 释放右摇杆");
            Simulation.SetRightStick(0, 0);
        }
        
        return isSuccessful;
    }
}
