using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Recognition.OCR;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.GameTask.AutoFight.Assets;
using BetterGenshinImpact.GameTask.AutoFight.Config;
using BetterGenshinImpact.GameTask.AutoFight.Model;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.View.Drawable;
using BetterGenshinImpact.GameTask.Model.Area;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Vanara.PInvoke;
using Point = System.Windows.Point;
using Rect = OpenCvSharp.Rect;

namespace BetterGenshinImpact.GameTask.SkillCd;

/// <summary>
/// 技能 CD 提示触发器
/// </summary>
public class SkillCdTrigger : ITaskTrigger
{
    public string Name => "SkillCd";
    public bool IsEnabled
    {
        get => TaskContext.Instance().Config.SkillCdConfig.Enabled;
        set => TaskContext.Instance().Config.SkillCdConfig.Enabled = value;
    }

    public int Priority => 10;
    public bool IsExclusive => false;
    /// <summary>
    /// 在所有UI场景下都运行（包括大地图），确保遮罩层能处理消失
    /// </summary>
    public GameUiCategory SupportedGameUiCategory => GameUiCategory.Unknown;

    private readonly double[] _cds = new double[4];
    private readonly bool[] _prevKeys = new bool[4];
    private bool _prevEKey = false;
    private DateTime _lastEKeyPress = DateTime.MinValue;
    private readonly DateTime[] _lastSetTime = new DateTime[4];
    private string[] _teamAvatarNames = new string[4];
    private Rect[] _teamIndexRects = new Rect[4];

    private DateTime _lastTickTime = DateTime.Now;
    private DateTime _contextEnterTime = DateTime.MinValue;
    /// <summary>
    /// 离开场景时间，用于0.8秒防抖避免识别失误导致UI闪烁（仅影响UI渲染，不影响CD计时）
    /// </summary>
    private DateTime _contextLeaveTime = DateTime.MinValue;
    private bool _wasInContext = false;
    
    /// <summary>
    /// 上一次激活的角色索引（1-4），用于检测当前激活角色切换
    /// </summary>
    private int _lastActiveIndex = -1;
    /// <summary>
    /// 上一次的队伍配置
    /// </summary>
    private string[] _lastTeamAvatarNames = new string[4];

    private int _lastSwitchFromSlot = -1;
    private DateTime _lastSwitchTime = DateTime.MinValue;
    
    /// <summary>
    /// 手柄输入监听器
    /// </summary>
    private GamepadInputMonitor? _gamepadMonitor;
    /// <summary>
    /// 上一次十字键状态（用于检测按键边沿）
    /// </summary>
    private bool _prevDPadUp = false;
    private bool _prevDPadDown = false;
    private bool _prevDPadLeft = false;
    private bool _prevDPadRight = false;
    
    /// <summary>
    /// 上一次检测到角色切换的时间，用于防抖
    /// </summary>
    private DateTime _lastDetectedSwitchTime = DateTime.MinValue;
    /// <summary>
    /// 手柄模式下当前激活的角色索引（1-4），初始为-1表示未知
    /// </summary>
    private int _gamepadCurrentActiveIndex = -1;
    
    /// <summary>
    /// 上一次检查手柄状态的时间，用于降低检查频率
    /// </summary>
    private DateTime _lastGamepadCheckTime = DateTime.MinValue;
    private DateTime _lastPressIndexTime = DateTime.MinValue; // 换人按键时间


    private volatile bool _isSyncingTeam = false;

    private DateTime _lastSyncTime = DateTime.MinValue;

    private ImageRegion? _lastImage = null; // 上一帧
    private ImageRegion? _penultimateImage = null; // 上上帧（倒数第二帧）
    private readonly object _stateLock = new();
    private readonly ILogger _logger = TaskControl.Logger;
    private readonly AvatarActiveCheckContext _activeCheckContext = new();

