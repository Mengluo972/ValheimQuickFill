# Changelog

## 1.1.1

- 修正依赖声明：改为 `denikson-BepInExPack_Valheim-5.4.2350`。
  此前误写成 `BepInEx-BepInExPack_Valheim-5.4.801`，那是 BepInEx 命名空间下的测试版本（描述为
  "DO NOT USE, TESTING VERSION"），会导致 mod 管理器安装到错误的 BepInEx 包。
  依赖本身是必需的，不可移除。

## 1.1.0

- 修复：改用游戏自身的输入层 `ZInput.GetKey`，解决在新版 Input System 下 Ctrl+E 无反应的问题。
- 按键配置支持 `Control` / `Shift` / `Alt`（任一侧）及具体 KeyCode，可用逗号配置多个键。
- 增加长按节流，长按 E 只会触发一次装填。
- 按剩余容量计算装入数量，避免非主机客户端因队列延迟而超过上限。
- 诊断日志改为配置项 `DebugLogging`，默认关闭。

## 1.0.0

- 首个版本：按住 Ctrl 再与熔炉 / 炭窑交互，一键装满矿石或燃料。
