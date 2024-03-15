using System;
using System.Text;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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
        // blockbehaviors are non-stateful, so just used to temporarily store data -> store the actual data in itemstack.attributes
        public TransientLightProps Props;
        public TransientLightState State;


        public BlockBehaviorTransientLight(Block block) : base(block)
        {
            Props = RekindledMain.ResolvePropsFromBlock(block);
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


        public void TryBlockTransition(EnumLightState toLightState)
        {

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


        BEBehaviorTransientLight GetBEBehavior(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (block.EntityClass != be.Block.EntityClass)
            {
                RekindledMain.sapi.Logger.Notification("entityclass mismatch");
                return null;
            }


            var behavior = be.GetBehavior<BEBehaviorTransientLight>();
            if (behavior == null || behavior.State == null)
            {
                RekindledMain.sapi.Logger.Notification("behavior/state null");
                return null;
            }

            return behavior;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
        {
            var behavior = GetBEBehavior(world, pos);
            if (behavior == null)
                return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);

            State = behavior.State;

            Block dropBlock;
            switch (block.Code.FirstCodePart()) 
            {
                case "torch":
                    dropBlock = world.BlockAccessor.GetBlock(block.CodeWithVariant("orientation", "up"));
                    break;
                default:
                    dropBlock = block; 
                    break;
            }

            ItemStack itemStack = new(dropBlock);
            
            // TODO: create an initialize function for this?
            if (itemStack.Attributes == null)
                itemStack.Attributes = new TreeAttribute();

            if (!itemStack.Attributes.HasAttribute(TransientUtil.ATTR_TRANSIENTSTATE))
                itemStack.Attributes[TransientUtil.ATTR_TRANSIENTSTATE] = new TreeAttribute();

            ITreeAttribute attr = (ITreeAttribute)itemStack.Attributes[TransientUtil.ATTR_TRANSIENTSTATE];

            if (!attr.HasAttribute(TransientUtil.ATTR_CREATED_HOURS))
                attr.SetDouble(TransientUtil.ATTR_CREATED_HOURS, State.CreatedTotalHours);

            attr.SetInt(TransientUtil.ATTR_TRANSIENTSTATE, (int)State.LightState);
            attr.SetDouble(TransientUtil.ATTR_CURR_HOURS, State.CurrentFuelHours);
            attr.SetDouble(TransientUtil.ATTR_CURR_DEPLETION, State.CurrentDepletionMul);
            attr.SetDouble(TransientUtil.ATTR_UPDATED_HOURS, State.LastUpdatedTotalHours);
            
            handling = EnumHandling.PreventSubsequent;

            return new ItemStack[]{itemStack};
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            BEBehaviorTransientLight bebehavior = GetBEBehavior(world, pos);
            if (bebehavior == null)
            {
                handling = EnumHandling.PassThrough;
                return base.OnPickBlock(world, pos, ref handling);
            }

            TransientLightState beState = bebehavior.State;

            if (beState == null)
            {
                handling = EnumHandling.PassThrough;
                return base.OnPickBlock(world, pos, ref handling);
            }
          
            ItemStack itemStack = new ItemStack(block);
            if (itemStack.Attributes == null)
                itemStack.Attributes = new TreeAttribute();

            if (!itemStack.Attributes.HasAttribute(TransientUtil.ATTR_TRANSIENTSTATE))
                itemStack.Attributes[TransientUtil.ATTR_TRANSIENTSTATE] = new TreeAttribute();

            ITreeAttribute attr = (ITreeAttribute)itemStack.Attributes[TransientUtil.ATTR_TRANSIENTSTATE];

            if (!attr.HasAttribute(TransientUtil.ATTR_CREATED_HOURS))
                attr.SetDouble(TransientUtil.ATTR_CREATED_HOURS, beState.CreatedTotalHours);

            attr.SetInt(TransientUtil.ATTR_TRANSIENTSTATE, (int)beState.LightState);
            attr.SetDouble(TransientUtil.ATTR_CURR_HOURS, beState.CurrentFuelHours);
            attr.SetDouble(TransientUtil.ATTR_CURR_DEPLETION, beState.CurrentDepletionMul);
            attr.SetDouble(TransientUtil.ATTR_UPDATED_HOURS, beState.LastUpdatedTotalHours);
            
            handling = EnumHandling.PreventSubsequent;
            return itemStack;
        }


        // Extinguish light source if hand is empty and sneaking
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            RekindledMain.sapi.Logger.Notification("started interact");
            handling = EnumHandling.Handled;

            if (byPlayer.InventoryManager.ActiveHotbarSlot != null 
                && (!byPlayer.InventoryManager.ActiveHotbarSlot.Empty || !byPlayer.Entity.Controls.CtrlKey))
                return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);

            BEBehaviorTransientLight bebehavior = GetBEBehavior(world, blockSel.Position);
            if (bebehavior == null)
                return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);

            handling = EnumHandling.PreventDefault;
            RekindledMain.sapi.Logger.Notification("attempting extinguish");
            bebehavior.TryTransition(EnumLightState.Extinct);

            return true;
        }


        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            RekindledMain.sapi.Logger.Notification("interact step");
            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel, ref handling);
        }
    }
}
