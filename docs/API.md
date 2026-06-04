# Utility — Toast 模块 API 参考文档

## 项目概览

| 属性 | 值 |
|---|---|
| 程序集 | `Utility` |
| 目标框架 | `netstandard2.1` |
| C# 版本 | 10.0 |
| 依赖 | `UnityEngine.dll`（运行时由 BepInEx / MelonLoader 提供） |
| 输出 | 类库 DLL，供 BepInEx 6 IL2CPP / MelonLoader 模组引用 |

项目提供游戏内 OnGUI Toast 通知系统和 Windows 原生备用 Toast。两个系统相互独立，可单独使用。

---

## 命名空间结构

```
Utility.dll
├── Utility.Toast          # 核心：Unity OnGUI Toast 通知
│   ├── ToastType (enum)
│   ├── Anchor (enum)
│   ├── ToastData (class)
│   ├── ToastStyle (class)
│   └── ToastUI (class : MonoBehaviour)
└── Utility.Legacy         # 备用：Windows 原生 Toast
    └── PowerShellToast (static class)
```

---

## 一、核心模块 `Utility.Toast`

### 1.1 ToastUI（主入口）

`ToastUI` 是一个 **MonoBehaviour 单例**，挂载在 DontDestroyOnLoad 的 GameObject 上。

> **⚠️ IL2CPP 兼容：** Unity IL2CPP AOT 运行时会裁剪 `GameObject.AddComponent` 方法，因此 ToastUI **不能自行创建**。必须由宿主插件通过框架的 `AddComponent<T>()` 创建，`Awake()` 会自动注册单例。

#### 初始化（必须在使用前调用一次）

```csharp
// BepInEx — 在 Plugin.Load() 中
AddComponent<ToastUI>();                          // Awake() 自动注册，后续直接 Instance
// 或显式赋值
ToastUI.Instance = AddComponent<ToastUI>();

// MelonLoader — 在 MelonMod.OnInitializeMelon() 中
// 类似方式，使用 MelonLoader 提供的组件创建方法
```

#### 获取实例

```csharp
ToastUI ui = ToastUI.Instance;   // 初始化后即可使用
if (ui == null) return;          // 未初始化时安全判空
```

#### 类型常量（int，IL2CPP 兼容）

| 常量 | 值 | 含义 |
|---|---|---|
| `ToastUI.TYPE_INFO` | 0 | 信息 |
| `ToastUI.TYPE_SUCCESS` | 1 | 成功 |
| `ToastUI.TYPE_WARN` | 2 | 警告 |
| `ToastUI.TYPE_ERROR` | 3 | 错误 |

#### 锚点常量（int，IL2CPP 兼容）

| 常量 | 值 | 屏幕位置 |
|---|---|---|
| `ToastUI.ANCHOR_TL` | 0 | 左上 |
| `ToastUI.ANCHOR_TC` | 1 | 中上 |
| `ToastUI.ANCHOR_TR` | 2 | 右上 |
| `ToastUI.ANCHOR_BL` | 3 | 左下 |
| `ToastUI.ANCHOR_BC` | 4 | 中下 |
| `ToastUI.ANCHOR_BR` | 5 | **右下（默认）** |

#### 方法

---

##### `Show(string title, string message, int type = 0, float duration = 3f)`

底层通用显示方法。其他便捷方法均调用此方法。

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `title` | `string` | 必填 | 通知标题（单行粗体） |
| `message` | `string` | 必填 | 通知消息（自动换行） |
| `type` | `int` | `0` | 通知类型：0=Info 1=Success 2=Warning 3=Error |
| `duration` | `float` | `3.0` | 显示时长（秒），最小值 1.0 |

**队列行为：**
- 若活跃通知数 < `Max`（默认 5），直接显示
- 否则加入等待队列，先进先出
- 队列上限 50 条，超出则静默丢弃

```csharp
// 显示一条蓝色 Info 通知，持续 3 秒
ToastUI.Instance.Show("提示", "数据已加载", ToastUI.TYPE_INFO, 3f);
```

---

##### `Info(string title, string message, float duration = 3f)`

显示 Info 类型通知（蓝色强调条）。

