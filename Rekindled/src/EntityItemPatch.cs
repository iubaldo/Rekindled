using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.GameContent;


namespace Rekindled.src
{
    [HarmonyPatch]
    public class InfoPanelPatch
    {
        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(EntityItem), "OnGameTick")]
        //public static void PrefixOnGameTick(EntityItem __instance)
        //{
        //    ItemStack transitionStack = ItemSlotTickPatch.UpdateTransientState(__instance.World, __instance.Slot);
        //    //if (transitionStack != null)
        //    //{
        //    //    RekindledMain.sapi.Logger.Notification("transition happens here entityitem");
        //    //    __instance.Itemstack = new ItemStack();
        //    //    __instance.Itemstack.SetFrom(transitionStack);
        //    //}
        //}


        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockTorch), "OnGroundIdle")]
        public static void PostfixOnGroundIdle(EntityItem entityItem)
        {
            ItemSlotTickPatch.UpdateTransientState(entityItem.World, entityItem.Slot);
        }
    }
}
