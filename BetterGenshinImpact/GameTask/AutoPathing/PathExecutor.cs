using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Simulator;
using BetterGenshinImpact.GameTask.AutoFight.Assets;
using BetterGenshinImpact.GameTask.AutoFight.Model;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation.Exception;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation.Model;
using BetterGenshinImpact.GameTask.AutoPathing.Handler;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using BetterGenshinImpact.GameTask.AutoPathing.Model.Enum;
using BetterGenshinImpact.GameTask.AutoSkip;
using BetterGenshinImpact.GameTask.AutoSkip.Assets;
using BetterGenshinImpact.GameTask.AutoTrackPath;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using BetterGenshinImpact.GameTask.Common.Job;
using BetterGenshinImpact.GameTask.Common.Map;
using BetterGenshinImpact.GameTask.Model.Area;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Recognition.OCR;
using BetterGenshinImpact.GameTask.AutoPathing.Suspend;
using BetterGenshinImpact.GameTask.Common;
using Vanara.PInvoke;
using static BetterGenshinImpact.GameTask.Common.TaskControl;
using static BetterGenshinImpact.GameTask.SystemControl;
using ActionEnum = BetterGenshinImpact.GameTask.AutoPathing.Model.Enum.ActionEnum;
using BetterGenshinImpact.Core.Simulator.Extensions;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.GameTask.AutoPathing;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Common.Exceptions;
using BetterGenshinImpact.GameTask.Common.Map.Maps;
using BetterGenshinImpact.GameTask.AutoFight;

namespace BetterGenshinImpact.GameTask.AutoPathing;

public class PathExecutor
{
    private readonly CameraRotateTask _rotateTask;
    private readonly TrapEscaper _trapEscaper;
    private readonly BlessingOfTheWelkinMoonTask _blessingOfTheWelkinMoonTask = new();
    private AutoSkipTrigger? _autoSkipTrigger;
    public int SuccessFight = 0;
    //·��׷����ȫ��������·�������ı�ʶ
    public bool SuccessEnd = false;
    private PathingPartyConfig? _partyConfig;
    private CancellationToken ct;
    private PathExecutorSuspend pathExecutorSuspend;

    public PathExecutor(CancellationToken ct)
    {
        _trapEscaper = new(ct);
        _rotateTask = new(ct);
        this.ct = ct;
        pathExecutorSuspend = new PathExecutorSuspend(this);
    }

    public PathingPartyConfig PartyConfig
    {
        get => _partyConfig ?? PathingPartyConfig.BuildDefault();
        set => _partyConfig = value;
    }

    /// <summary>
    /// �ж��Ƿ���ֹ��ͼ׷�ٵ�����
    /// </summary>
    public Func<ImageRegion, bool>? EndAction { get; set; }

    private CombatScenes? _combatScenes;
    // private readonly Dictionary<string, string> _actionAvatarIndexMap = new();

    private DateTime _elementalSkillLastUseTime = DateTime.MinValue;
    private DateTime _useGadgetLastUseTime = DateTime.MinValue;

    private const int RetryTimes = 2;
    private int _inTrap = 0;


    //��¼��ǰ��ص�λ����
    public (int, List<WaypointForTrack>) CurWaypoints { get; set; }

    //��¼��ǰ��λ
    public (int, WaypointForTrack) CurWaypoint { get; set; }

    //��¼�ָ���λ����
    private (int, List<WaypointForTrack>) RecordWaypoints { get; set; }

    //��¼�ָ���λ
    private (int, WaypointForTrack) RecordWaypoint { get; set; }

    //��������·������Ĳ���
    private bool _skipOtherOperations = false;

    // ���һ�λ�ȡ��ǲ������ʱ��
    private DateTime _lastGetExpeditionRewardsTime = DateTime.MinValue;


    //������ָ���λ
    public void TryCloseSkipOtherOperations()
    {
        // Logger.LogWarning("�ж��Ƿ�������ͼ׷��:" + (CurWaypoint.Item1 < RecordWaypoint.Item1));
        if (RecordWaypoints == CurWaypoints && CurWaypoint.Item1 < RecordWaypoint.Item1)
        {
            return;
        }

        if (_skipOtherOperations)
        {
            Logger.LogWarning("�ѵ����ϴε�λ����ͼ׷�ٹ��ָܻ�");
        }

        _skipOtherOperations = false;
    }

    //��¼��λ���������ָ�
    public void StartSkipOtherOperations()
    {
        Logger.LogWarning("��¼�ָ���λ����ͼ׷�ٽ������ϴε�λ֮ǰ��������·֮��Ĳ���");
        _skipOtherOperations = true;
        RecordWaypoints = CurWaypoints;
        RecordWaypoint = CurWaypoint;
    }