```csharp
ToastUI.Instance.Info("提示", "配置已保存");
```

---

##### `Success(string title, string message, float duration = 3f)`

显示 Success 类型通知（绿色强调条）。

```csharp
ToastUI.Instance.Success("完成", "模组加载成功");
```

---

##### `Warn(string title, string message, float duration = 4f)`

显示 Warning 类型通知（橙色强调条）。默认比 Info/Success 长 1 秒。

```csharp
ToastUI.Instance.Warn("警告", "配置文件即将过期");
```

---

##### `Error(string title, string message, float duration = 5f)`

显示 Error 类型通知（红色强调条）。默认显示时间最长。

```csharp
ToastUI.Instance.Error("错误", "网络请求失败");
```

---

##### `Config(...)`

运行时配置样式。所有参数均可选，只更新传入的参数。

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `width` | `float?` | `425` | 卡片宽度（px） |
| `maxHeight` | `float?` | `105` | 卡片最大高度（px） |
| `margin` | `float?` | `20` | 距屏幕边缘间距（px） |
| `gap` | `float?` | `15` | 卡片之间间距（px） |
| `max` | `int?` | `5` | 同时显示最大数 |
| `titleSize` | `int?` | `19` | 标题字号 |
| `textSize` | `int?` | `16` | 消息字号 |
| `anchor` | `int?` | `5` (BR) | 锚点位置（0-5，见锚点常量） |

```csharp
// 改为左上角显示，最多 3 条
ToastUI.Instance.Config(anchor: ToastUI.ANCHOR_TL, max: 3);

// 只改卡宽
ToastUI.Instance.Config(width: 350f);

// 混合配置
ToastUI.Instance.Config(max: 8, titleSize: 16, textSize: 14, anchor: ToastUI.ANCHOR_BC);
```

---

##### `Clear()`

立即清除所有活跃和等待中的通知。

```csharp
ToastUI.Instance.Clear();
```

---

##### `Count`（属性）

返回当前通知总数（活跃 + 队列中）。线程安全。

```csharp
int total = ToastUI.Instance.Count;
if (total > 10) { /* 积压过多 */ }
```

---

### 1.2 ToastType 枚举

```csharp
namespace Utility.Toast
{
    public enum ToastType
    {
        Info,       // 0 — 蓝色强调
        Success,    // 1 — 绿色强调
        Warning,    // 2 — 橙色强调
        Error       // 3 — 红色强调
    }
}
```

> **注意：** 调用 `ToastUI` 的公开方法时，**请使用 int 常量**（`ToastUI.TYPE_INFO` 等）而非直接使用此枚举，以确保 IL2CPP 兼容性。

---

### 1.3 Anchor 枚举

```csharp
namespace Utility.Toast
{
    public enum Anchor
    {
        TopLeft, TopCenter, TopRight,          // 0, 1, 2
        BottomLeft, BottomCenter, BottomRight   // 3, 4, 5
    }
}
```

锚点决定通知卡片在屏幕上的基准位置。`Config(anchor: ...)` 内部使用 int 值（0-5）。

---

### 1.4 ToastData

单条通知的运行时不透明数据结构。**调用方不应直接创建此对象**，由 `ToastUI.Show()` 内部构造。

| 字段/属性 | 类型 | 说明 |
|---|---|---|
| `Title` | `string` | 标题 |
| `Message` | `string` | 消息正文 |
| `Type` | `ToastType` | 类型 |
| `Duration` | `float` | 总时长（秒） |
| `Remaining` | `float` | 剩余时间（秒） |
| `Expired` | `bool` | 是否已过期（`Remaining <= 0`） |
| `Alpha` | `float` | 当前透明度（0-1），含淡入淡出 |

**动画时序：**
- **0 → 0.3s**：淡入（alpha 从 0 到 1）
- **0.3s → Duration-0.5s**：完全显示（alpha = 1）
- **最后 0.5s**：淡出（alpha 从 1 到 0）

---

### 1.5 ToastStyle