    /// <summary>
    /// 初始化
    /// </summary>
    public void Init()
    {
        _logger.LogInformation("🚀 [SkillCD] 冷却提示功能初始化，状态: {Enabled}", IsEnabled ? "已启用" : "已禁用");
        
        // 清空帧缓存
        _lastImage?.Dispose();
        _lastImage = null;
        _penultimateImage?.Dispose();
        _penultimateImage = null;
        for (int i = 0; i < 4; i++)
        {
            _cds[i] = 0;
            _prevKeys[i] = false;
            _teamAvatarNames[i] = string.Empty;
            _teamIndexRects[i] = default;
            _lastSetTime[i] = DateTime.MinValue;
            _lastTeamAvatarNames[i] = string.Empty;
        }

        _prevEKey = false;
        _lastEKeyPress = DateTime.MinValue;
        _wasInContext = false;
        _contextEnterTime = DateTime.MinValue;
        _contextLeaveTime = DateTime.MinValue;
        _lastTickTime = DateTime.Now;
        _lastActiveIndex = -1;
        _lastSwitchFromSlot = -1;
        _lastSwitchTime = DateTime.MinValue;
        _lastPressIndexTime = DateTime.MinValue;
        _lastSyncTime = DateTime.MinValue;
        
        // 初始化手柄监听器
        _gamepadMonitor = new GamepadInputMonitor();
        _prevDPadUp = false;
        _prevDPadDown = false;
        _prevDPadLeft = false;
        _prevDPadRight = false;
        _gamepadCurrentActiveIndex = -1;

        if (!IsEnabled)
        {
            VisionContext.Instance().DrawContent.PutOrRemoveTextList("SkillCdText", null);
        }
    }

