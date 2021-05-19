using System.Reflection;
using BepInEx;
using BepInEx.Logging;

using HarmonyLib;


namespace BetterArcheryEAQSFix
{
    [BepInPlugin("MVP.BetterArcheryEAQSFix", "BetterArcheryEAQSFix", "0.0.1")]
    [BepInDependency("ishid4.mods.betterarchery")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource logger;

        private void Awake()
        {
            logger = this.Logger;
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }
    }

    public static class BetterArcheryState
    {
        public const int RowCount = 2;
        public static bool QuiverEnabled = false;
        public static int RowStartIndex = 0;
        public static int RowEndIndex = 0;

        public static void UpdateRowIndex()
        {
            int QuiverRowIndex = BetterArchery.BetterArchery.QuiverRowIndex;
            if (QuiverRowIndex > 0)
            {
                QuiverEnabled = true;
                RowStartIndex = QuiverRowIndex - 1;
                RowEndIndex = RowStartIndex + RowCount;
            }
            else
            {
                QuiverEnabled = false;
                RowStartIndex = 0;
                RowEndIndex = 0;
            }
        }
    }
}
