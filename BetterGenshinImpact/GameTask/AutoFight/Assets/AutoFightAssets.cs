using BetterGenshinImpact.Core.Recognition;
using BetterGenshinImpact.GameTask.Model;
using OpenCvSharp;
using System.Collections.Generic;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation.Model;


namespace BetterGenshinImpact.GameTask.AutoFight.Assets;

public class AutoFightAssets : BaseAssets<AutoFightAssets>
{
    public Rect TeamRectNoIndex;
    public Rect TeamRect;
    public List<Rect> AvatarSideIconRectList; // 侧边栏角色头像 非联机状态下
    public List<Rect> AvatarIndexRectList; // 侧边栏角色头像对应的白色块 非联机状态下
    public List<Rect> AvatarQRectListMap; // 角色头像对应的Q技能图标 

    public Rect ERect;
    public Rect ECooldownRect;
    
    // 手柄模式的E技能区域
    public Rect ERectGamepad;
    public Rect ECooldownRectGamepad;
    
    // 手柄模式的角色编号区域
    public List<Rect> AvatarIndexRectListGamepad;
    
    // 手柄模式的出战标识识别对象
    public RecognitionObject CurrentAvatarThresholdGamepad;
    
    // 手柄模式的出战标识识别对象（专用于SkillCd当前角色检测，区域更大）
    public RecognitionObject CurrentAvatarThresholdGamepadForSkillCd;
    
    // 手柄模式的角色编号识别对象
    public RecognitionObject Index1Gamepad;
    public RecognitionObject Index2Gamepad;
    public RecognitionObject Index3Gamepad;
    public RecognitionObject Index4Gamepad;
    public List<RecognitionObject> IndexListGamepad => [Index1Gamepad, Index2Gamepad, Index3Gamepad, Index4Gamepad];
    
    public Rect QRect;
    public Rect ZCooldownRect;
    public Rect EndTipsUpperRect; // 挑战达成提示
    public Rect EndTipsRect;
    public RecognitionObject WandererIconRa;
    public RecognitionObject WandererIconNoActiveRa;
    public RecognitionObject ConfirmRa;
    public RecognitionObject ArtifactAreaRa;
    public RecognitionObject ExitRa;
    public RecognitionObject ClickAnyCloseTipRa;

    // 自动秘境
    // public RecognitionObject LockIconRa; // 锁定辅助图标

    public Dictionary<string, string> AvatarCostumeMap;

    // 联机
    public RecognitionObject OnePRa;

    public RecognitionObject PRa;
    public Dictionary<string, List<Rect>> AvatarSideIconRectListMap; // 侧边栏角色头像 联机状态下
    public Dictionary<string, List<Rect>> AvatarIndexRectListMap; // 侧边栏角色头像对应的白色块 联机状态下

    // 小道具位置
    public Rect GadgetRect;

    public RecognitionObject AbnormalIconRa;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    private AutoFightAssets() : base()
    {
        Initialization(this.systemInfo);
    }

    protected AutoFightAssets(ISystemInfo systemInfo) : base(systemInfo)
    {
        Initialization(systemInfo);
    }
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

