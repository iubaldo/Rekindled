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
    internal class RekindledHarmonyPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CollectibleObject), "UpdateAndGetTransitionStatesNative")]
        public static void PrefixUpdateAndGetTransitionStatesNative(IWorldAccessor world, ItemSlot inslot)
        {
            RekindledMain.UpdateAndGetTransientState(world, inslot);
        }


        // TODO: figure out why model doesn't update when itemstack transitions
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockTorch), "OnGroundIdle")]
        public static void PostfixOnGroundIdle(EntityItem entityItem)
        {
            RekindledMain.UpdateAndGetTransientState(entityItem.World, entityItem.Slot);
        }


        // Should prevent the "Burns for {0} hours when placed." line from being printed in the mouseover text
        // Since we can't call base.GetHeldItemInfo with harmony patches, just copy-paste the entire base method lmao
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockTorch), "GetHeldItemInfo")]
        public static bool PrefixGetHeldItemInfo(BlockTorch __instance, ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            // Block.GetHeldItemInfo

            ItemStack itemstack = inSlot.Itemstack;
            if (__instance.DrawType == EnumDrawType.SurfaceLayer)
            {
                dsc.AppendLine(Lang.Get("Decor layer block"));
            }

            dsc.Append(Lang.Get("Material: ") + Lang.Get("blockmaterial-" + __instance.GetBlockMaterial(world.BlockAccessor, null, itemstack)) + "\n");
            __instance.AddExtraHeldItemInfoPostMaterial(inSlot, dsc, world);
            byte[] lightHsv = __instance.GetLightHsv(world.BlockAccessor, null, itemstack);
            dsc.Append((!withDebugInfo) ? "" : ((lightHsv[2] > 0) ? (Lang.Get("light-hsv") + lightHsv[0] + ", " + lightHsv[1] + ", " + lightHsv[2] + "\n") : ""));
            dsc.Append(withDebugInfo ? "" : ((lightHsv[2] > 0) ? (Lang.Get("light-level") + lightHsv[2] + "\n") : ""));
            if (__instance.WalkSpeedMultiplier != 1f)
            {
                dsc.Append(Lang.Get("walk-multiplier") + __instance.WalkSpeedMultiplier + "\n");
            }

            BlockBehavior[] blockBehaviors = __instance.BlockBehaviors;
            foreach (BlockBehavior blockBehavior in blockBehaviors)
            {
                dsc.Append(blockBehavior.GetHeldBlockInfo(world, inSlot));
            }


            // CollectibleObject.GetHeldItemInfo

            itemstack = inSlot.Itemstack;
            string text = __instance.Code?.Domain + ":" + __instance.ItemClass.ToString().ToLowerInvariant() + "desc-" + __instance.Code?.Path;
            string matching = Lang.GetMatching(text);
            matching = ((!(matching == text)) ? (matching + "\n") : "");
            if (withDebugInfo)
            {
                dsc.AppendLine("<font color=\"#bbbbbb\">Id:" + __instance.Id + "</font>");
                dsc.AppendLine("<font color=\"#bbbbbb\">Code: " + __instance.Code?.ToString() + "</font>");
                ICoreAPI coreAPI = inSlot.Inventory.Api;
                if (coreAPI != null && coreAPI.Side == EnumAppSide.Client && (inSlot.Inventory.Api as ICoreClientAPI).Input.KeyboardKeyStateRaw[1])
                {
                    dsc.AppendLine("<font color=\"#bbbbbb\">Attributes: " + inSlot.Itemstack.Attributes.ToJsonToken() + "</font>\n");
                }
            }

            int maxDurability = __instance.GetMaxDurability(itemstack);
            if (maxDurability > 1)
            {
                dsc.AppendLine(Lang.Get("Durability: {0} / {1}", itemstack.Collectible.GetRemainingDurability(itemstack), maxDurability));
            }

            if (__instance.MiningSpeed != null && __instance.MiningSpeed.Count > 0)
            {
                dsc.AppendLine(Lang.Get("Tool Tier: {0}", __instance.ToolTier));
                dsc.Append(Lang.Get("item-tooltip-miningspeed"));
                int num = 0;
                foreach (KeyValuePair<EnumBlockMaterial, float> item in __instance.MiningSpeed)
                {
                    if (!((double)item.Value < 1.1))
                    {
                        if (num > 0)
                        {
                            dsc.Append(", ");
                        }

                        dsc.Append(Lang.Get(item.Key.ToString()) + " " + item.Value.ToString("#.#") + "x");
                        num++;
                    }
                }

                dsc.Append("\n");
            }

            if (CollectibleObject.IsBackPack(itemstack))
            {
                dsc.AppendLine(Lang.Get("Storage Slots: {0}", CollectibleObject.QuantityBackPackSlots(itemstack)));
                ITreeAttribute treeAttribute = itemstack.Attributes.GetTreeAttribute("backpack");
                if (treeAttribute != null)
                {
                    bool flag = false;
                    foreach (KeyValuePair<string, IAttribute> item2 in treeAttribute.GetTreeAttribute("slots"))
                    {
                        ItemStack itemStack = (ItemStack)(item2.Value?.GetValue());
                        if (itemStack != null && itemStack.StackSize > 0)
                        {
                            if (!flag)
                            {
                                dsc.AppendLine(Lang.Get("Contents: "));
                                flag = true;
                            }

                            itemStack.ResolveBlockOrItem(world);
                            dsc.AppendLine("- " + itemStack.StackSize + "x " + itemStack.GetName());
                        }
                    }

                    if (!flag)
                    {
                        dsc.AppendLine(Lang.Get("Empty"));
                    }
                }
            }

            EntityPlayer entityPlayer = ((world.Side == EnumAppSide.Client) ? (world as IClientWorldAccessor).Player.Entity : null);
            float spoilState = __instance.AppendPerishableInfoText(inSlot, dsc, world);
            FoodNutritionProperties nutritionProperties = __instance.GetNutritionProperties(world, itemstack, entityPlayer);
            if (nutritionProperties != null)
            {
                float num2 = GlobalConstants.FoodSpoilageSatLossMul(spoilState, itemstack, entityPlayer);
                float num3 = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, itemstack, entityPlayer);
                if (Math.Abs(nutritionProperties.Health * num3) > 0.001f)
                {
                    dsc.AppendLine(Lang.Get("When eaten: {0} sat, {1} hp", Math.Round(nutritionProperties.Satiety * num2), nutritionProperties.Health * num3));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("When eaten: {0} sat", Math.Round(nutritionProperties.Satiety * num2)));
                }

                dsc.AppendLine(Lang.Get("Food Category: {0}", Lang.Get("foodcategory-" + nutritionProperties.FoodCategory.ToString().ToLowerInvariant())));
            }

            if (__instance.GrindingProps != null)
            {
                dsc.AppendLine(Lang.Get("When ground: Turns into {0}x {1}", __instance.GrindingProps.GroundStack.ResolvedItemstack.StackSize, __instance.GrindingProps.GroundStack.ResolvedItemstack.GetName()));
            }

            if (__instance.CrushingProps != null)
            {
                float num4 = __instance.CrushingProps.Quantity.avg * (float)__instance.CrushingProps.CrushedStack.ResolvedItemstack.StackSize;
                dsc.AppendLine(Lang.Get("When pulverized: Turns into {0:0.#}x {1}", num4, __instance.CrushingProps.CrushedStack.ResolvedItemstack.GetName()));
                dsc.AppendLine(Lang.Get("Requires Pulverizer tier: {0}", __instance.CrushingProps.HardnessTier));
            }

            if (__instance.GetAttackPower(itemstack) > 0.5f)
            {
                dsc.AppendLine(Lang.Get("Attack power: -{0} hp", __instance.GetAttackPower(itemstack).ToString("0.#")));
                dsc.AppendLine(Lang.Get("Attack tier: {0}", __instance.ToolTier));
            }

            if (__instance.GetAttackRange(itemstack) > GlobalConstants.DefaultAttackRange)
            {
                dsc.AppendLine(Lang.Get("Attack range: {0} m", __instance.GetAttackRange(itemstack).ToString("0.#")));
            }

            if (__instance.CombustibleProps != null)
            {
                string text2 = __instance.CombustibleProps.SmeltingType.ToString().ToLowerInvariant();
                if (text2 == "fire")
                {
                    dsc.AppendLine(Lang.Get("itemdesc-fireinkiln"));
                }
                else
                {
                    if (__instance.CombustibleProps.BurnTemperature > 0)
                    {
                        dsc.AppendLine(Lang.Get("Burn temperature: {0}°C", __instance.CombustibleProps.BurnTemperature));
                        dsc.AppendLine(Lang.Get("Burn duration: {0}s", __instance.CombustibleProps.BurnDuration));
                    }

                    if (__instance.CombustibleProps.MeltingPoint > 0)
                    {
                        dsc.AppendLine(Lang.Get("game:smeltpoint-" + text2, __instance.CombustibleProps.MeltingPoint));
                    }
                }

                if (__instance.CombustibleProps.SmeltedStack?.ResolvedItemstack != null)
                {
                    int smeltedRatio = __instance.CombustibleProps.SmeltedRatio;
                    int stackSize = __instance.CombustibleProps.SmeltedStack.ResolvedItemstack.StackSize;
                    string value = ((smeltedRatio == 1) ? Lang.Get("game:smeltdesc-" + text2 + "-singular", stackSize, __instance.CombustibleProps.SmeltedStack.ResolvedItemstack.GetName()) : Lang.Get("game:smeltdesc-" + text2 + "-plural", smeltedRatio, stackSize, __instance.CombustibleProps.SmeltedStack.ResolvedItemstack.GetName()));
                    dsc.AppendLine(value);
                }
            }

            CollectibleBehavior[] collectibleBehaviors = __instance.CollectibleBehaviors;
            for (int i = 0; i < collectibleBehaviors.Length; i++)
            {
                collectibleBehaviors[i].GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            }

            if (matching.Length > 0 && dsc.Length > 0)
            {
                dsc.Append("\n");
            }

            dsc.Append(matching);
            float temperature = __instance.GetTemperature(world, itemstack);
            if (temperature > 20f)
            {
                dsc.AppendLine(Lang.Get("Temperature: {0}°C", (int)temperature));
            }

            if (__instance.Code != null && __instance.Code.Domain != "game")
            {
                Mod mod = inSlot.Inventory.Api.ModLoader.GetMod(__instance.Code.Domain);
                dsc.AppendLine(Lang.Get("Mod: {0}", mod?.Info.Name ?? __instance.Code.Domain));
            }


            return false; // prevent original method from executing
        }


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


        // base doesn't call behavior methods
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BELantern), "GetBlockInfo")]
        public static void PostfixGetBlockInfo(BELantern __instance, IPlayer forPlayer, StringBuilder sb)
        {
            foreach (var val in __instance.Behaviors)
            {
                val.GetBlockInfo(forPlayer, sb);
            }
        }


        // base doesn't call behavior methods
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockEntityTransient), "OnBlockPlaced")]
        public static void PrefixOnBlockPlaced(BlockEntityTransient __instance, ItemStack byItemStack)
        {
            //if (byItemStack == null) // placed by worldgen/already exists, just load treeattributes instead
            //{
            //    RekindledMain.sapi.Logger.Notification("byItemStack was null");
            //    return;
            //}

            foreach (var val in __instance.Behaviors)
            {
                val.OnBlockPlaced(byItemStack);
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockGroundAndSideAttachable), "TryPlaceBlock")]
        public static void PostfixTryPlaceBlock(BlockGroundAndSideAttachable __instance, ref bool __result, IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!__result) // block wasn't successfully placed
                return;

            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (__instance.EntityClass == null)
                return;

            if (__instance.EntityClass == be.Block.EntityClass)
            {
                var behavior = be.GetBehavior<BEBehaviorTransientLight>();
                if (behavior != null)
                    behavior.SetFromItemStack(itemstack);
            }
        }
    }
}
