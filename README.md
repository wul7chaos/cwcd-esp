# CWCD-ESP

> Steam《CWCD》单机俯视角射击游戏 · 护肝辅助工具（**个人自用，不分发**；单机游戏无风险）

基于 **Unity 6 + Mono 运行时** · **Harmony Patch + DLL 注入** 方案（v3）。

---

## 功能

| 功能 | 说明 | 快捷键 | 默认 |
|------|------|--------|------|
| **方框透视 Box ESP** | 敌人方框 + 名称 + 血量（GL 管线绘制） | F6 | ON |
| **物资透视 Loot ESP** | 容器/掉落物上方列出物品与稀有度 | F7 | ON |
| **子弹追踪 Bullet Tracking** | 子弹自动修正弹道朝向最近敌人 + 散布归零 | F8 | OFF |

---

## 目录结构

```
cwcd-esp/
├── CWCD-ESP.sln                  解决方案
├── Directory.Build.props         顶层构建属性（含 GameManagedDir 说明）
├── GamePaths.local.props.example 游戏路径配置示例
│
├── CwcdEspLibrary/               补丁库（注入进游戏，.NET Framework 4.7.2）
│   ├── CwcdEspLibrary.csproj
│   ├── EntryPoint.cs             Harmony 入口（逐条容错注册）
│   ├── EspConfig.cs              全局配置（开关/快捷键/颜色/参数）
│   ├── Patches/                  6 个 Harmony Patch
│   │   ├── Patch_OnGUI.cs        仅 Postfix，Repaint 时绘制
│   │   ├── Patch_ActorUpdate.cs  填充双缓冲 + Swap + 物资脏标记 + 目标选择
│   │   ├── Patch_Camera.cs       缓存 camMain（LateUpdate）
│   │   ├── Patch_Spread.cs       散布归零
│   │   ├── Patch_Bullet.cs       飞行微调（可选，默认未注册）
│   │   └── Patch_ItemContainer.cs 逐 Actor 脏标记
│   ├── ESP/                      BoxESP / LootESP / EspRenderer / ScreenTools
│   ├── Tracking/                 TargetSelector（降频视线检测）/ BulletTracker
│   ├── Data/                     EnemyCache（双缓冲）/ LootCache（逐 Actor 脏标记）/ EspTypes
│   └── Utils/                    Colors / HotkeyManager / GlDrawHelper / PatchGuard
│
├── CwcdEspInjector/              外部注入器（独立，.NET 10.0）
│   ├── CwcdEspInjector.csproj
│   ├── Program.cs                CLI 入口
│   ├── ProcessFinder.cs          进程查找 + mono 模块定位
│   ├── RemotePe.cs               远程 PE 导出表解析
│   ├── MonoInjector.cs           x64 shellcode 注入 + mono 调用链
│   └── Win32.cs                  P/Invoke
│
├── scripts/                      build_library / build_injector / deploy
└── decompile_Assembly-CSharp/    反编译参考源码（非本项目代码）
```

---

## 构建环境

- **.NET SDK 10.0+**（构建注入器与还原 Harmony）
- **.NET Framework 4.7.2 Targeting Pack**（构建补丁库，Windows 已自带参考程序集）
- 游戏已安装（用于补丁库引用 `Assembly-CSharp.dll` / `UnityEngine.*.dll`）

---

## 配置游戏路径

补丁库 `CwcdEspLibrary` 编译时需要引用游戏自带的 DLL。复制 `GamePaths.local.props.example` 为 `GamePaths.local.props`，把 `GameManagedDir` 改成你机器上游戏的 `Managed` 文件夹：

```xml
<GameManagedDir>D:\SteamLibrary\steamapps\common\查无此地 Demo\release\NoSuchPlace_Data\Managed</GameManagedDir>
```

判断标志：该目录下应存在 `Assembly-CSharp.dll` 与 `UnityEngine.CoreModule.dll`。
> 游戏可执行文件为 `NoSuchPlace.exe`，使用 Unity 6 + **MonoBleedingEdge** 后端（运行时 DLL 为 `mono-2.0-bdwgc.dll`，非旧版 `mono-2.0-sgen.dll`）。
（注入器不依赖游戏 DLL，无需配置即可编译。）

---

## 编译

```bat
:: 1. 编译注入器（无需游戏路径）
scripts\build_injector.bat

:: 2. 编译补丁库（需游戏 Managed 目录）
scripts\build_library.bat "D:\SteamLibrary\steamapps\common\查无此地 Demo\release\NoSuchPlace_Data\Managed"

:: 或一键部署（编译两者并汇总到 deploy\）
scripts\deploy.bat "D:\SteamLibrary\steamapps\common\查无此地 Demo\release\NoSuchPlace_Data\Managed"
```

> 若未安装游戏 / 找不到 Managed 目录，补丁库无法编译（缺 `Assembly-CSharp` / `UnityEngine` 类型），注入器仍可独立编译。

---

## 使用

1. 以管理员身份运行 `deploy\CwcdEspInjector.exe`（默认注入 `NoSuchPlace` 进程，加载同目录 `CwcdEspLibrary.dll`，调用 `CwcdEsp.EntryPoint.Load()`）。
   ```
   CwcdEspInjector.exe [进程名] [CwcdEspLibrary.dll全路径] [命名空间] [类名] [方法名]
   ```
2. 启动游戏（注入器会等待进程出现）。
3. 进游戏后用 F6 / F7 / F8 切换功能。

---

## 技术要点（v3 修复）

- **双缓冲真指针交换**（修复 v2 单向 `Exchange`）：写入永远在写缓冲，读取永远在读缓冲，主线程末尾 `Swap` 两引用。
- **OnGUI 仅 Postfix**：去掉 Prefix，只在 Postfix 内用 `Event.current.type == Repaint` 过滤，不破坏原生 UI。
- **子弹方向修正**：在 `LogicActionBulletBase.Parse` Prefix 中改 `equipSnapshot.to`（public 字段）为目标坐标，**保持 `from`=枪口不变**，原方法随后用 `to-from` 计算飞行方向。
- **逐 Actor 脏标记**：容器变动只标记所属 Actor，避免全局全量扫描。
- **GL 完整管线**：`PushMatrix + LoadPixelMatrix + SetPass`，`GUIStyle` 按颜色字典缓存，杜绝颜色闪烁。
- **目标缓存 + 降频视线检测**：每帧 O(N) 选最近候选，仅对最近候选每 ~8 帧 `Physics.Linecast` 一次。
- **逐条 Patch 容错**：每条注册 try-catch，单个失败不影响其他模块。

---

## 风险与待办

| 项 | 说明 |
|----|------|
| IL2CPP 后端 | 已确认当前版本为 **Mono 后端**（`mono-2.0-bdwgc.dll`）。若后续游戏改 IL2CPP，则无 mono 模块，本方案不兼容（注入器会提示）。 |
| 子弹飞行微调 | `Patch_Bullet`（Patch C）为可选占位，默认未注册。 |
| 物资扫描 | `LootCache.ScanContainer` 为骨架，需按 `LogicDataActorItems/Pickable` + `StaticDataManager` 补全物品读取。 |
| 敌人身高 | `EnemyData.Height` 默认 1.8f，可改从 `StaticData_Actor.values["height"]` 读取。 |
| shellcode | 注入器 x64 shellcode 为手工编码，首次使用建议用调试器验证 mono 调用链。 |
