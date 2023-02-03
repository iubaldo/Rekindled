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
    // A custom version of BlockEntityTransient, specifically for light sources
    class BEBehaviorTransientLight : BlockEntityBehavior
    {
        public int checkIntervalMs = 2000; // every 2 seconds

        TransientLightProperties props;
        TransientLightState state;
        long listenerId; // ensure the GameTickListener is unique

        public BEBehaviorTransientLight(BlockEntity blockentity) : base(blockentity) { }


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (Blockentity.Block.Attributes == null) 
                return;
            if (Blockentity.Block.Attributes["transientLightProps"].Exists != true) 
                return;

            // Sanity check
            if (api.Side == EnumAppSide.Server)
            {
                var block = Api.World.BlockAccessor.GetBlock(Blockentity.Pos);
                if (block.Id != Blockentity.Block.Id)
                {
                    Api.World.Logger.Error("BEBehaviorTransientLight @{0} for Block {1}, but there is {2} at this position? Will delete BE", Blockentity.Pos, this.Blockentity.Block.Code.ToShortString(), block.Code.ToShortString());
                    // Can't delete during init
                    api.Event.EnqueueMainThreadTask(() => api.World.BlockAccessor.RemoveBlockEntity(Blockentity.Pos), "delete betransientlight");
                    return;
                }
            }

            props = Blockentity.Block.Attributes["transientLightProps"].AsObject<TransientLightProperties>();
            if (props == null) 
                return;
            state = new TransientLightState(props);              

            if (api.Side == EnumAppSide.Server)
            {
                if (listenerId != 0)
                {
                    throw new InvalidOperationException("Initializing BEBehaviorTransientLight twice would create a memory and performance leak");
                }
                listenerId = Blockentity.RegisterGameTickListener(CheckBETransition, checkIntervalMs);
            }
        }


        public void CheckBETransition(float deltaTime)
        {
            Block block = Api.World.BlockAccessor.GetBlock(Blockentity.Pos);
            if (block.Attributes == null)
            {
                Api.World.Logger.Error("BEBehaviorTransientLight @{0}: cannot find block attributes for {1}. Will stop transient timer", Blockentity.Pos, this.Blockentity.Block.Code.ToShortString());
                Blockentity.UnregisterGameTickListener(listenerId);
                return;
            }

            // In case this block was imported from another older world. In that case lastCheckAtTotalDays would be a future date.
            state.TimeLastChecked = (float) Math.Min(state.TimeLastChecked, Api.World.Calendar.TotalDays);

            float oneHour = 1f / Api.World.Calendar.HoursPerDay;
            while (Api.World.Calendar.TotalDays - state.TimeLastChecked > oneHour) // only attempt to transition every in-game hour
            {
                state.TimeLastChecked += oneHour;
                state.CurrentFuelHours -= 1f;

                if (state.CurrentFuelHours <= 0)
                {
                    TryBETransition(EnumLightState.burnedout);
                    break;
                }
            }
        }


        // attempts to transition the placed block
        public void TryBETransition(EnumLightState toLightState)
        {
            Block block = Api.World.BlockAccessor.GetBlock(Blockentity.Pos);
            Block toBlock;

            // sanity check
            if (block.Attributes == null) 
                return;

            string toState = Enum.GetName(typeof(EnumLightState), toLightState);
            if (toState == null) 
                return;

            AssetLocation blockCode = block.CodeWithVariant("state", toState);

            toBlock = Api.World.GetBlock(blockCode);
            if (toBlock == null) 
                return;

            Api.World.BlockAccessor.SetBlock(toBlock.BlockId, Blockentity.Pos);
        }


        // load attributes from tree
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            state.TimeLastChecked = tree.GetFloat("TimeLastChecked");
            state.CurrentFuelHours = tree.GetFloat("CurrentFuel");
            state.CurrentDepletionMul = tree.GetFloat("CurrentDepletionMul");
        }


        // save attributes to tree
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat("TimeLastChecked", state.TimeLastChecked);
            tree.SetFloat("CurrentFuel", state.CurrentFuelHours);
            tree.SetFloat("CurrentDepletionMul", state.CurrentDepletionMul);
        }


        // transfer attributes from itemStack.Block.Attributes
        public override void OnBlockPlaced()
        {
           // need a way to access fromStack (will be fixed in 1.18)
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            SetBlockDrops();
            base.OnBlockBroken(byPlayer);
        }


        // save the current attributes to the block drops when this block is broken
        void SetBlockDrops()
        {
            Block block = Blockentity.Block;

            foreach (BlockDropItemStack blockDrop in block.Drops) {
                blockDrop.ResolvedItemstack.Attributes.SetFloat("state.CurrentFuel", state.CurrentFuelHours);
                blockDrop.ResolvedItemstack.Attributes.SetFloat("state.CurrentDepletionMul", state.CurrentDepletionMul);
            }
        }
    }
}
