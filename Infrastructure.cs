using System;
using System.Collections.Generic;
using System.Reflection;
using ItemChanger;
using MonoMod.RuntimeDetour;

namespace NightmareAccess
{
    /// <summary>Static logging facade so non-Mod classes do not depend on the Mod instance directly.</summary>
    internal static class ModLog
    {
        public static void Info(string message) => NightmareAccess.Instance?.Log(message);

        public static void Debug(string message) => NightmareAccess.Instance?.LogDebug(message);

        public static void Warn(string message) => NightmareAccess.Instance?.LogWarn(message);

        public static void Error(string message, Exception ex = null) =>
            NightmareAccess.Instance?.LogError(ex == null ? message : $"{message}: {ex}");
    }

    /// <summary>Installs MonoMod detours and keeps them referenced for the lifetime of the mod.</summary>
    internal static class HookUtil
    {
        private static readonly List<Hook> Installed = new();
        private static readonly List<(MethodInfo Target, MethodInfo Detour)> AfterItemChanger = new();
        private static readonly List<Hook> ActiveIcHooks = new();
        private static bool _icSubscribed;

        public static void Install(MethodInfo target, Type owner, string detourName)
        {
            MethodInfo detour = ResolveDetour(target, owner, detourName);
            if (detour == null) return;

            Installed.Add(new Hook(target, detour));
        }

        public static void InstallAfterItemChanger(MethodInfo target, Type owner, string detourName)
        {
            MethodInfo detour = ResolveDetour(target, owner, detourName);
            if (detour == null) return;

            AfterItemChanger.Add((target, detour));
            if (!_icSubscribed)
            {
                _icSubscribed = true;
                Events.OnItemChangerHook += OnItemChangerHook;
                Events.OnItemChangerUnhook += OnItemChangerUnhook;
            }
        }

        private static MethodInfo ResolveDetour(MethodInfo target, Type owner, string detourName)
        {
            if (target == null)
            {
                ModLog.Warn($"Hook target for '{detourName}' was not found; the related feature is disabled");
                return null;
            }

            MethodInfo detour = owner.GetMethod(
                detourName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (detour == null)
            {
                ModLog.Error($"Detour '{detourName}' was not found on {owner.Name}");
                return null;
            }

            return detour;
        }

        private static void OnItemChangerHook()
        {
            OnItemChangerUnhook();
            foreach (var (target, detour) in AfterItemChanger)
            {
                ActiveIcHooks.Add(new Hook(target, detour));
            }
        }

        private static void OnItemChangerUnhook()
        {
            foreach (Hook hook in ActiveIcHooks)
            {
                hook.Dispose();
            }
            ActiveIcHooks.Clear();
        }
    }
}