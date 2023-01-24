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
    class BBehaviorTransferAttributesOnDrop : BlockBehavior
    {
        float dropQuantityMultiplier = 1;

        public BBehaviorTransferAttributesOnDrop(Block block) : base(block) { }

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
                // might move this to the BEBehavior, since Block doesn't have access to BlockEntity, but the reverse it true
            }

            handling = EnumHandling.PreventDefault; // want to fully override base function
            return toDrop.ToArray();
        }

        public override string GetHeldBlockInfo(IWorldAccessor world, ItemSlot inSlot)
        {
            return base.GetHeldBlockInfo(world, inSlot);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }
    }
}
