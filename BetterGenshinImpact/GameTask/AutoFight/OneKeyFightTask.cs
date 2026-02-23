using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.AutoFight.Assets;
using BetterGenshinImpact.GameTask.AutoFight.Model;
using BetterGenshinImpact.GameTask.AutoFight.Script;
using BetterGenshinImpact.Model;
using BetterGenshinImpact.Service;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static BetterGenshinImpact.GameTask.Common.TaskControl;

namespace BetterGenshinImpact.GameTask.AutoFight;

/// <summary>
/// 一键战斗宏
/// </summary>
public class OneKeyFightTask : Singleton<OneKeyFightTask>
{
    public static readonly string HoldOnMode = "按住时重复";
    public static readonly string TickMode = "触发";

    private Dictionary<string, List<CombatCommand>>? _avatarMacros;
    private CancellationTokenSource? _cts = null;
    private Task? _fightTask;

    private bool _isKeyDown = false;
    private int _activeMacroPriority = -1;
    private DateTime _lastUpdateTime = DateTime.MinValue;

    private CombatScenes? _currentCombatScenes;

    public void KeyDown()
    {
        if (_isKeyDown || !IsEnabled())
        {
            return;
        }

        _isKeyDown = true;
        if (_activeMacroPriority != TaskContext.Instance().Config.MacroConfig.CombatMacroPriority ||
            IsAvatarMacrosEdited())
        {
            _activeMacroPriority = TaskContext.Instance().Config.MacroConfig.CombatMacroPriority;
            _avatarMacros = LoadAvatarMacros();
            Logger.LogInformation("加载一键宏配置完成");
        }

        if (IsHoldOnMode())
        {
            if (_cts == null || _cts.Token.IsCancellationRequested)
            {
                _cts = new CancellationTokenSource();
                _fightTask = FightTask(_cts.Token);
                if (!_fightTask.IsCompleted)
                {
                    _fightTask.Start();
                }
            }
        }
        else if (IsTickMode())
        {
            // 触发模式：每次按键都启动新任务（如果有旧任务在运行则先取消）
            if (_fightTask != null && !_fightTask.IsCompleted)
            {
                // 有任务正在运行，先取消它
                Logger.LogInformation("取消正在运行的宏任务");
                _cts?.Cancel();
                // 等待任务完成
                try
                {
                    _fightTask.Wait(100); // 最多等待100ms
                }
                catch { }
            }
            
            // 启动新任务
            Logger.LogInformation("启动新的宏任务");
            _cts = new CancellationTokenSource();
            _fightTask = Task.Run(() => ExecuteMacro(_cts.Token));
            
            // 触发模式下立即重置 _isKeyDown，允许下次按键
            _isKeyDown = false;
        }
    }

    public void KeyUp()
    {
        _isKeyDown = false;
        if (!IsEnabled())
        {
            return;
        }

        if (IsHoldOnMode())
        {
            _cts?.Cancel();
        }
    }

    // public void Run()
    // {
    //     if (!IsEnabled())
    //     {
    //         return;
    //     }
    //     _avatarMacros ??= LoadAvatarMacros();
    //
    //     if (IsHoldOnMode())
    //     {
    //         if (_fightTask == null || _fightTask.IsCompleted)
    //         {
    //             _fightTask = FightTask(_cts);
    //             _fightTask.Start();
    //         }
    //         Thread.Sleep(100);
    //     }
    //     else if (IsTickMode())
    //     {
    //         if (_cts.Token.IsCancellationRequested)
    //         {
    //             _cts = new CancellationTokenSource();
    //             Task.Run(() => FightTask(_cts));
    //         }
    //         else
    //         {
    //             _cts.Cancel();
    //         }
    //     }
    // }