    /// <summary>
    /// 截图回调处理
    /// </summary>
    public void OnCapture(CaptureContent content)
    {
        if (!IsEnabled)
        {
            VisionContext.Instance().DrawContent.PutOrRemoveTextList("SkillCdText", null);
            return;
        }

        var now = DateTime.Now;

        var delta = (now - _lastTickTime).TotalSeconds;
        _lastTickTime = now;

        // CD计时器持续运行
        if (delta >= 0 && delta < 5)
        {
            lock (_stateLock)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_cds[i] > 0)
                    {
                        _cds[i] -= delta;
                        if (_cds[i] < 0) _cds[i] = 0;
                    }
                }
            }
        }

        // 场景检测：只在主界面或秘境中运行
        bool rawInContext = Bv.IsInMainUi(content.CaptureRectArea) || Bv.IsInDomain(content.CaptureRectArea);
        bool isInContext;
        
        if (rawInContext)
        {
            var multiGameStatus = PartyAvatarSideIndexHelper.DetectedMultiGameStatus(content.CaptureRectArea);
            if (multiGameStatus.IsInMultiGame)
            {
                // 检测到联机状态，自动关闭SkillCd
                IsEnabled = false;
                _logger.LogWarning("检测到联机状态，自动关闭冷却提示");
                return;
            }
            _contextLeaveTime = DateTime.MinValue;
            isInContext = true;
        }
        else
        {
            if (_wasInContext && _contextLeaveTime == DateTime.MinValue)
            {
                _contextLeaveTime = now;
            }

            // 离开后0.8秒内仍视为在场景中，防止识别失误
            isInContext = _contextLeaveTime != DateTime.MinValue &&
                          (now - _contextLeaveTime).TotalSeconds < 0.8;
        }

        // 离开场景时隐藏UI，但保留角色信息和CD数据
        if (!isInContext)
        {
            if (_wasInContext)
            {
                VisionContext.Instance().DrawContent.PutOrRemoveTextList("SkillCdText", null);
                _wasInContext = false;
                _contextEnterTime = DateTime.MinValue;
                _lastActiveIndex = -1;
            }

            _lastImage?.Dispose();
            _lastImage = null;
            _penultimateImage?.Dispose();
            _penultimateImage = null;
            return;
        }

        if (!_wasInContext)
        {
            _logger.LogInformation("🎯 [SkillCD-DEBUG] 检测到 !_wasInContext，准备触发队伍同步");
            // 进入场景时同步队伍信息并检测队伍变化
            _contextEnterTime = now;
            _lastSyncTime = DateTime.MinValue;
            _wasInContext = true;
            _isSyncingTeam = true;
            
            _logger.LogInformation("✅ [SkillCD] 进入战斗场景，开始同步队伍信息...");
            
            Task.Run(async () =>
            {
                // 确保画面加载完成，提高识别成功率
                await Task.Delay(500);
                
                // 手柄模式不需要等待换人冷却（因为没有按键监听）
                bool isGamepadMode = Core.Simulator.Simulation.CurrentInputMode == Core.Simulator.InputMode.XInput;
                if (!isGamepadMode)
                {
                    var delaySinceLastPressIndex = (DateTime.Now - _lastPressIndexTime).TotalSeconds;
                    if (delaySinceLastPressIndex < 1.1)
                    {
                        // 刚按过换人键，人物头像还在读秒，此时yolo识别可能会失败
                        await Task.Delay(TimeSpan.FromSeconds(1.1 - delaySinceLastPressIndex));
                    }
                }
                    
                CombatScenes? scenes = null;
                try 
                {
                    _logger.LogInformation("🔍 [SkillCD] 正在识别队伍配置...");
                    scenes = RunnerContext.Instance.TrySyncCombatScenesSilent();
                    if (scenes != null && scenes.CheckTeamInitialized())
                    {
                        var avatars = scenes.GetAvatars();
                        _logger.LogInformation("✅ [SkillCD] 识别到 {Count} 个角色", avatars.Count);
                        
                        if (avatars.Count >= 1)
                        {
                            var newTeamNames = avatars.Select(a => a.Name).ToArray();
                            _logger.LogInformation("📋 [SkillCD] 队伍成员: {Team}", string.Join(", ", newTeamNames));
                            
                            // 检测队伍配置是否变化
                            bool teamChanged = false;
                            for (int i = 0; i < 4; i++)
                            {
                                string newName = i < newTeamNames.Length ? newTeamNames[i] : string.Empty;
                                if (_lastTeamAvatarNames[i] != newName)
                                {
                                    teamChanged = true;
                                    break;
                                }
                            }
                            
                            lock (_stateLock)
                            {
                                // 只更新角色名称，不重置CD值
                                // CD值由角色切换和OCR识别来管理
                                if (teamChanged)
                                {
                                    _logger.LogInformation("[SkillCD] 队伍配置变化: {OldTeam} -> {NewTeam}",
                                        string.Join(",", _lastTeamAvatarNames),
                                        string.Join(",", newTeamNames));
                                }
                                
                                // 在锁内同步角色信息
                                for (int i = 0; i < 4; i++)
                                {
                                    if (i < avatars.Count)
                                    {
                                        _teamAvatarNames[i] = avatars[i].Name;
                                        _teamIndexRects[i] = avatars[i].IndexRect;
                                    }
                                    else
                                    {
                                        _teamAvatarNames[i] = string.Empty;
                                        _teamIndexRects[i] = default;
                                    }
                                }
                                
                                for (int i = 0; i < 4; i++)
                                {
                                    _lastTeamAvatarNames[i] = i < newTeamNames.Length ? newTeamNames[i] : string.Empty;
                                }
                            }
                            
                            _logger.LogInformation("✅ [SkillCD] 队伍同步完成，冷却提示功能已激活");
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ [SkillCD] 未识别到任何角色");
                            lock (_stateLock)
                            {
                                // 同步失败/无人时清空UI，但保留数据
                                for (int i = 0; i < 4; i++)
                                {
                                    _teamAvatarNames[i] = string.Empty;
                                    _teamIndexRects[i] = default;
                                }
                            }
                        }
                    }
                    else
                    {
                        var avatarCount = scenes?.AvatarCount ?? 0;
                        var expectedCount = scenes?.ExpectedTeamAvatarNum ?? 0;
                        _logger.LogWarning("⚠️ [SkillCD] 队伍识别失败 (scenes={ScenesNull}, initialized={Init}, avatars={AvatarCount}, expected={Expected})", 
                            scenes == null, scenes?.CheckTeamInitialized() ?? false, avatarCount, expectedCount);
                        lock (_stateLock)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                _teamAvatarNames[i] = string.Empty;
                                _teamIndexRects[i] = default;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [SkillCD] 队伍同步异常");
                }
                finally
                {
                    scenes?.Dispose();
                    lock (_stateLock)
                    {
                        _isSyncingTeam = false;
                        _lastSyncTime = DateTime.Now;
                        _logger.LogInformation("✅ [SkillCD] 同步任务完成，_isSyncingTeam={Sync}, _lastSyncTime={Time}", _isSyncingTeam, _lastSyncTime);
                    }
                }
            });
        }

        // 场景切入缓冲期：避免刚进入场景时误触发
        if ((now - _contextEnterTime).TotalSeconds < 0.5)
        {
            return;
        }

        // 监听元素战技 (E) 键物理输入
        var elementalSkillKey = (int)TaskContext.Instance()
            .Config.KeyBindingsConfig.ElementalSkill.ToVK();

        short eKeyState = User32.GetAsyncKeyState(elementalSkillKey);
        bool isEDown = (eKeyState & 0x8000) != 0;
        if (isEDown && !_prevEKey) _lastEKeyPress = now;
        _prevEKey = isEDown;

        // 监听换人操作
        int pressedIndex = -1;
        bool isGamepadMode = Core.Simulator.Simulation.CurrentInputMode == Core.Simulator.InputMode.XInput;
        
        if (isGamepadMode && _gamepadMonitor != null)
        {
            // 性能优化：降低手柄状态检查频率，每100ms检查一次
            var timeSinceLastCheck = (now - _lastGamepadCheckTime).TotalMilliseconds;
            if (timeSinceLastCheck < 100)
            {
                // 跳过本次检查
            }
            else
            {
                _lastGamepadCheckTime = now;
                
                // 手柄模式：监听十字键上下左右
                // 角色编号对应：1=上, 2=右, 3=左, 4=下
                _gamepadMonitor.UpdateState();
                
                bool dpadUp = _gamepadMonitor.IsDPadUpPressed();
                bool dpadDown = _gamepadMonitor.IsDPadDownPressed();
                bool dpadLeft = _gamepadMonitor.IsDPadLeftPressed();
                bool dpadRight = _gamepadMonitor.IsDPadRightPressed();
            
            // 检测按键边沿（从未按下到按下）
            if ((dpadUp && !_prevDPadUp) || (dpadDown && !_prevDPadDown) || 
                (dpadLeft && !_prevDPadLeft) || (dpadRight && !_prevDPadRight))
            {
                // 防抖：避免短时间内重复识别
                var timeSinceLastDetection = (now - _lastDetectedSwitchTime).TotalSeconds;
                if (timeSinceLastDetection < 0.5)
                {
                    _prevDPadUp = dpadUp;
                    _prevDPadDown = dpadDown;
                    _prevDPadLeft = dpadLeft;
                    _prevDPadRight = dpadRight;
                    return;
                }
                
                // 确定目标角色索引：上=1, 右=2, 左=3, 下=4
                int targetIndex = dpadUp ? 1 : dpadRight ? 2 : dpadLeft ? 3 : 4;
                string direction = dpadUp ? "上(角色1)" : dpadRight ? "右(角色2)" : dpadLeft ? "左(角色3)" : "下(角色4)";
                
                // 首次检测：使用图像识别确定当前角色
                if (_gamepadCurrentActiveIndex <= 0)
                {
                    if (_lastImage != null)
                    {
                        _logger.LogInformation("🔍 [SkillCD-Gamepad] 首次检测开始，目标角色={Target}", targetIndex);
                        
                        int detectedIndex = IdentifyActiveIndex(_lastImage, new AvatarActiveCheckContext());
                        _logger.LogInformation("🔍 [SkillCD-Gamepad] 图像识别结果: detectedIndex={Detected}", detectedIndex);
                        
                        if (detectedIndex > 0)
                        {
                            _gamepadCurrentActiveIndex = detectedIndex;
                            _logger.LogInformation("✅ [SkillCD-Gamepad] 首次检测成功，图像识别当前角色={Current}，目标角色={Target}", 
                                detectedIndex, targetIndex);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ [SkillCD-Gamepad] 首次检测失败，图像识别返回{Result}，无法确定当前角色", detectedIndex);
                            // 图像识别失败，无法确定当前角色，跳过本次
                            _prevDPadUp = dpadUp;
                            _prevDPadDown = dpadDown;
                            _prevDPadLeft = dpadLeft;
                            _prevDPadRight = dpadRight;
                            return;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [SkillCD-Gamepad] 首次检测失败，_lastImage为null");
                        _prevDPadUp = dpadUp;
                        _prevDPadDown = dpadDown;
                        _prevDPadLeft = dpadLeft;
                        _prevDPadRight = dpadRight;
                        return;
                    }
                }
                
                // 如果当前激活角色已知，且不等于目标角色，说明要切换了
                // 此时OCR识别的是当前角色的CD
                if (_gamepadCurrentActiveIndex > 0 && _gamepadCurrentActiveIndex != targetIndex)
                {
                    if (_lastImage != null)
                    {
                        double ocrVal = RecognizeSkillCd(_lastImage);
                        
                        if (ocrVal > 0)
                        {
                            int slot = _gamepadCurrentActiveIndex - 1;
                            lock (_stateLock)
                            {
                                _cds[slot] = ocrVal;
                                _lastSetTime[slot] = DateTime.Now;
                            }
                            _logger.LogInformation("✅ [SkillCD-Gamepad] 角色{Current}→{Target}，记录角色{Current}的CD: {Cd:F1}s", 
                                _gamepadCurrentActiveIndex, targetIndex, _gamepadCurrentActiveIndex, ocrVal);
                            
                            // OCR成功，更新当前角色索引
                            _gamepadCurrentActiveIndex = targetIndex;
                        }
                        else
                        {
                            // OCR识别失败（可能是战技持续期间），不记录CD，也不更新当前角色索引
                            // 保持原有的_gamepadCurrentActiveIndex，等待下次成功识别
                            _logger.LogWarning("⚠️ [SkillCD-Gamepad] 角色{Current}→{Target}，OCR未识别到CD，保持当前角色索引不变", 
                                _gamepadCurrentActiveIndex, targetIndex);
                        }
                    }
                }
                else if (_gamepadCurrentActiveIndex == targetIndex)
                {
                    // 连续按相同按键，防抖跳过
                    _logger.LogDebug("🔄 [SkillCD-Gamepad] 连续按相同按键（角色{Target}），跳过", targetIndex);
                }
                _lastDetectedSwitchTime = now;
            }
            
                _prevDPadUp = dpadUp;
                _prevDPadDown = dpadDown;
                _prevDPadLeft = dpadLeft;
                _prevDPadRight = dpadRight;
            }
        }
        else if (!isGamepadMode)
        {
            // 键鼠模式：监听数字键 1-4
            for (int i = 0; i < 4; i++)
            {
                short keyState = User32.GetAsyncKeyState((int)(User32.VK.VK_1 + (byte)i));
                bool isDown = (keyState & 0x8000) != 0;
                if (isDown && !_prevKeys[i]) pressedIndex = i;
                _prevKeys[i] = isDown;
                _lastPressIndexTime = DateTime.Now;
            }
        }

        if (_lastImage != null)
        {
            // 键鼠模式：数字键切换角色
            if (!isGamepadMode && pressedIndex != -1)
            {
                ImageRegion frameToUse = _penultimateImage ?? _lastImage;
                if (frameToUse != null)
                {
                    HandleActionTrigger(frameToUse, pressedIndex);
                }
            }

            // 手柄模式：已改为在按键时直接OCR识别，不再需要后续的切换检测
            // 键鼠模式：E键触发时也使用图像识别
            if (!isGamepadMode && _prevEKey && TaskContext.Instance().Config.SkillCdConfig.TriggerOnSkillUse)
            {
                ImageRegion frameToUse = _penultimateImage ?? _lastImage;
                if (frameToUse != null)
                {
                    HandleActionTrigger(frameToUse, pressedIndex);
                }
            }
        }

        // 更新帧缓存队列
        _penultimateImage?.Dispose();
        _penultimateImage = _lastImage; // 把上一帧移到倒数第二帧
        
        // 记录当前帧为上一帧（深拷贝，避免current用完会被dispose）
        _lastImage = new ImageRegion(
            content.CaptureRectArea.SrcMat.Clone(),
            content.CaptureRectArea.X,
            content.CaptureRectArea.Y
        );

        UpdateOverlay();
    }

    /// <summary>
    /// 同步角色基础数据
    /// </summary>
    private void SyncAvatarInfo(List<Avatar> avatars)
    {
        for (int i = 0; i < 4; i++)
        {
            if (i < avatars.Count)
            {
                _teamAvatarNames[i] = avatars[i].Name;
                _teamIndexRects[i] = avatars[i].IndexRect;
            }
            else
            {
                _teamAvatarNames[i] = string.Empty;
                _teamIndexRects[i] = default;
            }
        }
    }

    private void HandleActionTrigger(ImageRegion frame, int pressedTarget)
    {
        int activeIdx = IdentifyActiveIndex(frame, new AvatarActiveCheckContext());
        if (activeIdx <= 0) return;

        int slot = activeIdx - 1;
        
        if (slot != pressedTarget)
        {
            double ocrVal = RecognizeSkillCd(frame);
            
            lock (_stateLock)
            {
                if (ocrVal > 0)
                {
                    _cds[slot] = ocrVal;
                    _lastSetTime[slot] = DateTime.Now;
                    
                    _lastSwitchFromSlot = slot;
                    _lastSwitchTime = DateTime.Now;
                }
                else
                {
                    bool justUsedE = (DateTime.Now - _lastEKeyPress).TotalSeconds < 1.1;
                    bool isVisualReady = Bv.IsSkillReady(frame, activeIdx, false);

                    if (isVisualReady)
                    {
                        if (justUsedE)
                        {
                            ApplyFallbackCd(slot);
                        }
                        else if (_cds[slot] > 0)
                        {
                        }
                        else
                        {
                            _cds[slot] = 0;
                        }
                    }
                    else
                    {
                        if (justUsedE)
                        {
                            ApplyFallbackCd(slot);
                        }
                    }
                }
            }
        }
        
        _lastActiveIndex = pressedTarget + 1;
    }

    private void HandleGamepadSwitch(ImageRegion frame, int fromIndex, int toIndex)
    {
        int fromSlot = fromIndex - 1;
        
        double ocrVal = RecognizeSkillCd(frame);
        
        lock (_stateLock)
        {
            if (ocrVal > 0)
            {
                _cds[fromSlot] = ocrVal;
                _lastSetTime[fromSlot] = DateTime.Now;
                _logger.LogInformation("[SkillCD-Gamepad] 角色切换 {From} -> {To}, 记录旧角色CD: {Cd:F1}s", fromIndex, toIndex, ocrVal);
            }
            else
            {
                // OCR返回0可能是：1.角色没用过E技能 2.OCR识别失败
                if (_cds[fromSlot] > 0)
                {
                    _logger.LogDebug("[SkillCD-Gamepad] 角色切换 {From} -> {To}, OCR未识别到CD，保留现有CD: {Cd:F1}s", fromIndex, toIndex, _cds[fromSlot]);
                }
                else
                {
                    _logger.LogDebug("[SkillCD-Gamepad] 角色切换 {From} -> {To}, OCR未识别到CD，角色可能未使用过E技能", fromIndex, toIndex);
                }
            }
        }
        
        _lastSwitchFromSlot = fromSlot;
        _lastSwitchTime = DateTime.Now;
    }

    /// <summary>
    /// 检测当前激活角色并同步技能状态
    /// </summary>
    private void CheckAndSyncActiveStatus(ImageRegion frame)
    {
        int activeIdx = IdentifyActiveIndex(frame, _activeCheckContext);
        if (activeIdx > 0)
        {
            // int slot = activeIdx - 1;
            //
            // // 更新当前激活角色索引（切换角色不清零CD）
            // if (_lastActiveIndex != activeIdx)
            // {
            //     _lastActiveIndex = activeIdx;
            // }
            //
            // // 检测技能是否就绪，就绪则归零
            // // 额外保护：处于切人冷却期时不检测
            // bool isInSwitchProtect = (slot == _lastSwitchFromSlot) && (DateTime.Now - _lastSwitchTime).TotalSeconds < 1.0;
            //
            // if (activeIdx == slot + 1 && !isInSwitchProtect)
            // {
            //     bool isReady = Bv.IsSkillReady(frame, activeIdx, false);
            //     if (isReady)
            //     {
            //         // 默认逻辑：识别到技能就绪时，不清零当前计时
            //         // 防止因开大招全屏遮挡导致误判为Ready从而错误清零计数器
            //         // 让倒计时自然跑完
            //     }
            // }
            _lastActiveIndex = activeIdx;
        }
    }

    /// <summary>
    /// 获取自定义规则中的CD值
    /// 返回值：
    /// - double值：命中规则，应强制设定为该值
    /// - null：未命中规则，走默认逻辑
    /// </summary>
    private double? GetCustomCdRule(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var config = ParseCustomCdConfig();
        if (config.TryGetValue(name, out var val))
        {
            // 如果用户只写了名字没写数值，尝试读默认配置
            if (!val.HasValue)
            {
                if (DefaultAutoFightConfig.CombatAvatarMap.TryGetValue(name, out var info))
                {
                    return info.SkillCd;
                }
                return 0; // 名字匹配但无默认配置，视为0
            }
            return val.Value;
        }
        return null;
    }

    /// <summary>
    /// 应用角色的冷却时间
    /// </summary>
    private void ApplyFallbackCd(int slot)
    {
        var name = _teamAvatarNames[slot];
        
        // 1. 优先自定义规则
        double? customRule = GetCustomCdRule(name);
        if (customRule.HasValue)
        {
            _cds[slot] = customRule.Value;
            _lastSetTime[slot] = DateTime.Now;
            return;
        }

        // 2. 默认兜底
        if (!string.IsNullOrEmpty(name) && DefaultAutoFightConfig.CombatAvatarMap.TryGetValue(name, out var info))
        {
            _cds[slot] = info.SkillCd;
            _lastSetTime[slot] = DateTime.Now;
        }
        else
        {
            _cds[slot] = 0;
        }
    }

    private Dictionary<string, double?> ParseCustomCdConfig()
    {
        var result = new Dictionary<string, double?>();
        var list = TaskContext.Instance().Config.SkillCdConfig.CustomCdList;
        
        if (list == null) return result;

        foreach (var item in list)
        {
            if (!string.IsNullOrWhiteSpace(item.RoleName))
            {
                if (!result.ContainsKey(item.RoleName))
                {
                    result[item.RoleName] = item.CdValue;
                }
            }
        }
        return result;
    }
    private int IdentifyActiveIndex(ImageRegion region, AvatarActiveCheckContext context)
    {
        bool isGamepadMode = Core.Simulator.Simulation.CurrentInputMode == Core.Simulator.InputMode.XInput;
        
        if (isGamepadMode)
        {
            // 手柄模式：只使用箭头检测，使用专用的识别区域
            var rectArray = AutoFightAssets.Instance.AvatarIndexRectListGamepad.ToArray();
            var arrowRo = AutoFightAssets.Instance.CurrentAvatarThresholdGamepadForSkillCd;
            
            var curr = region.Find(arrowRo);
            if (curr.IsEmpty())
            {
                return -1;
            }

            for (int i = 0; i < rectArray.Length; i++)
            {
                bool intersects = IsIntersecting(curr.Y, curr.Height, rectArray[i].Y, rectArray[i].Height);
                if (intersects)
                {
                    return i + 1;
                }
            }

            return -1;
        }
        else
        {
            // 键鼠模式：使用完整的检测逻辑（颜色+箭头+图像差异）
            var rectArray = AutoFightAssets.Instance.AvatarIndexRectList.ToArray();
            int result = PartyAvatarSideIndexHelper.GetAvatarIndexIsActiveWithContext(region, rectArray, context);
            return result;
        }
    }
    
    private static bool IsIntersecting(int y1, int height1, int y2, int height2)
    {
        int bottom1 = y1 + height1;
        int bottom2 = y2 + height2;
        return !(bottom1 < y2 || bottom2 < y1);
    }

    private double RecognizeSkillCd(ImageRegion image)
    {
        try
        {
            var eCdRect = Core.Simulator.Simulation.CurrentInputMode == Core.Simulator.InputMode.XInput
                ? AutoFightAssets.Instance.ECooldownRectGamepad
                : AutoFightAssets.Instance.ECooldownRect;
            
            using var crop = image.DeriveCrop(eCdRect);
            var roi = crop.SrcMat;
            
            // 方法1：白色文字过滤（降低阈值，提取更多接近白色的像素）
            using var whiteMask = new Mat();
            Cv2.InRange(roi, new Scalar(180, 180, 180), new Scalar(255, 255, 255), whiteMask);
            
            var text = OcrFactory.Paddle.OcrWithoutDetector(whiteMask);
            _logger.LogInformation("[SkillCD] OCR识别文本: \"{Text}\"", text ?? "(null)");
            
            if (!string.IsNullOrWhiteSpace(text))
            {
                var match = Regex.Match(text, @"\d+(\.\d+)?");
                if (match.Success && double.TryParse(match.Value, out var val))
                {
                    int intervalMs = TaskContext.Instance().Config.TriggerInterval;
                    double compensation = (intervalMs * 2) / 1000.0;
                    val -= compensation;

                    _logger.LogInformation("[SkillCD] OCR识别结果: {Val:F1}", val);
                    return (val > 0 && val < 60) ? val : 0;
                }
            }
            
            // 方法2：白色过滤失败，尝试二值化处理（只保留最亮的像素）
            _logger.LogDebug("[SkillCD] 白色过滤OCR失败，尝试二值化处理");
            using var grayRoi = new Mat();
            Cv2.CvtColor(roi, grayRoi, ColorConversionCodes.BGR2GRAY);
            
            // 使用OTSU自动阈值二值化，或者使用固定阈值200
            using var binaryRoi = new Mat();
            Cv2.Threshold(grayRoi, binaryRoi, 200, 255, ThresholdTypes.Binary);
            
            var text2 = OcrFactory.Paddle.OcrWithoutDetector(binaryRoi);
            _logger.LogInformation("[SkillCD] 二值化OCR识别文本: \"{Text}\"", text2 ?? "(null)");
            
            if (!string.IsNullOrWhiteSpace(text2))
            {
                var match = Regex.Match(text2, @"\d+(\.\d+)?");
                if (match.Success && double.TryParse(match.Value, out var val))
                {
                    int intervalMs = TaskContext.Instance().Config.TriggerInterval;
                    double compensation = (intervalMs * 2) / 1000.0;
                    val -= compensation;

                    _logger.LogInformation("[SkillCD] 二值化OCR识别结果: {Val:F1}", val);
                    return (val > 0 && val < 60) ? val : 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SkillCD] OCR识别异常");
        }
        return 0;
    }

    /// <summary>
    /// 更新 UI 层渲染
    /// </summary>
    private void UpdateOverlay()
    {
        var drawContent = VisionContext.Instance().DrawContent;
        var config = TaskContext.Instance().Config.SkillCdConfig;
        
        if (_isSyncingTeam)
        {
            _logger.LogDebug("[SkillCD] UpdateOverlay: 正在同步队伍，跳过");
            return;
        }

        var systemInfo = TaskContext.Instance().SystemInfo;
        double factor = (double)systemInfo.GameScreenSize.Width / systemInfo.ScaleMax1080PCaptureRect.Width;
        
        bool isGamepadMode = Core.Simulator.Simulation.CurrentInputMode == Core.Simulator.InputMode.XInput;
        
        double userPX = Math.Round(config.PX, 1);
        double userPY = Math.Round(config.PY, 1);
        double userGap = Math.Round(config.Gap, 1);
        
        // 手柄模式：自动调整遮罩位置
        if (isGamepadMode)
        {
            // 手柄模式下角色位置下移约70px，间距缩小为75px
            // 向左移动30px避免遮挡大招图标
            userPX -= 30.0;
            userPY += 70.0;
            userGap = 75.0;
        }

        double basePx = userPX * factor;
        double basePy = userPY * factor;
        double intervalY = userGap * factor;

        var textList = new List<TextDrawable>();
        string[] avatarNames;
        double[] cds;
        
        lock (_stateLock)
        {
            avatarNames = (string[])_teamAvatarNames.Clone();
            cds = (double[])_cds.Clone();
        }
        
        for (int slot = 0; slot < 4; slot++)
        {
            if (!string.IsNullOrEmpty(avatarNames[slot]))
            {
                if (config.HideWhenZero && cds[slot] <= 0)
                {
                    continue;
                }

                var px = basePx;
                var py = basePy + intervalY * slot;

                textList.Add(new TextDrawable(cds[slot].ToString("F1"), new Point(px, py)));
            }
            else
            {
                if (cds[slot] > 0)
                {
                    _logger.LogWarning("[SkillCD] 角色{Slot}名称为空但CD={Cd:F1}s > 0，无法显示遮罩", 
                        slot + 1, cds[slot]);
                }
            }
        }
        
        if (textList.Count == 0)
        {
            drawContent.PutOrRemoveTextList("SkillCdText", null);
        }
        else
        {
            drawContent.PutOrRemoveTextList("SkillCdText", textList);
        }
    }
}
