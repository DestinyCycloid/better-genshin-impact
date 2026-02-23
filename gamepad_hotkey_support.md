# 手柄快捷键支持

## 概述
为了让手柄模式下的用户体验更好，现在快捷键系统已支持手柄按键监听。用户可以将软件的各种功能绑定到手柄按键上，无需再按键盘快捷键。

**✨ 新增功能：支持手柄组合键！** 可以设置"修饰键 + 主键"的组合，例如 `LB+A`、`RB+X` 等，大大增加可用快捷键数量。

## 实现的功能

### 1. 扩展的手柄按键支持
`GamepadInputMonitor.cs` 现在支持所有常用手柄按键：
- **面部按键**: A, B, X, Y
- **肩键**: LB, RB
- **扳机**: LT, RT（阈值30/255）
- **系统按键**: Start, Back
- **摇杆按键**: LeftThumb (L3), RightThumb (R3)
- **十字键**: DPadUp, DPadDown, DPadLeft, DPadRight

### 2. 组合键支持 ⭐
- 支持"修饰键 + 主键"的组合方式
- 必须先按下修饰键，再按下主键才会触发
- 松开任意一个键都会结束组合键状态
- 示例：`LB+A`, `RB+X`, `LT+DPadUp`, `Start+Back`

### 3. 新增 GamepadHook 类
类似于 `KeyboardHook` 和 `MouseHook`，提供：
- 按键按下事件 (`GamepadDownEvent`)
- 按键松开事件 (`GamepadUpEvent`)
- 按键触发事件 (`GamepadPressed`)
- 支持"按住模式"和"点击模式"
- 支持单键和组合键
- 后台轮询监听（50ms间隔）

### 4. 快捷键类型扩展
`HotKeyTypeEnum` 新增：
- `GamepadMonitor` - 手柄监听

现在支持三种快捷键类型循环切换：
- 全局热键 → 键鼠监听 → 手柄监听 → 全局热键

### 5. HotKeySettingModel 集成
- 添加 `GamepadMonitorHook` 属性
- `RegisterHotKey()` 方法支持手柄按键注册（单键和组合键）
- `UnRegisterHotKey()` 方法支持手柄清理
- `OnSwitchHotKeyType()` 支持三种类型循环切换

## 使用方法

### 配置单键快捷键
1. 打开快捷键设置页面
2. 选择要配置的功能
3. 点击"切换快捷键类型"按钮，切换到"手柄监听"
4. 设置手柄按键（如：`A`, `B`, `LB`, `RB`等）
5. 保存配置

### 配置组合键快捷键 ⭐
1. 打开快捷键设置页面
2. 选择要配置的功能
3. 点击"切换快捷键类型"按钮，切换到"手柄监听"
4. 设置组合键（格式：`修饰键+主键`）
   - 例如：`LB+A`, `RB+X`, `LT+B`, `Start+DPadUp`
5. 保存配置

### 支持的按键名称
配置时使用以下按键名称：
- `A`, `B`, `X`, `Y`
- `LB`, `RB`, `LT`, `RT`
- `Start`, `Back`
- `LeftThumb`, `RightThumb`
- `DPadUp`, `DPadDown`, `DPadLeft`, `DPadRight`

### 组合键规则
- 格式：`修饰键+主键`（中间用加号连接，无空格）
- 修饰键和主键可以是任意按键
- 常用修饰键推荐：`LB`, `RB`, `LT`, `RT`, `Start`, `Back`
- 触发条件：修饰键和主键同时按下
- 松开条件：任意一个键松开

## 技术细节

### 组合键检测机制
```csharp
// 组合键：修饰键和主键都必须按下
bool modifierPressed = _monitor.IsButtonPressed(_modifierButton);
bool mainPressed = _monitor.IsButtonPressed(_bindButton);
isPressed = modifierPressed && mainPressed;
```

### 按键检测机制
- 使用 XInput API (`xinput1_4.dll`) 读取手柄状态
- 后台任务每50ms轮询一次手柄状态
- 检测按键边沿（按下/松开）触发事件
- 自动检测手柄连接/断开状态

### 性能优化
- 手柄未连接时降低检测频率（500ms）
- 使用异步任务避免阻塞主线程
- 取消令牌机制确保资源正确释放

### 兼容性
- 支持所有 XInput 兼容手柄（Xbox手柄、大部分第三方手柄）
- 仅读取第一个手柄（索引0）
- 扳机阈值设置为30/255，避免误触

## 示例配置

### 单键示例

#### 一键宏功能
```
功能: 一键宏（按角色）
快捷键类型: 手柄监听
按键: RB
模式: 触发
```

#### 冷却提示开关
```
功能: 冷却提示开关
快捷键类型: 手柄监听
按键: Back
模式: 点击
```

### 组合键示例 ⭐

#### 自动拾取开关
```
功能: 自动拾取开关
快捷键类型: 手柄监听
按键: LB+A
模式: 点击
说明: 按住LB，再按A触发
```

#### 自动战斗开关
```
功能: 自动战斗开关
快捷键类型: 手柄监听
按键: RB+B
模式: 点击
说明: 按住RB，再按B触发
```

#### 快速传送
```
功能: 快速传送
快捷键类型: 手柄监听
按键: Start+DPadUp
模式: 点击
说明: 按住Start，再按十字键上触发
```

#### 截图功能
```
功能: 截图
快捷键类型: 手柄监听
按键: LT+RT
模式: 点击
说明: 同时按下左右扳机触发
```

## 推荐的组合键配置

### 常用功能（避免与游戏冲突）
- `LB+A` - 自动拾取开关
- `LB+B` - 自动战斗开关
- `LB+X` - 自动秘境开关
- `LB+Y` - 自动钓鱼开关
- `RB+A` - 快速传送
- `RB+B` - 队伍切换
- `RB+X` - 截图
- `RB+Y` - 录制
- `Start+DPadUp` - 功能1
- `Start+DPadDown` - 功能2
- `Start+DPadLeft` - 功能3
- `Start+DPadRight` - 功能4

### 为什么使用组合键？
1. **避免冲突**：游戏内常用A/B/X/Y等按键，组合键不会误触
2. **更多选择**：16个单键 × 16个修饰键 = 256种组合
3. **更安全**：需要同时按两个键，不容易误触发
4. **更直观**：可以按功能分组（如LB系列、RB系列）

## 注意事项

1. **手柄连接**: 确保手柄已连接并被系统识别为XInput设备
2. **按键冲突**: 避免将游戏内使用的按键设置为快捷键（推荐使用组合键）
3. **推荐按键**: 
   - 单键：建议使用 Back, Start 等不常用按键
   - 组合键：建议使用 LB/RB/LT/RT 作为修饰键
4. **性能影响**: 手柄监听会持续运行后台任务，但CPU占用极低（<1%）
5. **组合键顺序**: 必须先按修饰键，再按主键（顺序很重要）

## 未来改进方向

1. ✅ ~~支持手柄组合键~~ (已完成)
2. 支持多个手柄
3. 支持三键组合（如 LB+RB+A）
4. 支持摇杆方向作为快捷键
5. 可视化手柄按键配置界面
6. 手柄震动反馈
7. 自定义扳机阈值

## 相关文件

- `BetterGenshinImpact/Core/Simulator/GamepadInputMonitor.cs` - 手柄输入监听
- `BetterGenshinImpact/Model/GamepadHook.cs` - 手柄快捷键Hook（支持组合键）
- `BetterGenshinImpact/Model/HotKeyTypeEnum.cs` - 快捷键类型枚举
- `BetterGenshinImpact/Model/HotKeySettingModel.cs` - 快捷键设置模型