    /// <summary>
    /// 循环执行战斗宏
    /// </summary>
    private Task FightTask(CancellationToken ct)
    {
        // 异步执行，避免阻塞
        Task.Run(async () =>
        {
            // 等待画面稳定
            await Task.Delay(300);
            ExecuteMacro(ct);
        });
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 执行宏命令
    /// </summary>
    private void ExecuteMacro(CancellationToken ct)
    {
        if (_avatarMacros == null || _avatarMacros.Count == 0)
        {
            Logger.LogError("❌ [一键宏] 未加载宏配置");
            return;
        }
        
        // 获取当前角色
        string? targetAvatar = GetCurrentAvatar();
        if (string.IsNullOrEmpty(targetAvatar))
        {
            Logger.LogError("❌ [一键宏] 无法识别当前角色");
            return;
        }
        
        if (!_avatarMacros.ContainsKey(targetAvatar))
        {
            Logger.LogWarning("⚠️ [一键宏] 未找到角色 {Avatar} 的宏配置", targetAvatar);
            return;
        }
        
        var commands = _avatarMacros[targetAvatar];
        if (commands == null || commands.Count == 0)
        {
            Logger.LogWarning("⚠️ [一键宏] 角色 {Avatar} 的命令列表为空", targetAvatar);
            return;
        }
        
        Logger.LogInformation("✅ [一键宏] 执行角色 {Avatar} 的宏 ({Count} 条命令)", targetAvatar, commands.Count);
        
        // 创建一个临时的 CombatScenes 和 Avatar 对象来执行宏命令
        var tempCombatScenes = new CombatScenes();
        var tempAvatar = new Avatar(tempCombatScenes, targetAvatar, 1, new Rect(0, 0, 100, 100));
        
        int executedCount = 0;
        foreach (var command in commands)
        {
            if (ct.IsCancellationRequested)
            {
                Logger.LogInformation("⏸️ [一键宏] 宏执行被取消 ({Count}/{Total})", executedCount, commands.Count);
                break;
            }
            try
            {
                command.Execute(tempAvatar);
                executedCount++;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ [一键宏] 执行命令失败");
            }
        }
        
        Logger.LogInformation("✅ [一键宏] 宏执行完成 ({Count}/{Total})", executedCount, commands.Count);
    }
    
    /// <summary>
    /// 获取当前激活的角色名称
    /// </summary>
    private string? GetCurrentAvatar()
    {
        try
        {
            // 同步队伍信息
            _currentCombatScenes = RunnerContext.Instance.TrySyncCombatScenesSilent();
            
            if (_currentCombatScenes == null || !_currentCombatScenes.CheckTeamInitialized())
            {
                Logger.LogWarning("⚠️ [一键宏] 队伍识别失败");
                return null;
            }
            
            var avatars = _currentCombatScenes.GetAvatars();
            if (avatars.Count == 0)
            {
                Logger.LogWarning("⚠️ [一键宏] 未识别到任何角色");
                return null;
            }
            
            // 获取当前激活的角色索引
            int activeIndex = -1;
            bool isGamepadMode = Core.Simulator.Simulation.CurrentInputMode == Core.Simulator.InputMode.XInput;
            var captureArea = CaptureToRectArea();
            
            if (isGamepadMode)
            {
                // 手柄模式：使用箭头检测（与冷却提示相同的方法）
                var rectArray = AutoFightAssets.Instance.AvatarIndexRectListGamepad.ToArray();
                var arrowRo = AutoFightAssets.Instance.CurrentAvatarThresholdGamepadForSkillCd;
                
                var curr = captureArea.Find(arrowRo);
                if (!curr.IsEmpty())
                {
                    for (int i = 0; i < rectArray.Length; i++)
                    {
                        int bottom1 = curr.Y + curr.Height;
                        int bottom2 = rectArray[i].Y + rectArray[i].Height;
                        bool intersects = !(bottom1 < rectArray[i].Y || bottom2 < curr.Y);
                        
                        if (intersects)
                        {
                            activeIndex = i + 1;
                            break;
                        }
                    }
                }
            }
            else
            {
                // 键鼠模式：使用完整的检测逻辑
                var context = new AvatarActiveCheckContext();
                activeIndex = _currentCombatScenes.GetActiveAvatarIndex(captureArea, context);
            }
            
            if (activeIndex <= 0 || activeIndex > avatars.Count)
            {
                Logger.LogWarning("⚠️ [一键宏] 无法识别当前激活角色 (索引: {Index})", activeIndex);
                return null;
            }
            
            var currentAvatar = avatars[activeIndex - 1];
            Logger.LogInformation("✅ [一键宏] 识别到当前角色: {Avatar}", currentAvatar.Name);
            
            return currentAvatar.Name;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ [一键宏] 获取当前角色失败");
            return null;
        }
    }

    public Dictionary<string, List<CombatCommand>> LoadAvatarMacros()
    {
        var jsonPath = GetAvatarMacroJsonPath();
        var json = File.ReadAllText(jsonPath);
        _lastUpdateTime = File.GetLastWriteTime(jsonPath);
        var avatarMacros = JsonSerializer.Deserialize<List<AvatarMacro>>(json, ConfigService.JsonOptions);
        if (avatarMacros == null)
        {
            return [];
        }

        var result = new Dictionary<string, List<CombatCommand>>();
        foreach (var avatarMacro in avatarMacros)
        {
            var commands = avatarMacro.LoadCommands();
            if (commands != null)
            {
                result.Add(avatarMacro.Name, commands);
            }
        }

        return result;
    }

    public bool IsAvatarMacrosEdited()
    {
        // 通过修改时间判断是否编辑过
        var jsonPath = GetAvatarMacroJsonPath();
        var lastWriteTime = File.GetLastWriteTime(jsonPath);
        return lastWriteTime > _lastUpdateTime;
    }
    
    public static string GetAvatarMacroJsonPath()
    {
        var path = Global.Absolute("User/avatar_macro.json");
        if (!File.Exists(path))
        {
            File.Copy(Global.Absolute("User/avatar_macro_default.json"), path);
        }
        return path;
    }

    public static bool IsEnabled()
    {
        return TaskContext.Instance().Config.MacroConfig.CombatMacroEnabled;
    }

    public static bool IsHoldOnMode()
    {
        return TaskContext.Instance().Config.MacroConfig.CombatMacroHotkeyMode == HoldOnMode;
    }

    public static bool IsTickMode()
    {
        return TaskContext.Instance().Config.MacroConfig.CombatMacroHotkeyMode == TickMode;
    }
}