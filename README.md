# ValheimQuickFill

一键装满熔炉与炭窑。按住 **Ctrl** 再按 **E** 与熔炉、炭窑（或风车磨坊）交互，即可把矿石或燃料一次性装到容量上限。不按 Ctrl 时保持原版逐个装入的行为，两者互不影响。

Valheim 里往熔炉塞矿石、往炭窑塞木材都要一个一个点，长按 E 的灌注速度又很慢。这个 mod 只是把"点很多次"变成"一次点满"，不改变任何配方、产量或游戏规则。

## 功能

- **装矿**：对准熔炉，按住 `Ctrl` + `E`，把背包中的可熔矿石一次装满（上限 10）。
- **加燃料**：对准熔炉或炭窑，按住 `Ctrl` + `E`，把木炭 / 木材一次装满（上限 10）。
- 不按修饰键时，完全是原版行为。
- 多人联机中经由游戏自带的 RPC 同步，只需客户端安装。

## 安装

方式一（推荐，用 mod 管理器）：通过 **r2modman / Thunderstore Mod Manager** 搜索 `ValheimQuickFill` 安装，会自动处理好 BepInEx 依赖。

方式二（手动）：

1. 先安装 **BepInExPack for Valheim**（`denikson-BepInExPack_Valheim`）。
2. 把本 mod 的 `ValheimQuickFill.dll` 放入 `BepInEx/plugins/`。
3. 启动游戏。

## 配置

首次启动后生成 `BepInEx/config/local.valheim.quickfill.cfg`：

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `FillModifier` | `LeftControl` | 触发一键装满需按住的键。支持 `Control` / `Shift` / `Alt`（任一侧），或具体键名如 `LeftShift`；逗号分隔可配多个键。 |
| `DebugLogging` | `false` | 开启后在日志输出补丁命中细节，用于排查。 |

## 会不会影响成就

不会。本 mod 不设置 `Game.isModded`、不触碰人物的 `m_usedCheats`、不给物品打作弊标记，因此不会禁用成就。若你同时使用控制台作弊命令或其他主动标记 mod 的插件，则属于那些功能自身的影响。

## 兼容性

- 游戏版本：Unity 6 时期的 Valheim（使用新版 Input System）。
- 依赖：BepInEx 5.x + HarmonyX。

## 已知问题 / 说明

- 修改按键或开关插件后需要**重启游戏**，无法热插拔。
- Steam 的"验证游戏文件完整性"可能删除根目录的 `winhttp.dll`，导致 mod 失效；此时重装 BepInEx 即可。
- 若按键无效，把 `BepInEx/LogOutput.log` 发到问题区。加载成功会有 `ValheimQuickFill loaded` 一行。

## 来源与许可

MIT License，详见仓库内 `LICENSE`。欢迎在 GitHub 提 issue / PR。

## 从源码构建

插件依赖游戏与 BepInEx 的托管程序集，因此需要把它们作为编译引用：

1. 安装 .NET SDK（构建目标为 `netstandard2.1`）与 Node.js（打包脚本用）。
2. 把本仓库克隆到 Valheim 安装目录下（默认假定 `..` 就是游戏根目录）。
3. 游戏根目录需已安装 BepInEx（`BepInEx/core/BepInEx.dll`、`BepInEx/core/0Harmony.dll`）。
4. 构建并生成发布包：

   ```bash
   ./build-package.sh 你的ThunderstoreTeam名
   # 产物：dist/你的Team名-ValheimQuickFill-版本号.zip
   ```

仓库不在游戏目录下时，用属性覆盖路径：

```bash
dotnet build -c Release -p:ValheimDir="C:\Steam\steamapps\common\Valheim\"
```

仓库结构：

```
QuickFillPlugin.cs        插件源码
ValheimQuickFill.csproj   项目文件
manifest.json             Thunderstore 元数据
README.md / CHANGELOG.md / LICENSE
icon.png                  Thunderstore 图标（256x256）
build-package.sh          一键构建 + 打包
tools/zip.js              无依赖 ZIP 打包器（保证 zip 内为正斜杠路径）
tools/make-icon.js        重新生成 icon.png
```

