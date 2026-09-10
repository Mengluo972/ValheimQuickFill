using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ValheimQuickFill
{
    // 与游戏原有交互并存：默认按住 Ctrl 再按 E / 使用物品时，把矿石或燃料一次装满到容量上限。
    // 未按修饰键时走原版逐个装入逻辑，不改变任何原版行为。
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class QuickFillPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.valheim.quickfill";
        public const string PluginName = "ValheimQuickFill";
        public const string PluginVersion = "1.1.1";

        internal static ManualLogSource Log;

        private static ConfigEntry<string> _fillModifierName;
        internal static ConfigEntry<bool> DebugLogging;
        private static List<KeyCode> _modifierKeys = new List<KeyCode> { KeyCode.LeftControl };

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _fillModifierName = Config.Bind(
                "General", "FillModifier", "LeftControl",
                "触发一键装满需要按住的修饰键。可用值：Control / Shift / Alt（任一侧），" +
                "或具体 KeyCode 名（LeftControl、RightControl、LeftShift、LeftAlt 等），" +
                "也可用逗号分隔多个键（任一按下即触发）。");
            RebuildKeyList();

            DebugLogging = Config.Bind(
                "Diagnostics", "DebugLogging", false,
                "开启后会在日志中输出补丁命中细节，用于排查问题。正常游玩建议关闭。");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Patches));
            LogPatchStatus();
            Log.LogInfo("ValheimQuickFill loaded (v" + PluginVersion + ")");
        }

        // 启动即自检：确认两个目标方法确实挂上了 prefix，便于无需按键就能判断补丁状态。
        private static void LogPatchStatus()
        {
            foreach (string name in new[] { "OnAddOre", "OnAddFuel" })
            {
                MethodInfo mi = AccessTools.Method(typeof(Smelter), name);
                if (mi == null)
                {
                    Log.LogError($"[QuickFill][diag] Smelter.{name} not found!");
                    continue;
                }
                HarmonyLib.Patches info = Harmony.GetPatchInfo(mi);
                int prefixes = info?.Prefixes?.Count ?? 0;
                Log.LogInfo($"[QuickFill][diag] Smelter.{name}: prefixes={prefixes}");
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        internal static void RebuildKeyList()
        {
            _modifierKeys = ParseKeys(_fillModifierName?.Value);
            Log.LogInfo("[QuickFill] modifier keys = " + string.Join(", ", _modifierKeys.ConvertAll(k => k.ToString()).ToArray()));
        }

        private static List<KeyCode> ParseKeys(string raw)
        {
            var list = new List<KeyCode>();
            if (string.IsNullOrEmpty(raw)) raw = "LeftControl";
            foreach (string partRaw in raw.Split(','))
            {
                string part = partRaw.Trim();
                if (part.Length == 0) continue;
                switch (part.ToLowerInvariant())
                {
                    case "control":
                    case "ctrl":
                        list.Add(KeyCode.LeftControl);
                        list.Add(KeyCode.RightControl);
                        break;
                    case "shift":
                        list.Add(KeyCode.LeftShift);
                        list.Add(KeyCode.RightShift);
                        break;
                    case "alt":
                        list.Add(KeyCode.LeftAlt);
                        list.Add(KeyCode.RightAlt);
                        break;
                    default:
                        if (Enum.TryParse(part, true, out KeyCode kc)) list.Add(kc);
                        break;
                }
            }
            if (list.Count == 0) list.Add(KeyCode.LeftControl);
            return list;
        }

        // 用游戏自己的输入层（新 Input System）判定，避免 BepInEx 旧版 Input 在新输入后端下失效。
        internal static bool ModifierHeld()
        {
            foreach (KeyCode k in _modifierKeys)
            {
                if (ZInput.GetKey(k, logWarning: false)) return true;
            }
            return false;
        }
    }

    internal static class Patches
    {
        // 反射缓存
        private static readonly FieldInfo FZNview = AccessTools.Field(typeof(Smelter), "m_nview");
        private static readonly MethodInfo MFindCookable = AccessTools.Method(typeof(Smelter), "FindCookableItem");
        private static readonly MethodInfo MGetQueueSize = AccessTools.Method(typeof(Smelter), "GetQueueSize");
        private static readonly MethodInfo MIsItemAllowedName = AccessTools.Method(typeof(Smelter), "IsItemAllowed", new[] { typeof(string) });

        // 长按 E 时游戏会以固定间隔重复调用，这里做节流，保证一次交互只装一次。
        private const float TakeoverCooldown = 0.25f;
        private static float _lastOreTakeover = -100f;
        private static float _lastFuelTakeover = -100f;

        // 诊断计数（仅前几次打日志，避免刷屏）
        private static int _oreDiag;
        private static int _fuelDiag;

        private static ZNetView NView(Smelter s) => (ZNetView)FZNview.GetValue(s);
        private static int QueueSize(Smelter s) => (int)MGetQueueSize.Invoke(s, null);

        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        [HarmonyPrefix]
        private static bool OnAddOrePrefix(Smelter __instance, Switch sw, Humanoid user, ItemDrop.ItemData item, ref bool __result)
        {
            bool mod = QuickFillPlugin.ModifierHeld();
            if (QuickFillPlugin.DebugLogging.Value && _oreDiag < 5)
            {
                _oreDiag++;
                QuickFillPlugin.Log.LogInfo($"[QuickFill] OnAddOre hit, player={user is Player}, modifier={mod}");
            }
            if (!(user is Player) || !mod) return true; // 走原版单次逻辑

            // 节流：长按 E 会重复进入本方法，只处理第一次
            if (Time.time - _lastOreTakeover < TakeoverCooldown)
            {
                __result = true;
                return false;
            }
            _lastOreTakeover = Time.time;

            Inventory inventory = user.GetInventory();
            if (inventory == null || __instance.m_conversion == null || __instance.m_conversion.Count == 0)
            {
                __result = false;
                return false;
            }

            // 用“剩余容量”限制本次装入数量，避免队列回读滞后（非主机客户端）导致超上限
            int room = __instance.m_maxOre - QueueSize(__instance);
            if (room <= 0)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                __result = false;
                return false;
            }

            int added = 0;
            while (added < room)
            {
                ItemDrop.ItemData cookable = (ItemDrop.ItemData)MFindCookable.Invoke(__instance, new object[] { inventory });
                if (cookable == null) break;
                if (!(bool)MIsItemAllowedName.Invoke(__instance, new object[] { cookable.m_dropPrefab.name })) break;

                inventory.RemoveItem(cookable, 1);
                NView(__instance).InvokeRPC("RPC_AddOre", cookable.m_dropPrefab.name, cookable.m_cheated);
                added++;
            }

            if (added > 0)
            {
                user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_added") + " x" + added);
            }
            else
            {
                user.Message(MessageHud.MessageType.Center, "$msg_noprocessableitems");
            }
            __result = added > 0;
            return false;
        }

        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        [HarmonyPrefix]
        private static bool OnAddFuelPrefix(Smelter __instance, Switch sw, Humanoid user, ItemDrop.ItemData item, ref bool __result)
        {
            bool mod = QuickFillPlugin.ModifierHeld();
            if (QuickFillPlugin.DebugLogging.Value && _fuelDiag < 5)
            {
                _fuelDiag++;
                QuickFillPlugin.Log.LogInfo($"[QuickFill] OnAddFuel hit, player={user is Player}, modifier={mod}");
            }
            if (!(user is Player) || !mod) return true;

            if (Time.time - _lastFuelTakeover < TakeoverCooldown)
            {
                __result = true;
                return false;
            }
            _lastFuelTakeover = Time.time;

            if (__instance.m_fuelItem == null)
            {
                __result = false;
                return false;
            }

            string fuelName = __instance.m_fuelItem.m_itemData.m_shared.m_name;
            Inventory inventory = user.GetInventory();
            if (inventory == null)
            {
                __result = false;
                return false;
            }

            float fuel = GetFuel(__instance);
            int maxFuel = __instance.m_maxFuel;
            if (maxFuel > 0 && fuel >= maxFuel - 0.0001f)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_itsfull");
                __result = false;
                return false;
            }

            int added = 0;
            // 原版每次加 1 单位。容量上限 m_maxFuel；为 0 表示无上限，只加一次保持原版语义。
            while (maxFuel == 0 || fuel + added < maxFuel - 0.0001f)
            {
                if (!inventory.HaveItem(fuelName)) break;
                inventory.RemoveItem(fuelName, 1);
                NView(__instance).InvokeRPC("RPC_AddFuel");
                added++;
                if (maxFuel == 0) break;
            }

            if (added > 0)
            {
                user.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize("$msg_added") + " " + Localization.instance.Localize(fuelName) + " x" + added);
                __result = true;
            }
            else
            {
                user.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize("$msg_donthaveany") + " " + Localization.instance.Localize(fuelName));
                __result = false;
            }
            return false;
        }

        private static float GetFuel(Smelter s)
        {
            ZNetView nview = NView(s);
            if (nview == null || !nview.IsValid()) return 0f;
            return nview.GetZDO().GetFloat(ZDOVars.s_fuel);
        }
    }
}
