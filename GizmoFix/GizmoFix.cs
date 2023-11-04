using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Bootstrap;

using HarmonyLib;
using UnityEngine;
using MonoMod.Cil;
using OC = Mono.Cecil.Cil.OpCodes;
using HarmonyLib.Tools;


namespace GizmoFix
{
    public class GizmoMetadata
    {
        public const string gizmoPluginName = "com.rolopogo.Gizmo";
        public const string gizmoPluginVersion = "1.0.0";

        public const string gizmoReloadedPluginName = "m3to.mods.GizmoReloaded";
        public const string gizmoReloadedPluginVersion = "1.1.5";
    }

    [BepInPlugin("MVP.GizmoFix", "GizmoFix", "1.0.0")]
    // Ensure we load after Gizmo / Gizmo Reloaded
    [BepInDependency(GizmoMetadata.gizmoPluginName, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(GizmoMetadata.gizmoReloadedPluginName, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource logger;
        public static Plugin instance;
        public bool gizmoActive = false;
        public bool gizmoReloadedActive = false;

        private void Awake()
        {
            logger = this.Logger;
            instance = this;

            foreach (var item in Chainloader.PluginInfos)
            {
                string version = item.Value.Metadata.Version.ToString();
                if (item.Key == GizmoMetadata.gizmoPluginName
                    && version == GizmoMetadata.gizmoPluginVersion)
                {
                    gizmoActive = true;
                    logger.LogInfo($"Detected Gizmo version {version}");
                }
                else if (item.Key == GizmoMetadata.gizmoReloadedPluginName
                    && version == GizmoMetadata.gizmoReloadedPluginVersion)
                {
                    gizmoReloadedActive = true;
                    logger.LogInfo($"Detected Gizmo Reloaded version {version}");
                }
            }

            if (!gizmoActive && !gizmoReloadedActive)
            {
                logger.LogInfo($"No supported versions of Gizmo found; doing nothing.");
                return;
            }
            
            Harmony harmony = Harmony.CreateAndPatchAll(typeof(Patches));
            if (gizmoReloadedActive)
            {
                harmony.PatchAll(typeof(GizmoReloadedPatches));
            }
        }
    }

    public class Patches
    {
        [HarmonyILManipulator]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Transpile_Player_UpdatePlacementGhost(ILContext il)
        {
            if (Plugin.instance.gizmoActive)
            {
                PatchGizmo(il);
            }
            else if (Plugin.instance.gizmoReloadedActive)
            {
                PatchGizmoReloaded(il);
            }
        }

        private static void PatchGizmo(ILContext il)
        {
            Plugin.logger.LogInfo("Trying to patch original Gizmo...");
            GenericPatch(il, AccessTools.Method(typeof(Gizmo.Plugin), "GetPlacementAngle"));
        }

        private static void PatchGizmoReloaded(ILContext il)
        {
            Plugin.logger.LogInfo("Trying to patch Gizmo Reloaded...");
            GenericPatch(il, AccessTools.Method(typeof(GizmoReloaded.Plugin), "GetPlacementAngle"));
        }

        private static void GenericPatch(ILContext il, MethodInfo method)
        {
            bool alreadyPatched = new ILCursor(il).TryGotoNext(
                i => i.Match(OC.Stfld),
                i => i.Match(OC.Ldc_R4),
                i => i.Match(OC.Ldarg_0),
                i => i.Match(OC.Ldfld),
                i => i.Match(OC.Ldarg_0),
                i => i.Match(OC.Ldfld),
                i => i.Match(OC.Conv_R4),
                i => i.Match(OC.Mul),
                i => i.Match(OC.Ldc_R4),
                i => i.MatchCall(method)
            );
            if (alreadyPatched)
            {
                Plugin.logger.LogWarning("Already patched; doing nothing.");
                return;
            }
            new ILCursor(il)
                .GotoNext(MoveType.After,
                    i => i.Match(OC.Stfld),
                    i => i.Match(OC.Ldc_R4),
                    i => i.Match(OC.Ldarg_0),
                    i => i.Match(OC.Ldfld),
                    i => i.Match(OC.Ldarg_0),
                    i => i.Match(OC.Ldfld),
                    i => i.Match(OC.Conv_R4),
                    i => i.Match(OC.Mul),
                    i => i.Match(OC.Ldc_R4)
                )
                .GotoNext(MoveType.Before,
                    i => i.MatchCall<Quaternion>(nameof(Quaternion.Euler))
                )
                .Remove()
                .Emit(OC.Call, method)
            ;
            Plugin.logger.LogInfo("Patched successfully.");
        }
    }

    public class GizmoReloadedPatches
    {
        [HarmonyILManipulator]
        [HarmonyPatch(typeof(GizmoReloaded.Plugin), "UpdatePlacement")]
        private static void Transpile_GizmoReloaded_Plugin_UpdatePlacement(ILContext il)
        /* Fix private method access error for Humanoid.GetRightItem()
         */
        {
            new ILCursor(il)
                .GotoNext(MoveType.Before,
                    i => i.MatchCallvirt<Humanoid>("GetRightItem")
                )
                .Remove()
                .Emit(OC.Callvirt, AccessTools.Method(typeof(Humanoid), "GetRightItem"))
            ;
        }
    }
}