    public async Task Pathing(PathingTask task)
    {
        // SuspendableDictionary;
        const string sdKey = "PathExecutor";
        var sd = RunnerContext.Instance.SuspendableDictionary;
        sd.Remove(sdKey);

        RunnerContext.Instance.SuspendableDictionary.TryAdd(sdKey, pathExecutorSuspend);

        if (!task.Positions.Any())
        {
            Logger.LogWarning("û��·���㣬Ѱ·����");
            return;
        }


        // �л�����
        if (!await SwitchPartyBefore(task))
        {
            return;
        }

        // 临时禁用队伍验证，避免角色识别失败导致任务中断
        // if (!await ValidateGameWithTask(task))
        // {
        //     return;
        // }
        Logger.LogWarning("已禁用队伍验证，跳过角色识别检查");

        InitializePathing(task);
        // ת���������͵�ָ�·��
        var waypointsList = ConvertWaypointsForTrack(task.Positions, task);

        await Delay(100, ct);
        Navigation.WarmUp(task.Info.MapMatchMethod); // ��ǰ���ص�ͼ������

        foreach (var waypoints in waypointsList) // �����͵�ָ��·��
        {
            CurWaypoints = (waypointsList.FindIndex(wps => wps == waypoints), waypoints);
            for (var i = 0; i < RetryTimes; i++)
            {
                try
                {
                    await ResolveAnomalies(); // �쳣��������

                    // ����׸����Ƿ�TP��λ��ǿ�������������λ�����������ֲ�ƥ��
                    if (waypoints[0].Type != WaypointType.Teleport.Code)
                    {
                        Navigation.SetPrevPosition((float)waypoints[0].X, (float)waypoints[0].Y);
                    }

                    foreach (var waypoint in waypoints) // һ��·��
                    {
                        CurWaypoint = (waypoints.FindIndex(wps => wps == waypoint), waypoint);
                        TryCloseSkipOtherOperations();
                        await RecoverWhenLowHp(waypoint); // ��Ѫ���ָ�

                        if (waypoint.Type == WaypointType.Teleport.Code)
                        {
                            if (CurWaypoints.Item1 > 0)
                            {
                                await Delay(1000, ct);
                            }
                            await HandleTeleportWaypoint(waypoint);
                        }
                        else
                        {
                            await BeforeMoveToTarget(waypoint);
                            // Path�����ߵúܽ���Target��Ҫ�ӽ���������Ҫ���ƶ�����Ӧλ��
                            if (waypoint.Type == WaypointType.Orientation.Code)
                            {
                                // ��λ�㣬ֻ��Ҫ����
                                // ���ǵ���λ����������Ϊִ��action�����һ���㣬���Է��ڴ˴����������ʹ��͵�һ����������
                                await FaceTo(waypoint);
                            }
                            else if (waypoint.Action != ActionEnum.UpDownGrabLeaf.Code)
                            {
                                await MoveTo(waypoint);
                            }

                            await BeforeMoveCloseToTarget(waypoint);

                            if (IsTargetPoint(waypoint))
                            {
                                await MoveCloseTo(waypoint);
                            }

                            //skipOtherOperations������ԣ���������ز�����
                            if ((!string.IsNullOrEmpty(waypoint.Action) && !_skipOtherOperations) ||
                                waypoint.Action == ActionEnum.CombatScript.Code)
                            {
                                //ս��ǰ�Ľڵ��¼��������Ӿ���ص�ս���ڵ�
                                AutoFightTask.FightWaypoint = waypoint.Action == ActionEnum.Fight.Code ? waypoint : null;

                                // ִ�� action
                                await AfterMoveToTarget(waypoint);
                            }
                        }
                    }

                    if (waypoints == waypointsList.Last())
                    {
                        SuccessEnd = true;
                    }
                    break;
                }
                catch (HandledException handledException)
                {
                    SuccessEnd = true;
                    break;
                }
                catch (NormalEndException normalEndException)
                {
                    Logger.LogInformation(normalEndException.Message);
                    if (!RunnerContext.Instance.isAutoFetchDispatch && RunnerContext.Instance.IsContinuousRunGroup)
                    {
                        throw;
                    }
                    else
                    {
                        break;
                    }
                }
                catch (TaskCanceledException e)
                {
                    if (!RunnerContext.Instance.isAutoFetchDispatch && RunnerContext.Instance.IsContinuousRunGroup)
                    {
                        throw;
                    }
                    else
                    {
                        break;
                    }
                }
                catch (RetryException retryException)
                {
                    StartSkipOtherOperations();
                    Logger.LogWarning(retryException.Message);
                }
                catch (RetryNoCountException retryException)
                {
                    //��������£����Բ����Ĵ���
                    i--;
                    StartSkipOtherOperations();
                    Logger.LogWarning(retryException.Message);
                }
                finally
                {
                    // ����զ�����ɿ����а���
                    Simulation.ReleaseAllKey();
                }
            }

        }
    }

    private bool IsTargetPoint(WaypointForTrack waypoint)
    {
        // ��λ�㲻��Ҫ�ӽ�
        if (waypoint.Type == WaypointType.Orientation.Code || waypoint.Action == ActionEnum.UpDownGrabLeaf.Code)
        {
            return false;
        }


        var action = ActionEnum.GetEnumByCode(waypoint.Action);
        if (action is not null && action.UseWaypointTypeEnum != ActionUseWaypointTypeEnum.Custom)
        {
            // ǿ�Ƶ�λ���͵� action���� action Ϊ׼
            return action.UseWaypointTypeEnum == ActionUseWaypointTypeEnum.Target;
        }

        // ���������û��action������Ե�λ����Ϊ׼
        return waypoint.Type == WaypointType.Target.Code;
    }

    private async Task<bool> SwitchPartyBefore(PathingTask task)
    {
        var ra = CaptureToRectArea();

        // �л�����ǰ�ж��Ƿ�ȫ������ // ���ܶ����л�ʧ�ܵ��µ�����
        if (Bv.ClickIfInReviveModal(ra))
        {
            await Bv.WaitForMainUi(ct); // �ȴ�������������
            Logger.LogInformation("�������");
            await Delay(4000, ct);
            // Ѫ���϶�������ֱ��ȥ���������Ѫ
            await TpStatueOfTheSeven();
        }

        var pRaList = ra.FindMulti(AutoFightAssets.Instance.PRa); // �ж��Ƿ�����
        if (pRaList.Count > 0)
        {
            Logger.LogInformation("��������״̬�£����л�����");
        }
        else
        {
            if (PartyConfig is { Enabled: false })
            {
                // ������δ���õ�����£����ݵ�ͼ׷�����������л�����
                var partyName = FilterPartyNameByConditionConfig(task);
                if (!await SwitchParty(partyName))
                {
                    Logger.LogError("�л�����ʧ�ܣ��޷�ִ�д�·���������ͼ׷�����ã�");
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(PartyConfig.PartyName))
            {
                if (!await SwitchParty(PartyConfig.PartyName))
                {
                    Logger.LogError("�л�����ʧ�ܣ��޷�ִ�д�·���������������еĵ�ͼ׷�����ã�");
                    return false;
                }
            }
        }

        return true;
    }

    private void InitializePathing(PathingTask task)
    {
        LogScreenResolution();
        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<object>(this,
            "UpdateCurrentPathing", new object(), task));
    }

