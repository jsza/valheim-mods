using System;

using HarmonyLib;

using BA = BetterArchery.BetterArchery;


namespace BetterArcheryEAQSFix
{
    // protected override void Awake()
    [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
    public static class Player_Awake_Patch
    {
        public static void Postfix()
        {
            BetterArcheryState.UpdateRowIndex();
        }
    }

    // public void Load(ZPackage pkg)
    [HarmonyPatch(typeof(Player), nameof(Player.Load))]
    public static class Player_Load_Patch
    {
        public static bool loading = false;

        public static void Prefix()
        {
            loading = true;
        }

        public static void Postfix(Player __instance)
        {
            if (!BetterArcheryState.QuiverEnabled)
            {
                return;
            }

            loading = false;

            // Avoid trying to drop items at character select screen
            if (__instance != Player.m_localPlayer)
            {
                return;
            }

            Inventory inventory = __instance.m_inventory;
            Plugin.logger.LogInfo($"Searching player inventory for lost items.");
            for (int i = inventory.m_inventory.Count - 1; i >= 0; i--)
            {
                ItemDrop.ItemData itemData = inventory.m_inventory[i];
                Vector2i pos = itemData.m_gridPos;
                if (
                    pos.y >= BetterArcheryState.RowStartIndex
                    && pos.y <= BetterArcheryState.RowEndIndex
                    && !(BA.IsQuiverSlot(pos) && itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo)
                )
                {
                    Plugin.logger.LogWarning($"Found {itemData.m_shared.m_name} x {itemData.m_stack} in Better Archery slots; attempting to drop.");
                    __instance.DropItem(inventory, itemData, itemData.m_stack);
                }
            }
        }
    }

    // private void OnTakeAll()
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTakeAll))]
    public static class InventoryGui_OnTakeAll_Patch
    {
        public static bool takingAllTombstone = false;

        public static void Prefix(InventoryGui __instance)
        {
            Container container = __instance.m_currentContainer;
            if (container != null && container.GetComponent<TombStone>())
            {
                takingAllTombstone = true;
            }
        }

        public static void Postfix()
        {
            takingAllTombstone = false;
        }
    }

    // public bool Interact(Humanoid character, bool hold)
    [HarmonyPatch(typeof(TombStone), nameof(TombStone.Interact))]
    public static class TombStone_Interact_Patch
    {
        public static bool interactingTombstone = false;

        public static void Prefix()
        {
            interactingTombstone = true;
        }

        public static void Postfix()
        {
            interactingTombstone = false;
        }
    }

    // public int GetEmptySlots()
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEmptySlots))]
    public static class Inventory_GetEmptySlots_Patch
    {
        public static void Postfix(Inventory __instance, ref int __result)
        {
            if (!BetterArcheryState.QuiverEnabled)
            {
                return;
            }

            if (TombStone_Interact_Patch.interactingTombstone && __instance.GetName() == "Inventory")
            {
                // Better Archery adds 2 (hidden from UI) rows whose slots we need to ignore
                // Prevents trying to move items to Better Archery's hidden inventory when interacting with tombstone
                // (TombStone.EasyFitInInventory calls GetEmptySlots)
                int subtractSlots = __instance.m_width * BetterArcheryState.RowCount;
                __result = Math.Max(0, __result - subtractSlots);
                Plugin.logger.LogWarning($"GetEmptySlots: subtracted {subtractSlots} BetterArchery slots (now {__result})");
            }
        }
    }

    // private bool AddItem(ItemDrop.ItemData item, int amount, int x, int y)
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) })]
    public static class Inventory_AddItem_Patch
    {
        public static bool Prefix(ItemDrop.ItemData item, int amount, int x, int y, ref bool __result)
        {
            if (!BetterArcheryState.QuiverEnabled)
            {
                return true;
            }

            if (InventoryGui_OnTakeAll_Patch.takingAllTombstone || TombStone_Interact_Patch.interactingTombstone)
            {
                if (y >= BetterArcheryState.RowStartIndex && y <= BetterArcheryState.RowEndIndex)
                {
                    Plugin.logger.LogWarning($"Blocking Inventory.Additem {item.m_shared.m_name} x {amount} into Better Archery slot {x},{y}.");
                    __result = false;
                    return false;
                }
            }
            return true;
        }
    }
}