    private void Initialization(ISystemInfo systemInfo)
    {
        TeamRectNoIndex = new Rect(CaptureRect.Width - (int)(355 * AssetScale), (int)(220 * AssetScale),
            (int)((355 - 85) * AssetScale), (int)(465 * AssetScale));
        TeamRect = new Rect(CaptureRect.Width - (int)(355 * AssetScale), (int)(220 * AssetScale),
            (int)(355 * AssetScale), (int)(465 * AssetScale));
        ERect = new Rect(CaptureRect.Width - (int)(267 * AssetScale), CaptureRect.Height - (int)(132 * AssetScale),
            (int)(77 * AssetScale), (int)(77 * AssetScale));
        ECooldownRect = new Rect(CaptureRect.Width - (int)(241 * AssetScale), CaptureRect.Height - (int)(97 * AssetScale),
            (int)(41 * AssetScale), (int)(18 * AssetScale));
        
        // 手柄模式：RB按钮位置（基于1080p坐标）
        // E技能图标：RB按钮圆形区域
        ERectGamepad = new Rect(CaptureRect.Width - (int)(210 * AssetScale), CaptureRect.Height - (int)(360 * AssetScale),
            (int)(70 * AssetScale), (int)(70 * AssetScale));
        // CD数字：在RB按钮圆形中心，显示"12.0"这样的白色数字
        // 扩大识别区域以提高OCR成功率
        ECooldownRectGamepad = new Rect(CaptureRect.Width - (int)(200 * AssetScale), CaptureRect.Height - (int)(345 * AssetScale),
            (int)(50 * AssetScale), (int)(25 * AssetScale));
        
        // 手柄模式：角色编号区域（基于1080p坐标）
        // 这个区域用于队伍识别和当前角色检测
        // 手柄模式下角色编号位置下移
        AvatarIndexRectListGamepad =
        [
            new Rect(CaptureRect.Width - (int)(135 * AssetScale), (int)(315 * AssetScale), (int)(28 * AssetScale), (int)(34 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(135 * AssetScale), (int)(390 * AssetScale), (int)(28 * AssetScale), (int)(34 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(135 * AssetScale), (int)(465 * AssetScale), (int)(28 * AssetScale), (int)(34 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(135 * AssetScale), (int)(542 * AssetScale), (int)(28 * AssetScale), (int)(34 * AssetScale)),
        ];
        
        // 手柄模式：出战标识识别区域（用于队伍识别）
        // 键鼠模式: Rect(Width - 240, 155, 210, 600)
        // 手柄模式: Y轴下移，高度减小，只覆盖角色列表区域
        CurrentAvatarThresholdGamepad = new RecognitionObject
        {
            Name = "CurrentAvatarThresholdGamepad",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("Common\\Element", "current_avatar_threshold.png", systemInfo),
            RegionOfInterest = new Rect(CaptureRect.Width - (int)(240 * AssetScale), (int)(255 * AssetScale), (int)(210 * AssetScale), (int)(370 * AssetScale)),
            UseBinaryMatch = true,
            BinaryThreshold = 200,
        }.InitTemplate();
        
        // 手柄模式：出战标识识别区域（专用于SkillCd当前角色检测）
        // 区域向左扩展覆盖箭头，宽度缩小为1/3避免十字键图标干扰
        CurrentAvatarThresholdGamepadForSkillCd = new RecognitionObject
        {
            Name = "CurrentAvatarThresholdGamepadForSkillCd",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("Common\\Element", "current_avatar_threshold.png", systemInfo),
            RegionOfInterest = new Rect(CaptureRect.Width - (int)(270 * AssetScale), (int)(255 * AssetScale), (int)(73 * AssetScale), (int)(370 * AssetScale)),
            UseBinaryMatch = true,
            BinaryThreshold = 200,
        }.InitTemplate();
        
        // 手柄模式：角色编号识别区域
        // 键鼠模式: Rect(Width - 65, 155, 35, 600)
        // 手柄模式: Y轴下移，高度减小
        Rect partyRectGamepad = new Rect(CaptureRect.Width - (int)(65 * AssetScale), (int)(255 * AssetScale), (int)(35 * AssetScale), (int)(370 * AssetScale));
        Index1Gamepad = new RecognitionObject
        {
            Name = "Index1Gamepad",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage(@"Common\Element", "index_1_gamepad.png", systemInfo),
            RegionOfInterest = partyRectGamepad,
        }.InitTemplate();
        Index2Gamepad = new RecognitionObject
        {
            Name = "Index2Gamepad",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage(@"Common\Element", "index_2_gamepad.png", systemInfo),
            RegionOfInterest = partyRectGamepad,
        }.InitTemplate();
        Index3Gamepad = new RecognitionObject
        {
            Name = "Index3Gamepad",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage(@"Common\Element", "index_3_gamepad.png", systemInfo),
            RegionOfInterest = partyRectGamepad,
        }.InitTemplate();
        Index4Gamepad = new RecognitionObject
        {
            Name = "Index4Gamepad",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage(@"Common\Element", "index_4_gamepad.png", systemInfo),
            RegionOfInterest = partyRectGamepad,
        }.InitTemplate();
        
        QRect = new Rect(CaptureRect.Width - (int)(157 * AssetScale), CaptureRect.Height - (int)(165 * AssetScale),
            (int)(110 * AssetScale), (int)(110 * AssetScale));
        ZCooldownRect = new Rect(CaptureRect.Width - (int)(130 * AssetScale), (int)(814 * AssetScale),
            (int)(60 * AssetScale), (int)(24 * AssetScale));
        // 小道具位置 1920-133,800,60,50
        GadgetRect = new Rect(CaptureRect.Width - (int)(133 * AssetScale), (int)(800 * AssetScale),
            (int)(60 * AssetScale), (int)(50 * AssetScale));
        // 结束提示从中间开始找相对位置
        EndTipsUpperRect = new Rect(CaptureRect.Width / 2 - (int)(100 * AssetScale), (int)(243 * AssetScale),
            (int)(200 * AssetScale), (int)(50 * AssetScale));
        EndTipsRect = new Rect(CaptureRect.Width / 2 - (int)(200 * AssetScale), CaptureRect.Height - (int)(160 * AssetScale),
            (int)(400 * AssetScale), (int)(80 * AssetScale));

        AvatarIndexRectList =
        [
            new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(256 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(352 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(448 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(544 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
        ];

        AvatarQRectListMap =
        [
            new Rect(CaptureRect.Width - (int)(336 * AssetScale), (int)(216 * AssetScale), (int)(64 * AssetScale), (int)(84 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(336 * AssetScale), (int)(316 * AssetScale), (int)(64 * AssetScale), (int)(84 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(336 * AssetScale), (int)(416 * AssetScale), (int)(64 * AssetScale), (int)(84 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(336 * AssetScale), (int)(516 * AssetScale), (int)(64 * AssetScale), (int)(84 * AssetScale)),
        ];

        AvatarSideIconRectList =
        [
            new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(225 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(315 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(410 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
            new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(500 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
        ];

        AvatarCostumeMap = new Dictionary<string, string>
        {
            { "Flamme", "殷红终夜" },
            { "Bamboo", "雨化竹身" },
            { "Dai", "冷花幽露" },
            { "Yu", "玄玉瑶芳" },
            { "Dancer", "帆影游风" },
            { "Witch", "琪花星烛" },
            { "Wic", "和谐" },
            { "Studentin", "叶隐芳名" },
            { "Fruhling", "花时来信" },
            { "Highness", "极夜真梦" },
            { "Feather", "霓裾翩跹" },
            { "Floral", "纱中幽兰" },
            { "Summertime", "闪耀协奏" },
            { "Sea", "海风之梦" },
        };

        // 联机
        // 1p_2 与 p_2 为同一位置
        // 1p_4 与 p_4 为同一位置
        AvatarSideIconRectListMap = new Dictionary<string, List<Rect>>
        {
            {
                "1p_2", [
                    new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(375 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
                    new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(470 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
                ]
            },
            {
                "1p_3", [
                    new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(375 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
                    new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(470 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
                ]
            },
            { "1p_4", [new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(515 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale))] },
            {
                "p_2", [
                    new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(375 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
                    new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(470 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale)),
                ]
            },
            { "p_3", [new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(475 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale))] },
            { "p_4", [new Rect(CaptureRect.Width - (int)(155 * AssetScale), (int)(515 * AssetScale), (int)(76 * AssetScale), (int)(76 * AssetScale))] },
        };

        AvatarIndexRectListMap = new Dictionary<string, List<Rect>>
        {
            {
                "1p_2", [
                    new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(412 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
                    new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(508 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
                ]
            },
            {
                "1p_3", [
                    new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(459 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
                    new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(555 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
                ]
            },
            { "1p_4", [new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(552 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale))] },
            {
                "p_2", [
                    new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(412 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
                    new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(508 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale)),
                ]
            },
            { "p_3", [new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(412 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale))] },
            { "p_4", [new Rect(CaptureRect.Width - (int)(61 * AssetScale), (int)(507 * AssetScale), (int)(28 * AssetScale), (int)(24 * AssetScale))] },
        };

        // 左上角的 1P 图标
        OnePRa = new RecognitionObject
        {
            Name = "1P",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "1p.png", this.systemInfo),
            RegionOfInterest = new Rect(0, 0, CaptureRect.Width / 4, CaptureRect.Height / 7),
            DrawOnWindow = false
        }.InitTemplate();
        // 右侧联机的 P 图标
        PRa = new RecognitionObject
        {
            Name = "P",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "p.png", this.systemInfo),
            RegionOfInterest = new Rect(CaptureRect.Width - (int)(CaptureRect.Width / 12.5), CaptureRect.Height / 5, (int)(CaptureRect.Width / 12.5), CaptureRect.Height / 2 - CaptureRect.Width / 7),
            DrawOnWindow = false
        }.InitTemplate();

        WandererIconRa = new RecognitionObject
        {
            Name = "WandererIcon",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "wanderer_icon.png", this.systemInfo),
            DrawOnWindow = false
        }.InitTemplate();
        WandererIconNoActiveRa = new RecognitionObject
        {
            Name = "WandererIconNoActive",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "wanderer_icon_no_active.png", this.systemInfo),
            DrawOnWindow = false
        }.InitTemplate();

        // 右下角的按钮
        ConfirmRa = new RecognitionObject
        {
            Name = "Confirm",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "confirm.png", this.systemInfo),
            RegionOfInterest = new Rect(CaptureRect.Width / 2, CaptureRect.Height / 2, CaptureRect.Width / 2, CaptureRect.Height / 2),
            DrawOnWindow = false
        }.InitTemplate();
        ArtifactAreaRa = new RecognitionObject
        {
            Name = "ArtifactArea",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "artifact_flower_logo.png", this.systemInfo),
            RegionOfInterest = new Rect(CaptureRect.Width / 2, 0, CaptureRect.Width / 2, CaptureRect.Height),
            DrawOnWindow = false
        }.InitTemplate();

        // 点击任意处关闭提示
        ClickAnyCloseTipRa = new RecognitionObject
        {
            Name = "ClickAnyCloseTip",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "click_any_close_tip.png", this.systemInfo),
            RegionOfInterest = new Rect(0, CaptureRect.Height / 2, CaptureRect.Width, CaptureRect.Height / 2),
            DrawOnWindow = false
        }.InitTemplate();

        ExitRa = new RecognitionObject
        {
            Name = "Exit",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "exit.png", this.systemInfo),
            RegionOfInterest = new Rect(0, CaptureRect.Height / 2, CaptureRect.Width / 2, CaptureRect.Height / 2),
            DrawOnWindow = false
        }.InitTemplate();

        // 自动秘境
        // LockIconRa = new RecognitionObject
        // {
        //     Name = "LockIcon",
        //     RecognitionType = RecognitionTypes.TemplateMatch,
        //     TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "lock_icon.png", this.systemInfo),
        //     RegionOfInterest = new Rect(CaptureRect.Width - (int)(215 * AssetScale), 0, (int)(215 * AssetScale), (int)(80 * AssetScale)),
        //     DrawOnWindow = false
        // }.InitTemplate();

        AbnormalIconRa = new RecognitionObject
        {
            Name = "AbnormalIcon",
            RecognitionType = RecognitionTypes.TemplateMatch,
            TemplateImageMat = GameTaskManager.LoadAssetImage("AutoFight", "abnormal_icon.png", this.systemInfo),
            RegionOfInterest = new Rect(0, (int)(CaptureRect.Height * 0.08), (int)(CaptureRect.Width * 0.04), (int)(CaptureRect.Height * 0.07)),
            DrawOnWindow = false
        }.InitTemplate();
    }
}