    private void LogScreenResolution()
    {
        var gameScreenSize = SystemControl.GetGameScreenRect(TaskContext.Instance().GameHandle);
        if (gameScreenSize.Width * 9 != gameScreenSize.Height * 16)
        {
            Logger.LogError("��Ϸ���ڷֱ��ʲ��� 16:9 ����ǰ�ֱ���Ϊ {Width}x{Height} , �� 16:9 �ֱ��ʵ���Ϸ�޷�����ʹ�õ�ͼ׷�ٹ��ܣ�",
                gameScreenSize.Width, gameScreenSize.Height);
            throw new Exception("��Ϸ���ڷֱ��ʲ��� 16:9 ���޷�ʹ�õ�ͼ׷�ٹ��ܣ�");
        }

        if (gameScreenSize.Width < 1920 || gameScreenSize.Height < 1080)
        {
            Logger.LogError("��Ϸ���ڷֱ���С�� 1920x1080 ����ǰ�ֱ���Ϊ {Width}x{Height} , С�� 1920x1080 �ķֱ��ʵ���Ϸ��ͼ׷�ٵ�Ч���ǳ��",
                gameScreenSize.Width, gameScreenSize.Height);
            throw new Exception("��Ϸ���ڷֱ���С�� 1920x1080 ���޷�ʹ�õ�ͼ׷�ٹ��ܣ�");
        }
    }

    /// <summary>
    /// �л�����
    /// </summary>
    /// <param name="partyName"></param>
    /// <returns></returns>
    private async Task<bool> SwitchParty(string? partyName)
    {
        bool success = true;
        if (!string.IsNullOrEmpty(partyName))
        {
            if (RunnerContext.Instance.PartyName == partyName)
            {
                return success;
            }

            bool forceTp = PartyConfig.IsVisitStatueBeforeSwitchParty;

            if (forceTp) // ǿ�ƴ���ģʽ
            {
                await new TpTask(ct).TpToStatueOfTheSeven(); // fix typos
                success = await new SwitchPartyTask().Start(partyName, ct);
            }
            else // ����ԭ���л�ģʽ
            {
                try
                {
                    success = await new SwitchPartyTask().Start(partyName, ct);
                }
                catch (PartySetupFailedException)
                {
                    await new TpTask(ct).TpToStatueOfTheSeven();
                    success = await new SwitchPartyTask().Start(partyName, ct);
                }
            }

            if (success)
            {
                RunnerContext.Instance.PartyName = partyName;
                RunnerContext.Instance.ClearCombatScenes();
            }
        }

        return success;
    }


    private static string? FilterPartyNameByConditionConfig(PathingTask task)
    {
        var pathingConditionConfig = TaskContext.Instance().Config.PathingConditionConfig;
        var materialName = task.GetMaterialName();
        var specialActions = task.Positions
            .Select(p => p.Action)
            .Where(action => !string.IsNullOrEmpty(action))
            .Distinct()
            .ToList();
        var partyName = pathingConditionConfig.FilterPartyName(materialName, specialActions);
        return partyName;
    }

