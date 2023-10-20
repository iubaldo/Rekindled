﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Rekindled.src
{
    // TODO: make this inherit from a generic transient light class to allow for item transient lights as well


    public class CollectibleBehaviorTLDescription : CollectibleBehavior
    {
        public CollectibleBehaviorTLDescription(CollectibleObject collObj) : base(collObj) { }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (!inSlot.Itemstack.Attributes.HasAttribute("transientState"))
                return;

            var props = inSlot.Itemstack.Block.Attributes["transientLightProps"].AsObject<TransientLightProps>();
            ITreeAttribute attr = (ITreeAttribute)inSlot.Itemstack.Attributes["transientState"];

            dsc.AppendLine("\nState: " + ((EnumLightState)attr.GetInt("currentLightState")).GetName() +
                    "\nFuel remaining: " + Math.Round(attr.GetDouble("currentFuelHours") / props.MaxFuelHours * 100, 2) + "%" + 
                    "\nDepletion multiplier: x" + Math.Round(attr.GetDouble("currentDepletionMul"), 2));
        }
    }


    public class BlockBehaviorTransientLight : BlockBehavior
    {
        // blockbehaviors are non-stateful, so just used to temporarily store data
        public TransientLightProps Props;


        public BlockBehaviorTransientLight(Block block) : base(block)
        {
            if (block.Attributes == null)
                return;
            if (!block.Attributes["transientLightProps"].Exists)
                return;

            Props = block.Attributes["transientLightProps"].AsObject<TransientLightProps>();
        }


        public void TryBlockTransition(EnumLightState toLightState, ItemSlot slot)
        {
            AssetLocation blockCode = block.CodeWithVariant("state", Enum.GetName(toLightState).ToLower());

            Block toBlock = RekindledMain.sapi.World.GetBlock(blockCode);
            if (toBlock == null)
                return;

            ItemStack newStack = new(toBlock, slot.StackSize);
            slot.Itemstack = newStack;
            slot.MarkDirty();
        }


        // transitions the transient light stack to the given toState
        // TODO: make sure props/state update correctly to match the new block
        public ItemStack OnTransitionStack(ItemSlot slot, EnumLightState toLightState)
        {
            if (!slot.Itemstack.Collectible.HasBehavior(typeof(BlockBehaviorTransientLight)))
                return null;

            string toState = toLightState.GetName().ToLower();

            AssetLocation blockCode = block.CodeWithVariant("state", toState);

            Block toBlock = RekindledMain.sapi.World.GetBlock(blockCode);
            if (toBlock == null)
                return null;

            ItemStack newStack = 
                new ItemStack(toBlock.Id, toBlock.ItemClass, slot.Itemstack.StackSize, slot.Itemstack.Attributes as TreeAttribute, RekindledMain.sapi.World);

            RekindledMain.sapi.Logger.Notification("Attempting to transition block \"" + block.Code + "\" to variant \"" + newStack.Block.Code + "\"");

            return newStack;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
        {
            List<ItemStack> toDrop = new();

            for (int i = 0; i < block.Drops.Length; i++)
            {
                BlockDropItemStack dropStack = block.Drops[i];
                if (dropStack.Tool != null && (byPlayer == null || dropStack.Tool != byPlayer.InventoryManager.ActiveTool)) 
                    continue;

                float extraMul = 1f;
                if (dropStack.DropModbyStat != null)
                    extraMul = byPlayer.Entity.Stats.GetBlended(dropStack.DropModbyStat); // If the stat does not exist, then GetBlended returns 1 \o/

                ItemStack stack = block.Drops[i].GetNextItemStack(extraMul); // * dropQuantityMul -> not sure if this is needed since light sources don't drop multiple
                if (stack == null) 
                    continue;

                toDrop.Add(stack);
                if (block.Drops[i].LastDrop) 
                    break;
            }

            if (block.BlockEntityBehaviors.OfType<BEBehaviorTransientLight>().Any())
            {
                // transfer attributes to toDrop itemStack
                // might move this to the BEBehavior, since Block doesn't have access to BlockEntity, but the reverse is true
                // or modify block.Drops (which is a BlockDropItemStack)
            }

            handling = EnumHandling.PreventDefault; // want to fully override base function, but still allow other behaviors to work
            return toDrop.ToArray();
        }


        //public override string GetHeldBlockInfo(IWorldAccessor world, ItemSlot inSlot)
        //{
        //    if (!inSlot.Itemstack.Attributes.HasAttribute("transientState"))
        //        return base.GetHeldBlockInfo(world, inSlot);

        //    ITreeAttribute attr = (ITreeAttribute)inSlot.Itemstack.Attributes["transientState"];
        //    return "\nState: " + ((EnumLightState)attr.GetInt("currentLightState")).GetName() + 
        //            "\nFuel Hours Remaining: " + Math.Round(attr.GetDouble("currentFuelHours"), 2) + 
        //            "\nCurrent Depletion Multiplier: x" + Math.Round(attr.GetDouble("currentDepletionMul"), 2);
        //}

        //public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        //{
        //    base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        //    //if (!inSlot.Itemstack.Attributes.HasAttribute("transientState"))
        //    //    return;

        //    ITreeAttribute attr = (ITreeAttribute)inSlot.Itemstack.Attributes["transientState"];
        //    dsc.Append("\nState: " + ((EnumLightState)attr.GetInt("currentLightState")).GetName() +
        //            "\nFuel Hours Remaining: " + Math.Round(attr.GetDouble("currentFuelHours"), 2) +
        //            "\nCurrent Depletion Multiplier: x" + Math.Round(attr.GetDouble("currentDepletionMul"), 2));
        //}
    }
}
