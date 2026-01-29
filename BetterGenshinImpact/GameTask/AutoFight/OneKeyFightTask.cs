using BetterGenshinImpact.Core.Config;
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
        // 强制使用"玛薇卡"的宏（测试模式）
        Logger.LogWarning("⚠️ 测试模式：强制使用玛薇卡的宏");
        
        ExecuteMacro(ct);
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 执行宏命令
    /// </summary>
    private void ExecuteMacro(CancellationToken ct)
    {
        if (_avatarMacros != null && _avatarMacros.Count > 0)
        {
            string targetAvatar = "玛薇卡";
            if (!_avatarMacros.ContainsKey(targetAvatar))
            {
                Logger.LogError("未找到角色 {Avatar} 的宏配置", targetAvatar);
                return;
            }
            
            Logger.LogInformation("使用角色宏: {Avatar}", targetAvatar);
            
            var commands = _avatarMacros[targetAvatar];
            if (commands != null && commands.Count > 0)
            {
                // 创建一个临时的 CombatScenes 和 Avatar 对象来执行宏命令
                var tempCombatScenes = new CombatScenes();
                var tempAvatar = new Avatar(tempCombatScenes, targetAvatar, 1, new Rect(0, 0, 100, 100));
                foreach (var command in commands)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    try
                    {
                        command.Execute(tempAvatar);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "执行宏命令失败: {Command}", command);
                    }
                }
            }
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