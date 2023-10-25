using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Text;

using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Rekindled.src
{
    [HarmonyPatch]
    public class OnPickBlockPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockLantern), "OnPickBlock")]
        public static bool PrefixOnPickBlock(BlockLantern __instance, ref ItemStack __result, IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack;
            if (__instance.HasBehavior<BlockBehaviorTransientLight>())
            {
                var behavior = __instance.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
                EnumHandling enumHandling = EnumHandling.PassThrough;
                stack = behavior.OnPickBlock(world, pos, ref enumHandling);
            }
            else 
                stack = new ItemStack(world.GetBlock(__instance.CodeWithParts("up")));

            BELantern be = world.BlockAccessor.GetBlockEntity(pos) as BELantern;
            if (be != null)
            {
                stack.Attributes.SetString("material", be.material);
                stack.Attributes.SetString("lining", be.lining);
                stack.Attributes.SetString("glass", be.glass);
            }
            else
            {
                stack.Attributes.SetString("material", "copper");
                stack.Attributes.SetString("lining", "plain");
                stack.Attributes.SetString("glass", "plain");
            }

            __result = stack;

            return false; // skip original method
        }
    }
}