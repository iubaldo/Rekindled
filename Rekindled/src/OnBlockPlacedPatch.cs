using HarmonyLib;

using Vintagestory.GameContent;
using Vintagestory.API.Common;

namespace Rekindled.src
{
    [HarmonyPatch]
    public class OnBlockPlacedPatch
    {
        // to whoever's out there, please stop overriding methods without calling behaviors or base I beg of you
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockEntityTransient), "OnBlockPlaced")]
        public static void PrefixOnBlockPlaced(BlockEntityTransient __instance, ItemStack byItemStack)
        {
            if (byItemStack == null) // placed by worldgen/already exists, just load treeattributes instead
            {
                RekindledMain.sapi.Logger.Notification("byItemStack was null");
                return;
            }
            else
                RekindledMain.sapi.Logger.Notification("not null");

            if (!RekindledMain.IsBlockTransientLight(byItemStack.Block))
                return;

            foreach (var val in __instance.Behaviors)
            {
                val.OnBlockPlaced(byItemStack);
            }
        }
    }
}
