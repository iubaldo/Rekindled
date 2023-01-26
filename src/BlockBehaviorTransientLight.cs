using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Util;

namespace rekindled.src
{
    // this behavior checks if the block has TransientLightProps, and transfers those props to the itemStack dropped when the block is broken
    class BlockBehaviorTransientLight : BlockBehavior
    {
        float dropQuantityMultiplier = 1;


        public BlockBehaviorTransientLight(Block block) : base(block) { }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
        {
            List<ItemStack> toDrop = new List<ItemStack>();

            for (int i = 0; i < block.Drops.Length; i++)
            {
                BlockDropItemStack dstack = block.Drops[i];
                if (dstack.Tool != null && (byPlayer == null || dstack.Tool != byPlayer.InventoryManager.ActiveTool)) continue;

                float extraMul = 1f;
                if (dstack.DropModbyStat != null)
                {
                    // If the stat does not exist, then GetBlended returns 1 \o/
                    extraMul = byPlayer.Entity.Stats.GetBlended(dstack.DropModbyStat);
                }

                ItemStack stack = block.Drops[i].GetNextItemStack(dropQuantityMultiplier * extraMul);
                if (stack == null) continue;

                toDrop.Add(stack);
                if (block.Drops[i].LastDrop) break;
            }

            if(Array.IndexOf(block.BlockEntityBehaviors, typeof(BEBehaviorTransientLight)) > -1)
            {
                // transfer attributes to toDrop itemStack
                // might move this to the BEBehavior, since Block doesn't have access to BlockEntity, but the reverse is true
                // or modify block.Drops (which is a BlockDropItemStack)
            }

            handling = EnumHandling.PreventDefault; // want to fully override base function
            return toDrop.ToArray();
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            ItemStack stack = new ItemStack(block);
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);

            handling = EnumHandling.PassThrough;
            foreach (BlockEntityBehavior behavior in be.Behaviors)
            {
                if (behavior is BEBehaviorTransientLight)
                {
                    stack.Attributes.SetFloat("timeLastChecked", behavior.properties["timeLastChecked"].AsFloat());
                    stack.Attributes.SetFloat("currentFuel", behavior.properties["currentFuel"].AsFloat());
                    stack.Attributes.SetFloat("currentDepletionMul", behavior.properties["currentFuel"].AsFloat());

                    handling = EnumHandling.PreventDefault; 
                }                        
            }
                      
            base.OnPickBlock(world, pos, ref handling); // will only prevent default if attributes successfully set
            return stack;
        }


        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handling)
        {


            base.OnBlockBroken(world, pos, byPlayer, ref handling);
        }


        public override string GetHeldBlockInfo(IWorldAccessor world, ItemSlot inSlot)
        {
            return base.GetHeldBlockInfo(world, inSlot);
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }


        /*
         * notes
         * look at BlockLantern for an example of using PickBlock to generate the itemStack made for OnBlockBroken
         * and then also remember to prevent default for OnBlockBroken
         */
    }
}
