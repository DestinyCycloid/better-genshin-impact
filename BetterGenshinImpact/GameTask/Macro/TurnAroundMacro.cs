using BetterGenshinImpact.Core.Simulator;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BetterGenshinImpact.GameTask.Macro
{
    public class TurnAroundMacro
    {
        private static bool _toggleModeRunning = false;  // 切换模式的状态
        private static CancellationTokenSource? _cts = null;
        
        /// <summary>
        /// 切换转圈状态（开/关）- 用于切换模式
        /// </summary>
        public static void Toggle()
        {
            _toggleModeRunning = !_toggleModeRunning;
            
            if (_toggleModeRunning)
            {
                // 开启时启动后台循环
                _cts = new CancellationTokenSource();
                Task.Run(() => ToggleLoop(_cts.Token));
            }
            else
            {
                // 关闭时停止后台循环并重置摇杆
                _cts?.Cancel();
                _cts = null;
                Stop();
            }
        }
        
        /// <summary>
        /// 切换模式的后台循环
        /// </summary>
        private static void ToggleLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _toggleModeRunning)
            {
                DoRotate();
            }
        }
        
        /// <summary>
        /// 执行一次转圈操作 - 用于长按模式
        /// </summary>
        public static void Done()
        {
            DoRotate();
        }
        
        /// <summary>
        /// 实际执行转圈的逻辑
        /// </summary>
        private static void DoRotate()
        {
            var config = TaskContext.Instance().Config.MacroConfig;
            
            if (config.RunaroundMouseXInterval == 0)
            {
                config.RunaroundMouseXInterval = 1;
            }

            // 根据当前输入模式选择不同的实现
            if (Simulation.CurrentInputMode == InputMode.XInput)
            {
                // 手柄模式：控制右摇杆
                // 将鼠标移动距离转换为摇杆值（-32768 到 32767）
                // 转换系数：调大这个值可以让转圈更快
                int stickValue = (int)(config.RunaroundMouseXInterval * 200.0); // 转换系数 200
                stickValue = Math.Clamp(stickValue, -32767, 32767);
                
                Simulation.SetRightStick((short)stickValue, 0);
                // 手柄模式下摇杆会持续保持这个位置，所以需要延迟
                Thread.Sleep(config.RunaroundInterval);
            }
            else
            {
                // 键鼠模式：移动鼠标
                Simulation.SendInput.Mouse.MoveMouseBy(config.RunaroundMouseXInterval, 0);
                Thread.Sleep(config.RunaroundInterval);
            }
        }
        
        /// <summary>
        /// 停止转圈（重置摇杆到中心）
        /// </summary>
        public static void Stop()
        {
            if (Simulation.CurrentInputMode == InputMode.XInput)
            {
                Simulation.SetRightStick(0, 0);
            }
        }
    }
}
