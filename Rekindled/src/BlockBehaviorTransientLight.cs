using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Rekindled.src
{
    class BlockBehaviorTransientLight : BlockBehavior, ITransientTickable
    {
        ICoreServerAPI sapi;

        // blockbehaviors are non-stateful, so just used to temporarily store data
        public TransientLightProps Props;
        public TransientLightState State;

        public BlockBehaviorTransientLight(Block block) : base(block)
        {
            if (block.Attributes == null)
                return;
            if (block.Attributes["transientLightProps"].Exists != true)
                return;

            Props = block.Attributes["transientLightProps"].AsObject<TransientLightProps>();
            if (Props == null)
                return;

            State = new TransientLightState(Props);

            if (!Enum.TryParse(block.Variant["state"], true, out EnumLightState lightState))
                return;
            State.LightState = lightState;

            RekindledMain.LightSources.Add((ITransientTickable) this);
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Server)
                sapi = api as ICoreServerAPI;
        }


        public void TryBlockTransition(EnumLightState toLightState, ItemSlot slot)
        {
            AssetLocation blockCode = block.CodeWithVariant("state", Enum.GetName(toLightState).ToLower());

            Block toBlock = sapi.World.GetBlock(blockCode);
            if (toBlock == null)
                return;

            ItemStack newStack = new(toBlock, slot.StackSize);
            slot.Itemstack = newStack;
            slot.MarkDirty();
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


        public override string GetHeldBlockInfo(IWorldAccessor world, ItemSlot inSlot)
        {
            if (State == null)
                return base.GetHeldBlockInfo(world, inSlot);
            return base.GetHeldBlockInfo(world, inSlot) + 
                "\nState: " + block.Variant["state"] + "\nFuel Hours Remaining: " + State.CurrentFuelHours + "\nCurrent Depletion Multiplier: x" + State.CurrentDepletionMul;
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            if (State == null)
                return base.GetPlacedBlockInfo(world, pos, forPlayer);
            return base.GetPlacedBlockInfo(world, pos, forPlayer) + 
                "\nState: " + block.Variant["state"] + "\nFuel Hours Remaining: " + State.CurrentFuelHours + "\nCurrent Depletion Multiplier: x" + State.CurrentDepletionMul;
        }


        public void OnGameTick(float deltaTime)
        {
            if (State == null)
                return;

            State.CurrentFuelHours -= (float)(sapi.World.Calendar.ElapsedHours - State.TimeLastChecked);
            State.TimeLastChecked = (float)sapi.World.Calendar.ElapsedHours;
        }
    }
}