样式配置对象，`ToastUI` 内部持有单例。通过 `ToastUI.Instance.Config(...)` 间接修改。

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Width` | `float` | `425` | 卡片宽度（px） |
| `MaxHeight` | `float` | `105` | 卡片最大高度（px） |
| `Margin` | `float` | `20` | 距屏幕边缘距离（px） |
| `Gap` | `float` | `15` | 卡片间距（px） |
| `Bar` | `float` | `6` | 左侧颜色强调条宽度（px） |
| `TitleSize` | `int` | `19` | 标题字体大小 |
| `TextSize` | `int` | `16` | 消息字体大小 |
| `Max` | `int` | `5` | 同时可见最大通知数 |
| `BgColor` | `Color` | `(0.06, 0.06, 0.08, 0.94)` | 卡片背景色 |
| `TitleColor` | `Color` | `(0.95, 0.95, 0.97)` | 标题颜色 |
| `TextColor` | `Color` | `(0.70, 0.70, 0.75)` | 消息颜色 |
| `InfoColor` | `Color` | `(0.29, 0.56, 0.85)` | Info 强调色 |
| `SuccessColor` | `Color` | `(0.26, 0.71, 0.51)` | Success 强调色 |
| `WarnColor` | `Color` | `(0.94, 0.63, 0.21)` | Warning 强调色 |
| `ErrorColor` | `Color` | `(0.94, 0.28, 0.28)` | Error 强调色 |
| `Anchor` | `Anchor` | `BottomRight` | 锚点位置 |

**如需完全替换样式**（不推荐，通常用 `Config` 即可）：

```csharp
// ToastStyle 是 public class，可以 new 后赋值给内部字段
// 但 ToastUI 没有直接暴露 _style，建议仅通过 Config() 调整
```

---

## 二、备用模块 `Utility.Legacy`

### 2.1 PowerShellToast

**静态类**，通过 PowerShell 进程调用 Windows 原生 Toast API。实现为常驻 `powershell.exe` 进程（隐藏窗口），通过标准输入管道传递 JSON 指令。

> **注意：** 此模块仅在 Windows 10+ 上工作，且需要应用在注册表中注册了 AppUserModelID。对于 BepInEx/MelonLoader 注入的模组，这可能不可用——它作为 **OnGUI Toast 的备选方案** 保留。

#### 方法

---

##### `Init(string appName, Action<string> log = null)`

启动一个隐藏的 PowerShell 进程并初始化 Toast 通知管理器。

| 参数 | 类型 | 说明 |
|---|---|---|
| `appName` | `string` | 应用标识名，用于系统通知来源显示 |
| `log` | `Action<string>` | 可选。错误日志回调，建议传入 BepInEx 的 `Log.LogError` 或兼容方法 |

**方法幂等性：** 重复调用 `Init` 不会重置已有进程。如需重启，先调用 `Stop()`。

```csharp
PowerShellToast.Init("MyMod", msg => Plugin.Log.LogError(msg));
```

---

##### `Show(string title, string message, int duration = 3)`

显示一条 Windows 原生 Toast 通知。

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `title` | `string` | 必填 | 通知标题 |
| `message` | `string` | 必填 | 通知正文 |
| `duration` | `int` | `3` | 显示时长（秒） |

**字符转义：** 内部自动转义 `"`、`&`、`<`、`>` 四个特殊字符，避免 JSON/XML 注入。

```csharp
if (PowerShellToast.Running)
    PowerShellToast.Show("提醒", "活动结束", 5);
```

---

##### `Stop()`

关闭标准输入管道并终止 PowerShell 进程。进程退出时会自动调用（通过 `AppDomain.ProcessExit` 事件）。

```csharp
// 模组卸载时手动清理
PowerShellToast.Stop();
```

---

##### `Running`（属性）

返回 `bool`，指示 PowerShell 进程是否正在运行。

```csharp
if (!PowerShellToast.Running)
{
    // 降级到 OnGUI Toast
    ToastUI.Instance.Warn("注意", "系统通知不可用，使用游戏内通知");
}
```

---

## 三、典型用法场景

### 3.1 BepInEx 插件启动