    /// <summary>
    /// У��
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    private async Task<bool> ValidateGameWithTask(PathingTask task)
    {
        _combatScenes = await RunnerContext.Instance.GetCombatScenes(ct);
        if (_combatScenes == null)
        {
            return false;
        }

        // û��ǿ�����õ�����£�ʹ�õ�ͼ׷���ڵ���������
        // ������������ΪҪͨ������ʶ�����õ����ս��
        var pathingConditionConfig = TaskContext.Instance().Config.PathingConditionConfig;
        if (PartyConfig is { Enabled: false })
        {
            PartyConfig = pathingConditionConfig.BuildPartyConfigByCondition(_combatScenes);
        }

        // У���ɫ�Ƿ����
        if (task.HasAction(ActionEnum.NahidaCollect.Code))
        {
            var avatar = _combatScenes.SelectAvatar("�����");
            if (avatar == null)
            {
                Logger.LogError("��·������������ռ�������������û������槽�ɫ���޷�ִ�д�·����");
                return false;
            }

            // _actionAvatarIndexMap.Add("nahida_collect", avatar.Index.ToString());
        }

        // ��������Ҫ�л��Ľ�ɫ��ż�¼����
        Dictionary<string, ElementalType> map = new()
        {
            { ActionEnum.HydroCollect.Code, ElementalType.Hydro },
            { ActionEnum.ElectroCollect.Code, ElementalType.Electro },
            { ActionEnum.AnemoCollect.Code, ElementalType.Anemo }
        };

        foreach (var (action, el) in map)
        {
            if (!ValidateElementalActionAvatarIndex(task, action, el, _combatScenes))
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateElementalActionAvatarIndex(PathingTask task, string action, ElementalType el,
        CombatScenes combatScenes)
    {
        if (task.HasAction(action))
        {
            foreach (var avatar in combatScenes.GetAvatars())
            {
                if (ElementalCollectAvatarConfigs.Get(avatar.Name, el) != null)
                {
                    return true;
                }
            }

            Logger.LogError("��·������ {El}Ԫ�زɼ� ������������û�ж�ӦԪ�ؽ�ɫ:{Names}���޷�ִ�д�·����", el.ToChinese(),
                string.Join(",", ElementalCollectAvatarConfigs.GetAvatarNameList(el)));
            return false;
        }
        else
        {
            return true;
        }
    }

    private List<List<WaypointForTrack>> ConvertWaypointsForTrack(List<Waypoint> positions, PathingTask task)
    {
        // �� X Y ת��Ϊ MatX MatY
        var allList = positions.Select(waypoint =>
        {
            WaypointForTrack wft = new WaypointForTrack(waypoint, task.Info.MapName, task.Info.MapMatchMethod);
            wft.Misidentification=waypoint.PointExtParams.Misidentification;
            wft.MonsterTag = waypoint.PointExtParams.MonsterTag;
            wft.EnableMonsterLootSplit = waypoint.PointExtParams.EnableMonsterLootSplit;
            return wft;
        }).ToList();

        // ����WaypointType.Teleport.Code�и�����
        var result = new List<List<WaypointForTrack>>();
        var tempList = new List<WaypointForTrack>();
        foreach (var waypoint in allList)
        {
            if (waypoint.Type == WaypointType.Teleport.Code)
            {
                if (tempList.Count > 0)
                {
                    result.Add(tempList);
                    tempList = new List<WaypointForTrack>();
                }
            }

            tempList.Add(waypoint);
        }

        result.Add(tempList);

        return result;
    }

    /// <summary>
    /// ���Զ����Ѫ��������˻�Ѫ�����ڼ�¼���ʱ����λ��Ѫ����������λ������
    /// </summary>
    private async Task<bool> TryPartyHealing()
    {
        if (_combatScenes is null) return false;
        foreach (var avatar in _combatScenes.GetAvatars())
        {
            if (avatar.Name == "����")
            {
                if (avatar.TrySwitch())
                {
                    //1������������
                    Simulation.SimulateAction(GIActions.ElementalSkill);
                    await Delay(800, ct);
                    Simulation.SimulateAction(GIActions.ElementalSkill);
                    await Delay(800, ct);
                    await SwitchAvatar(PartyConfig.MainAvatarIndex);
                    await Delay(4000, ct);
                    return true;
                }

                break;
            }
            else if (avatar.Name == "ϣ����")
            {
                if (avatar.TrySwitch())
                {
                    Simulation.SimulateAction(GIActions.ElementalSkill);
                    await Delay(11000, ct);
                    await SwitchAvatar(PartyConfig.MainAvatarIndex);
                    return true;
                }

                break;
            }
            else if (avatar.Name == "ɺ�����ĺ�")
            {
                if (avatar.TrySwitch())
                {
                    Simulation.SimulateAction(GIActions.ElementalSkill);
                    await Delay(500, ct);
                    //����Qȫ�ӻ�Ѫ
                    Simulation.SimulateAction(GIActions.ElementalBurst);
                    //����Ѫֻ������λ��Ѫ
                    await SwitchAvatar(PartyConfig.MainAvatarIndex);
                    await Delay(5000, ct);
                    return true;
                }
            }
        }


        return false;
    }

    private async Task RecoverWhenLowHp(WaypointForTrack waypoint)
    {
        if (PartyConfig.OnlyInTeleportRecover && waypoint.Type != WaypointType.Teleport.Code)
        {
            return;
        }

        using var region = CaptureToRectArea();
        if (Bv.CurrentAvatarIsLowHp(region) && !(await TryPartyHealing() && Bv.CurrentAvatarIsLowHp(region)))
        {
            Logger.LogInformation("��ǰ��ɫѪ�����ͣ�ȥ��������ָ�");
            await TpStatueOfTheSeven();
            throw new RetryException("��Ѫ��ɺ�����·��");
        }
        else if (Bv.ClickIfInReviveModal(region))
        {
            await Bv.WaitForMainUi(ct); // �ȴ�������������
            Logger.LogInformation("�������");
            await Delay(4000, ct);
            // Ѫ���϶�������ֱ��ȥ���������Ѫ
            await TpStatueOfTheSeven();
            throw new RetryException("��Ѫ��ɺ�����·��");
        }
    }

    private async Task TpStatueOfTheSeven()
    {
        // tp �����������Ѫ
        var tpTask = new TpTask(ct);
        await RunnerContext.Instance.StopAutoPickRunTask(async () => await tpTask.TpToStatueOfTheSeven(), 5);
        Logger.LogInformation("Ѫ���ָ���ɡ������á�-�������������á������޸Ļ�Ѫ������á�");
    }

    /// <summary>
    /// �����Զ���ȡ��ǲ������
    /// </summary>
    /// <returns>�Ƿ������ȡ��ǲ����</returns>
    private async Task<bool> TryGetExpeditionRewardsDispatch(TpTask? tpTask = null)
    {
        if (tpTask == null)
        {
            tpTask = new TpTask(ct);
        }
        
        // ��С5���Ӽ��
        if ( _combatScenes?.CurrentMultiGameStatus?.IsInMultiGame == true || (DateTime.UtcNow - _lastGetExpeditionRewardsTime).TotalMinutes < 5)
        {
            return false;
        }

        //�򿪴��ͼ����
        await tpTask.OpenBigMapUi();
        bool changeBigMap = false;
        string adventurersGuildCountry =
            TaskContext.Instance().Config.OtherConfig.AutoFetchDispatchAdventurersGuildCountry;
        if (!RunnerContext.Instance.isAutoFetchDispatch && adventurersGuildCountry != "��")
        {
            var ra1 = CaptureToRectArea();
            var textRect = new Rect(60, 20, 160, 260);
            var textMat = new Mat(ra1.SrcMat, textRect);
            string text = OcrFactory.Paddle.Ocr(textMat);
            if (text.Contains("̽����ǲ����"))
            {
                changeBigMap = true;
                Logger.LogInformation("��ʼ�Զ���ȡ��ǲ����");
                try
                {
                    RunnerContext.Instance.isAutoFetchDispatch = true;
                    await RunnerContext.Instance.StopAutoPickRunTask(
                        async () => await new GoToAdventurersGuildTask().Start(adventurersGuildCountry, ct, null, true),
                        5);
                    Logger.LogInformation("�Զ���ȡ��ǲ�������ع�ԭ����");
                }
                catch (Exception e)
                {
                    Logger.LogInformation("δ֪ԭ�򣬷����쳣�����Լ���ִ������");
                }
                finally
                {
                    RunnerContext.Instance.isAutoFetchDispatch = false;
                    _lastGetExpeditionRewardsTime = DateTime.UtcNow; // ���۳ɹ���񶼸���ʱ��
                }
            }
        }

        return changeBigMap;
    }

    private async Task HandleTeleportWaypoint(WaypointForTrack waypoint)
    {
        var forceTp = waypoint.Action == ActionEnum.ForceTp.Code;
        TpTask tpTask = new TpTask(ct);
        await TryGetExpeditionRewardsDispatch(tpTask);
        var (tpX, tpY) = await tpTask.Tp(waypoint.GameX, waypoint.GameY, waypoint.MapName, forceTp);
        var (tprX, tprY) = MapManager.GetMap(waypoint.MapName, waypoint.MapMatchMethod)
            .ConvertGenshinMapCoordinatesToImageCoordinates(new Point2f((float)tpX, (float)tpY));
        Navigation.SetPrevPosition(tprX, tprY); // ͨ����һ��λ��ֱ�ӽ��оֲ�����ƥ��
        await Delay(500, ct); // ���һ��
    }

    public async Task FaceTo(WaypointForTrack waypoint)
    {
        var screen = CaptureToRectArea();
        var position = await GetPosition(screen, waypoint);
        var targetOrientation = Navigation.GetTargetOrientation(waypoint, position);
        Logger.LogDebug("面向路径点，位置({x2},{y2})", $"{waypoint.GameX:F1}", $"{waypoint.GameY:F1}");
        await WaitUntilRotatedTo(targetOrientation, 2);
        await Delay(500, ct);
    }

    public DateTime moveToStartTime;

    public async Task MoveTo(WaypointForTrack waypoint)
    {
        // ����
        await SwitchAvatar(PartyConfig.MainAvatarIndex);

        var screen = CaptureToRectArea();
        var (position, additionalTimeInMs) = await GetPositionAndTime(screen, waypoint);
        var targetOrientation = Navigation.GetTargetOrientation(waypoint, position);
        Logger.LogDebug("���Խӽ�;���㣬λ��({x2},{y2})", $"{waypoint.GameX:F1}", $"{waypoint.GameY:F1}");
        await WaitUntilRotatedTo(targetOrientation, 5);
        moveToStartTime = DateTime.UtcNow;
        var lastPositionRecord = DateTime.UtcNow;
        var fastMode = false;
        var prevPositions = new List<Point2f>();
        var fastModeColdTime = DateTime.MinValue;
        var prevNotTooFarPosition = position;
        int num = 0, distanceTooFarRetryCount = 0, consecutiveRotationCountBeyondAngle = 0;

        // 按下w键（或左摇杆）一直走
        Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyDown);
        while (!ct.IsCancellationRequested)
        {
            // 检查前进键是否按下（兼容键鼠和手柄模式）
            bool isMoveForwardPressed = false;
            if (Simulation.CurrentInputMode == InputMode.XInput)
            {
                // 手柄模式：检查左摇杆状态
                var xinput = InputRouter.Instance.GetOutput() as XInputOutput;
                isMoveForwardPressed = xinput?.IsMoveForwardPressed() ?? false;
            }
            else
            {
                // 键鼠模式：检查W键状态
                isMoveForwardPressed = Simulation.IsKeyDown(GIActions.MoveForward.ToActionKey().ToVK());
            }
            
            if (!isMoveForwardPressed)
            {
                Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyDown);
            }

            num++;
            if ((DateTime.UtcNow - moveToStartTime).TotalSeconds > 240)
            {
                Logger.LogWarning("ִ�г�ʱ�������˴�׷��");
                throw new RetryException("·����ִ�г�ʱ����������·��");
            }

            screen = CaptureToRectArea();

            EndJudgment(screen);

            // position = await GetPosition(screen, waypoint);
             (position, additionalTimeInMs) = await GetPositionAndTime(screen, waypoint);
             
             // 如果位置识别失败（返回0,0），跳过本次循环，等待下次重试
             if (position.X == 0 && position.Y == 0)
             {
                 Logger.LogDebug("⚠️ 位置识别失败，跳过本次循环");
                 await Delay(100, ct);
                 continue;
             }
             
             if (additionalTimeInMs>0)
             {
                 // 检查前进键是否按下（兼容键鼠和手柄模式）
                 if (Simulation.CurrentInputMode == InputMode.XInput)
                 {
                     var xinput = InputRouter.Instance.GetOutput() as XInputOutput;
                     isMoveForwardPressed = xinput?.IsMoveForwardPressed() ?? false;
                 }
                 else
                 {
                     isMoveForwardPressed = Simulation.IsKeyDown(GIActions.MoveForward.ToActionKey().ToVK());
                 }
                 
                 if (!isMoveForwardPressed)
                 {
                     Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyDown);
                 }

                 additionalTimeInMs = additionalTimeInMs + 1000;//�����𲽲���
             }
            var distance = Navigation.GetDistance(waypoint, position);
            Debug.WriteLine($"�ӽ�Ŀ����У�����Ϊ{distance}");
            Logger.LogDebug("🎮 当前距离目标: {Distance:F2}米, 位置: ({X:F1}, {Y:F1})", distance, position.X, position.Y);
            if (distance < 2)
            {
                Logger.LogInformation("✅ 到达路径点附近，停止移动");
                break;
            }

            if (distance > 500)
            {
                if (pathExecutorSuspend.CheckAndResetSuspendPoint())
                {
                    throw new RetryNoCountException("������ͣ����·����Զ������һ�δ�·�ߣ�");
                }
                else
                {
                    distanceTooFarRetryCount++;
                    if (distanceTooFarRetryCount > 50)
                    {
                        if (position == new Point2f())
                        {
                            throw new HandledException("���Զ�κ󣬵�ǰ��λ�޷���ʶ�𣬷�����·����");
                        }
                        else
                        {
                            Logger.LogWarning($"�����Զ��{position.X},{position.Y}��->��{waypoint.X},{waypoint.Y}��={distance}�����Զ�κ���Ȼʧ�ܣ�������·���㣡");
                            throw new HandledException("Ŀ������Զ�������ǵ�ǰ��λ�޷�ʶ�𣬷�����·����");
                        }
                    }
                    else
                    {
                        // ȡ�������־���Ƶ��
                        if (distanceTooFarRetryCount % 5 == 0)
                        {
                            Logger.LogWarning($"�����Զ��{position.X},{position.Y}��->��{waypoint.X},{waypoint.Y}��={distance}������");
                        }
                        // ȡ������ж�Ƶ��
                        if (distanceTooFarRetryCount % 10 == 0)
                        {
                            await ResolveAnomalies(screen);
                            Logger.LogInformation($"���õ��ϴ���ȷʶ������� ({prevNotTooFarPosition.X},{prevNotTooFarPosition.Y})");
                            Navigation.SetPrevPosition(prevNotTooFarPosition.X, prevNotTooFarPosition.Y);
                            // ���뵭����Ч
                            await Delay(500, ct);
                        }
                        await Delay(50, ct);
                        continue;
                    }
                }
            } else
            {
                prevNotTooFarPosition = position;
            }

            // ������״̬�£�����Ƿ�����������������
            if (waypoint.MoveMode != MoveModeEnum.Climb.Code)
            {
                if ((DateTime.UtcNow - lastPositionRecord).TotalMilliseconds > 1000 + additionalTimeInMs)
                {
                    lastPositionRecord = DateTime.UtcNow;
                    prevPositions.Add(position);
                    if (prevPositions.Count > 8)
                    {
                        var delta = prevPositions[^1] - prevPositions[^8];
                        if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 3)
                        {
                            _inTrap++;
                            if (_inTrap > 2)
                            {
                                throw new RetryException("在路径超过3次卡死，重试下一条路径或重新录制路径！");
                            }

                            Logger.LogWarning("怀疑卡死，尝试脱困...");

                            //调用脱困逻辑，由TrapEscaper负责移动
                            await _trapEscaper.RotateAndMove();
                            await _trapEscaper.MoveTo(waypoint);
                            Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyDown);
                            Logger.LogInformation("脱困完成，继续");
                            continue;
                        }
                    }
                }
            }

            // ��ת�ӽ�
            targetOrientation = Navigation.GetTargetOrientation(waypoint, position);
            //ִ����ת
            var diff = _rotateTask.RotateToApproach(targetOrientation, screen);
            if (num > 20)
            {
                if (Math.Abs(diff) > 5)
                {
                    consecutiveRotationCountBeyondAngle++;
                }
                else
                {
                    consecutiveRotationCountBeyondAngle = 0;
                }

                if (consecutiveRotationCountBeyondAngle > 10)
                {
                    // ֱ��վ����ת��
                    await WaitUntilRotatedTo(targetOrientation, 2);
                }
            }
            

            // ����ָ����ʽ�����ƶ�
            if (waypoint.MoveMode == MoveModeEnum.Fly.Code)
            {
                var isFlying = Bv.GetMotionStatus(screen) == MotionStatus.Fly;
                if (!isFlying)
                {
                    Debug.WriteLine("δ�������״̬�����¿ո�");
                    Simulation.SimulateAction(GIActions.Jump);
                    await Delay(200, ct);
                }

                await Delay(100, ct);
                continue;
            }

            if (waypoint.MoveMode == MoveModeEnum.Jump.Code)
            {
                Simulation.SimulateAction(GIActions.Jump);
                await Delay(200, ct);
                continue;
            }

            // ֻ������Ϊrun�Ż�һֱ����
            if (waypoint.MoveMode == MoveModeEnum.Run.Code)
            {
                if (distance > 20 != fastMode) // �������20ʱ����ʹ�ü���/����Ӿ
                {
                    if (fastMode)
                    {
                        Simulation.SimulateAction(GIActions.SprintMouse, KeyType.KeyUp);
                    }
                    else
                    {
                        Simulation.SimulateAction(GIActions.SprintMouse, KeyType.KeyDown);
                    }

                    fastMode = !fastMode;
                }
            }
            else if (waypoint.MoveMode == MoveModeEnum.Dash.Code)
            {
                if (distance > 20) // �������25ʱ����ʹ�ü���
                {
                    if (Math.Abs((fastModeColdTime - DateTime.UtcNow).TotalMilliseconds) > 1000) //��ȴһ��
                    {
                        fastModeColdTime = DateTime.UtcNow;
                        Simulation.SimulateAction(GIActions.SprintMouse);
                    }
                }
            }
            else if (waypoint.MoveMode != MoveModeEnum.Climb.Code) //�����Զ��̼���
            {
                // ʹ�� E ����
                if (distance > 10 && !string.IsNullOrEmpty(PartyConfig.GuardianAvatarIndex) &&
                    double.TryParse(PartyConfig.GuardianElementalSkillSecondInterval, out var s))
                {
                    if (s < 1)
                    {
                        Logger.LogWarning("Ԫ��ս����ȴʱ������̫�̣���ִ�У�");
                        return;
                    }

                    var ms = s * 1000;
                    if ((DateTime.UtcNow - _elementalSkillLastUseTime).TotalMilliseconds > ms)
                    {
                        // ���ܸ��й�������ȴʱ����
                        if (num <= 5 && (!string.IsNullOrEmpty(PartyConfig.MainAvatarIndex) &&
                                         PartyConfig.GuardianAvatarIndex != PartyConfig.MainAvatarIndex))
                        {
                            await Delay(800, ct); // �ܹ�1s
                        }

                        await UseElementalSkill();
                        _elementalSkillLastUseTime = DateTime.UtcNow;
                    }
                }

                // �Զ�����
                if (distance > 20 && PartyConfig.AutoRunEnabled)
                {
                    if (Math.Abs((fastModeColdTime - DateTime.UtcNow).TotalMilliseconds) > 2500) //��ȴʱ��2.5s���ظ�������
                    {
                        fastModeColdTime = DateTime.UtcNow;
                        Simulation.SimulateAction(GIActions.SprintMouse);
                    }
                }
            }

            // ʹ��С����
            if (PartyConfig.UseGadgetIntervalMs > 0)
            {
                if ((DateTime.UtcNow - _useGadgetLastUseTime).TotalMilliseconds > PartyConfig.UseGadgetIntervalMs)
                {
                    Simulation.SimulateAction(GIActions.QuickUseGadget);
                    _useGadgetLastUseTime = DateTime.UtcNow;
                }
            }

            await Delay(100, ct);
        }

        // 抬起w键（或释放左摇杆）
        Logger.LogInformation("🎮 释放 MoveForward (KeyUp)");
        Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyUp);
        Logger.LogInformation("✅ MoveTo 方法结束");
    }

    private async Task UseElementalSkill()
    {
        if (string.IsNullOrEmpty(PartyConfig.GuardianAvatarIndex))
        {
            return;
        }

        await Delay(200, ct);

        // ����
        Logger.LogInformation("�л��ܡ���Ѫ��ɫ��ʹ��Ԫ��ս��");
        var avatar = await SwitchAvatar(PartyConfig.GuardianAvatarIndex, true);
        if (avatar == null)
        {
            return;
        }

        // ���������������
        if (avatar.Name == "����")
        {
            Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyUp);
            await Delay(50, ct);
            Simulation.SimulateAction(GIActions.MoveBackward);
            await Delay(200, ct);
        }

        avatar.UseSkill(PartyConfig.GuardianElementalSkillLongPress);

        // ��������������� �������·
        if (avatar.Name == "����")
        {
            Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyDown);
        }
    }

    private async Task MoveCloseTo(WaypointForTrack waypoint)
    {
        ImageRegion screen;
        Point2f position;
        int targetOrientation;
        Logger.LogDebug("精确接近目标点，位置({x2},{y2})", $"{waypoint.GameX:F1}", $"{waypoint.GameY:F1}");

        var stepsTaken = 0;
        while (!ct.IsCancellationRequested)
        {
            stepsTaken++;
            if (stepsTaken > 25)
            {
                Logger.LogWarning("��ȷ�ӽ���ʱ");
                break;
            }

            screen = CaptureToRectArea();

            EndJudgment(screen);

            position = await GetPosition(screen, waypoint);
            if (Navigation.GetDistance(waypoint, position) < 2)
            {
                Logger.LogDebug("已到达路径点");
                break;
            }

            targetOrientation = Navigation.GetTargetOrientation(waypoint, position);
            await WaitUntilRotatedTo(targetOrientation, 2);
            // С�鲽�ӽ�
            Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyDown);
            Thread.Sleep(60);
            Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyUp);
            // Simulation.SendInput.Keyboard.KeyDown(User32.VK.VK_W).Sleep(60).KeyUp(User32.VK.VK_W);
            await Delay(20, ct);
        }

        Simulation.SimulateAction(GIActions.MoveForward, KeyType.KeyUp);

        // ����Ŀ�ĵغ�ͣ��һ��
        await Delay(1000, ct);
    }

    private async Task BeforeMoveCloseToTarget(WaypointForTrack waypoint)
    {
        if (waypoint.MoveMode == MoveModeEnum.Fly.Code && waypoint.Action == ActionEnum.StopFlying.Code)
        {
            await ActionFactory.GetBeforeHandler(ActionEnum.StopFlying.Code).RunAsync(ct, waypoint);
        }
    }

    private async Task BeforeMoveToTarget(WaypointForTrack waypoint)
    {
        if (waypoint.Action == ActionEnum.UpDownGrabLeaf.Code)
        {
            Simulation.SimulateAction(GIActions.Jump);
            await Delay(300, ct);
            var screen = CaptureToRectArea();
            var position = await GetPosition(screen, waypoint);
            var targetOrientation = Navigation.GetTargetOrientation(waypoint, position);
            await WaitUntilRotatedTo(targetOrientation, 10);
            var handler = ActionFactory.GetBeforeHandler(waypoint.Action);
            await handler.RunAsync(ct, waypoint);
        }
        else if (waypoint.Action == ActionEnum.LogOutput.Code)
        {
            Logger.LogInformation(waypoint.LogInfo);
        }
    }

    private async Task AfterMoveToTarget(WaypointForTrack waypoint)
    {
        if (waypoint.Action == ActionEnum.NahidaCollect.Code
            || waypoint.Action == ActionEnum.PickAround.Code
            || waypoint.Action == ActionEnum.Fight.Code
            || waypoint.Action == ActionEnum.HydroCollect.Code
            || waypoint.Action == ActionEnum.ElectroCollect.Code
            || waypoint.Action == ActionEnum.AnemoCollect.Code
            || waypoint.Action == ActionEnum.PyroCollect.Code
            || waypoint.Action == ActionEnum.CombatScript.Code
            || waypoint.Action == ActionEnum.Mining.Code
            || waypoint.Action == ActionEnum.Fishing.Code
            || waypoint.Action == ActionEnum.ExitAndRelogin.Code
            || waypoint.Action == ActionEnum.EnterAndExitWonderland.Code
            || waypoint.Action == ActionEnum.SetTime.Code
            || waypoint.Action == ActionEnum.UseGadget.Code
            || waypoint.Action == ActionEnum.PickUpCollect.Code)
        {
            var handler = ActionFactory.GetAfterHandler(waypoint.Action);
            //,PartyConfig
            await handler.RunAsync(ct, waypoint, PartyConfig);
            //ͳ�ƽ���ս���Ĵ���
            if (waypoint.Action == ActionEnum.Fight.Code)
            {
                SuccessFight++;
            }
            await Delay(1000, ct);
        }
    }

    private async Task<Avatar?> SwitchAvatar(string index, bool needSkill = false)
    {
        if (string.IsNullOrEmpty(index))
        {
            return null;
        }

        var avatar = _combatScenes?.SelectAvatar(int.Parse(index));
        if (avatar == null) return null;
        if (needSkill && !avatar.IsSkillReady())
        {
            Logger.LogInformation("��ɫ{Name}����δ��ȴ��������", avatar.Name);
            return null;
        }

        var success = avatar.TrySwitch(5);//���л�һ�Σ�����������˾���Ҫ����һ��ѭ��
        if (success)
        {
            await Delay(100, ct);
            return avatar;
        }

        Logger.LogInformation("�����л���ɫ{Name}ʧ�ܣ�", avatar.Name);
        return null;
    }
    
    /// <summary>
    /// ����ʱ����������֮���ֵ��
    /// </summary>
    /// <param name="startPoint">�������</param>
    /// <param name="endPoint">�յ�����</param>
    /// <param name="startTime">��ʼʱ��</param>
    /// <param name="midTime">�м�ʱ��</param>
    /// <param name="endTime">����ʱ��</param>
    /// <returns>�м������</returns>
    public static Point2f InterpolatePointByTime(
        Point2f startPoint,
        Point2f endPoint,
        DateTime startTime,
        DateTime midTime,
        DateTime endTime)
    {
        // ����ʱ���
        double totalMillis = (endTime - startTime).TotalMilliseconds;
        double midMillis = (midTime - startTime).TotalMilliseconds;

        // ��ֹ����0
        if (totalMillis == 0)
            return startPoint;

        // �������
        float t = (float)(midMillis / totalMillis);
        if (t>1.0f)
        {
            t = 1.0f;
        }
        // ��ֵ����
        float x = startPoint.X + (endPoint.X - startPoint.X) * t;
        float y = startPoint.Y + (endPoint.Y - startPoint.Y) * t;

        return new Point2f(x, y);
    }
    
    private  Point2f prePosition;
    private  DateTime preTime;
    //�Զ������λ�����ʱ��
    private int maxAutoPositionTime=10000; 
    private async Task WaitForCloseMap(int maxAttempts, int delayMs)
    {
        await Delay(delayMs, ct);
        for (var i = 0; i < maxAttempts; i++)
        {
            using var capture = CaptureToRectArea();
            if (Bv.IsInMainUi(capture))
            {
                return;
            }

            await Delay(delayMs, ct);
        }
        
    }

    private async Task<Point2f> GetPosition(ImageRegion imageRegion, WaypointForTrack waypoint)
    {
        return (await GetPositionAndTime(imageRegion, waypoint)).point;
    }
    //
    public bool GetPositionAndTimeSuspendFlag = false;
    private async Task<(Point2f point,int additionalTimeInMs)> GetPositionAndTime(ImageRegion imageRegion, WaypointForTrack waypoint)
    {
        
        var position = Navigation.GetPosition(imageRegion, waypoint.MapName, waypoint.MapMatchMethod);
        int time = 0;
        if (position == new Point2f())
        {
            if (!Bv.IsInMainUi(imageRegion))
            {
                Logger.LogDebug("С��ͼλ�ö�λʧ�ܣ��ҵ�ǰ���������棬�����쳣����");
                await ResolveAnomalies(imageRegion);
            }
        }

        var distance = Navigation.GetDistance(waypoint, position);
        //��;��ͣ������ͼδʶ��
        if (position is {X:0,Y:0} && GetPositionAndTimeSuspendFlag)
        {
            GetPositionAndTimeSuspendFlag = false;
            throw new RetryNoCountException("������ͣ����·����Զ������һ�δ�·�ߣ�");
        }
        //��ʱ����   pathTooFar  ·����Զ  unrecognized δʶ��
        if ((position is {X:0,Y:0} && waypoint.Misidentification.Type.Contains("unrecognized")) || (distance>500 && waypoint.Misidentification.Type.Contains("pathTooFar")))
        {
            if (waypoint.Misidentification.HandlingMode == "previousDetectedPoint")
            {
                if (prePosition != default)
                {
                    position = prePosition;
                    Logger.LogInformation(@$"δʶ�𵽾���·����ȡ�ϴε�λ");
                }
            }else if (waypoint.Misidentification.HandlingMode == "mapRecognition"){
                //���ͼʶ������
                DateTime start = DateTime.Now;
                TpTask tpTask = new TpTask(ct);
                await tpTask.OpenBigMapUi();
                try
                {
                    position =MapManager.GetMap(waypoint.MapName, waypoint.MapMatchMethod).ConvertGenshinMapCoordinatesToImageCoordinates(tpTask.GetPositionFromBigMap(waypoint.MapName));
                }
                catch (Exception e)
                {
                    Logger.LogInformation(@$"��ͼ���ĵ�ʶ��ʧ�ܣ�");
                }
               
                Simulation.SimulateAction(GIActions.OpenPaimonMenu);
                //Bv.IsInMainUi(imageRegion);
                await WaitForCloseMap(10,200);
                DateTime end = DateTime.Now;
                time=(int)(end - start).TotalMilliseconds;
                Logger.LogInformation(@$"δʶ�𵽾���·�����򿪵�ͼ�������ĵ�({position.X},{position.Y})");
            }
            
            /*if (prePosition!=default)
            {*/
                //position = InterpolatePointByTime(prePosition,new Point2f((float)waypoint.GameX,(float)waypoint.GameY),preTime,DateTime.Now,preTime.AddMilliseconds(maxAutoPositionTime));
                //Logger.LogInformation(@$"δʶ�𵽾���·����Ԥ����·��Ϊ��{position.X},{position.Y}��,��ʼ������λΪ����{prePosition.X},{prePosition.Y}����{waypoint.GameX},{waypoint.GameY}��");
                //Point2f GetBigMapCenterPoint(string mapName)

               // Logger.LogInformation(@$"δʶ�𵽾���·�����򿪵�ͼ�������ĵ�({position.X},{position.Y})");
                //position =prePosition;
           // }

        }
        else
        {
            prePosition = position;
            preTime = DateTime.Now;
        }

        //Logger.LogDebug("ʶ��·����"+position.X+","+position.Y);
        return (position,time);
    }

    private async Task WaitUntilRotatedTo(int targetOrientation, int maxDiff)
    {
        if (await _rotateTask.WaitUntilRotatedTo(targetOrientation, maxDiff))
        {
            return;
        }
        await ResolveAnomalies();
        await _rotateTask.WaitUntilRotatedTo(targetOrientation, maxDiff);
    }

    /**
     * ���������쳣����
     * ��Ҫ��֤��ʱ����̫��
     */
    private async Task ResolveAnomalies(ImageRegion? imageRegion = null)
    {
        if (imageRegion == null)
        {
            imageRegion = CaptureToRectArea();
        }

        // һЩ�쳣���洦��
        var cookRa = imageRegion.Find(AutoSkipAssets.Instance.CookRo);
        var closeRa = imageRegion.Find(AutoSkipAssets.Instance.PageCloseMainRo);
        var closeRa2 = imageRegion.Find(ElementAssets.Instance.PageCloseWhiteRo);
        var closeRa3 = imageRegion.Find(AutoSkipAssets.Instance.PageCloseRo);
        if (cookRa.IsExist() || closeRa.IsExist() || closeRa2.IsExist() || closeRa3.IsExist())
        {
            // �ų����ͼ
            if (Bv.IsInBigMapUi(imageRegion))
            {
                return;
            }

            Logger.LogInformation("检测到对话界面，使用ESC关闭界面");
            Simulation.SimulateAction(GIActions.OpenPaimonMenu);
            await Delay(1000, ct); // 等待界面关闭
        }


        // �����¿�
        await _blessingOfTheWelkinMoonTask.Start(ct);

        if (PartyConfig.AutoSkipEnabled)
        {
            // �ж��Ƿ�������
            await AutoSkip();
        }
    }

    private async Task AutoSkip()
    {
        var ra = CaptureToRectArea();
        var disabledUiButtonRa = ra.Find(AutoSkipAssets.Instance.DisabledUiButtonRo);
        if (disabledUiButtonRa.IsExist())
        {
            Logger.LogWarning("������飬�Զ��������ֱ������");

            if (_autoSkipTrigger == null)
            {
                _autoSkipTrigger = new AutoSkipTrigger(new AutoSkipConfig
                {
                    Enabled = true,
                    QuicklySkipConversationsEnabled = true, // ���ٵ��������
                    ClosePopupPagedEnabled = true,
                    ClickChatOption = "����ѡ�����һ��ѡ��",
                });
                _autoSkipTrigger.Init();
            }

            int noDisabledUiButtonTimes = 0;

            while (true)
            {
                ra = CaptureToRectArea();
                disabledUiButtonRa = ra.Find(AutoSkipAssets.Instance.DisabledUiButtonRo);
                if (disabledUiButtonRa.IsExist())
                {
                    _autoSkipTrigger.OnCapture(new CaptureContent(ra));
                    noDisabledUiButtonTimes = 0;
                }
                else
                {
                    noDisabledUiButtonTimes++;
                    if (noDisabledUiButtonTimes > 10)
                    {
                        Logger.LogInformation("�Զ��������");
                        break;
                    }
                }

                await Delay(210, ct);
            }
        }
    }

    private void EndJudgment(ImageRegion ra)
    {
        if (EndAction != null && EndAction(ra))
        {
            throw new HandledException("��ɽ���������������ͼ׷��");
        }
    }
}