```csharp
using Utility.Toast;
using Utility.Legacy;

[BepInPlugin(GUID, NAME, VERSION)]
public class MyPlugin : BasePlugin
{
    public override void Load()
    {
        Log = base.Log;

        // 必须：创建 ToastUI 组件（任选一种方式）
        AddComponent<ToastUI>();                         // Awake() 自动注册
        // 或: ToastUI.Instance = AddComponent<ToastUI>();

        // 启动问候
        ToastUI.Instance.Success(NAME, $"v{VERSION} 加载成功");

        // 可选：初始化系统备用 Toast
        if (Application.platform == RuntimePlatform.WindowsPlayer)
            PowerShellToast.Init(NAME, msg => Log.LogError(msg));
    }

    public override bool Unload()
    {
        ToastUI.Instance?.Clear();
        PowerShellToast.Stop();
        return base.Unload();
    }
}
```

### 3.2 配置变更通知

```csharp
// 在 ConfigEntry 变更回调中
config.SettingChanged += (_, _) =>
{
    ToastUI.Instance.Info("配置", "设置已更新，重启后生效");
};
```

### 3.3 错误处理

```csharp
try
{
    await FetchData();
}
catch (HttpException e)
{
    ToastUI.Instance.Error("网络错误", e.Message, 8f); // 错误消息显示更久
}
catch (Exception e)
{
    ToastUI.Instance.Error("未知错误", e.Message);
}
```

### 3.4 自定义布局

```csharp
// 左上角小卡片风格
ToastUI.Instance.Config(
    width: 300f,
    max: 3,
    titleSize: 14,
    textSize: 12,
    gap: 8f,
    anchor: ToastUI.ANCHOR_TL
);
```

### 3.5 静默模式（禁用通知）

```csharp
// 配置 Max = 0 后，所有新通知将进入队列但不显示
// 恢复后队列中的通知会立即显示
ToastUI.Instance.Config(max: 0);   // 暂停显示
// ... 做一些不希望打扰用户的事 ...
ToastUI.Instance.Config(max: 5);   // 恢复
```

### 3.6 批量操作前清空

```csharp
// 切换场景前清空旧通知
ToastUI.Instance.Clear();
```

---

## 四、实现细节与注意事项

### 线程安全

- `Show()`、`Clear()`、`Count` 内部使用 `lock(_lock)` 保护共享数据结构
- `OnGUI()` 和 `Update()` 运行在 Unity 主线程，读取 `_active` 列表时加锁
- 可从任意线程调用 `Show()`，实际通知将在主线程的 `Update()` 中处理

### IL2CPP 兼容性

- 所有公开 API 的参数使用基础类型（`string`、`int`、`float`），避免 IL2CPP 泛型互操作问题
- 内部枚举 `ToastType` 和 `Anchor` 仅作内部使用，对外暴露 int 常量
- **`AddComponent` 已从 `Instance` getter 中移除**，因为 Unity IL2CPP AOT 会裁剪运行时未使用的方法。必须由宿主插件通过其框架的 `AddComponent<T>()` 创建 ToastUI（BepInEx 的 `BasePlugin.AddComponent<T>()` 内部有 IL2CPP 安全适配）

### 性能特征

- 无通知时 `OnGUI()` 立即返回，开销可忽略
- 纹理（24×24 ARGB32）仅生成一次并缓存
- GUIStyle 对象缓存在 `_bgTex`/`_boxStyle`/`_titleStyle`/`_textStyle` 字段中
- `GUIContent` 通过 `.text` 属性复用，避免每帧 GC Alloc
- `Config()` 会使 `_titleStyle` 失效并在下一帧重建

### 生命周期

- `ToastUI` 的 GameObject 标记为 `DontDestroyOnLoad`，场景切换时不会销毁
- `Awake()` 执行单例检查，重复添加会 `Destroy(gameObject)`
- `OnDestroy()` 清除单例引用，下次 `Instance` 访问时重新创建

### PowerShellToast 限制

- 仅 Windows 10+（需要 Windows.UI.Notifications API）
- 注入型宿主（Unity IL2CPP）可能未注册 AppUserModelID，导致 Toast 无法显示
- `powershell.exe` 进程占用少量后台内存
- JSON 通信使用简写键名（`T`=Title, `M`=Message, `E`=Expiration）以减少管道传输
