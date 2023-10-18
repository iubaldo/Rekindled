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

namespace Rekindled.src
{
    // this class allows placed transient lights to tick their fuel count
    class BEBehaviorTransientLight : BlockEntityBehavior
    {
        public int checkIntervalMs = 2000; // every 2 seconds

        TransientLightProps Props;
        TransientLightState State;
        long listenerId; // ensure the GameTickListener is unique

        public BEBehaviorTransientLight(BlockEntity blockEntity) : base(blockEntity) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            BlockBehaviorTransientLight behavior = Blockentity.Block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
            if (behavior == null)
                return;

            // Sanity check
            if (api.Side == EnumAppSide.Server)
            {
                if (Blockentity.Block.Id != Blockentity.Block.Id)
                {
                    Api.World.Logger.Error("BEBehaviorTransientLight @{0} for Block {1}, but there is {2} at this position? Will delete BE", Blockentity.Pos, Blockentity.Block.Code.ToShortString(), Blockentity.Block.Code.ToShortString());
                    // Can't delete during init
                    api.Event.EnqueueMainThreadTask(() => api.World.BlockAccessor.RemoveBlockEntity(Blockentity.Pos), "delete betransientlight");
                    return;
                }
            }

            Props = behavior.Props;
            State = behavior.State;

            if (api.Side == EnumAppSide.Server)
            {
                if (listenerId != 0)
                    throw new InvalidOperationException("Initializing BEBehaviorTransientLight twice would create a memory and performance leak");
                
                listenerId = Blockentity.RegisterGameTickListener(CheckBETransition, checkIntervalMs);
            }

            Api.World.Logger.Error("BEBehaviorTransientLight @{0}: initializing props for {1}... ", Blockentity.Pos, this.Blockentity.Block.Code.ToShortString());
        }


        public void CheckBETransition(float deltaTime)
        {
            if (Blockentity.Block.Attributes == null)
            {
                Api.World.Logger.Error("BEBehaviorTransientLight @{0}: cannot find block attributes for {1}. Will stop transient timer", Blockentity.Pos, Blockentity.Block.Code.ToShortString());
                Blockentity.UnregisterGameTickListener(listenerId);
                return;
            }

            // In case this block was imported from another older world. In that case lastCheckAtTotalDays would be a future date.
            State.LastUpdatedTotalHours = (float)Math.Min(State.LastUpdatedTotalHours, Api.World.Calendar.TotalDays);

            float oneHour = 1f / Api.World.Calendar.HoursPerDay;
            while (Api.World.Calendar.TotalDays - State.LastUpdatedTotalHours > oneHour) // if from an older world, simulate for difference in time
            {
                State.LastUpdatedTotalHours += oneHour;
                State.CurrentFuelHours -= 1f;

                if (State.CurrentFuelHours <= 0)
                {
                    TryBETransition(EnumLightState.Burnedout);
                    break;
                }
            }
        }


        // attempts to transition the placed block
        public void TryBETransition(EnumLightState toLightState)
        {
            System.Diagnostics.Debug.WriteLine("TryBETransition: @{0}, attempting transition on {1}", Blockentity.Pos, Blockentity.Block.Code.ToShortString());

            Block toBlock;

            // sanity check
            if (Blockentity.Block.Attributes == null)
                return;

            string toState = Enum.GetName(typeof(EnumLightState), toLightState);
            if (toState == null)
                return;

            AssetLocation blockCode = Blockentity.Block.CodeWithVariant("state", toState);

            toBlock = Api.World.GetBlock(blockCode);
            if (toBlock == null)
                return;

            Api.World.BlockAccessor.SetBlock(toBlock.BlockId, Blockentity.Pos);

            System.Diagnostics.Debug.WriteLine("TryBETransition: @{0}, successfully transitioned {1} to {2}", Blockentity.Pos, Blockentity.Block.Code.ToShortString(), toState);
        }


        // load attributes from tree
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            if (State == null)
                return;

            State.LastUpdatedTotalHours = tree.GetFloat("TimeLastChecked");
            State.CurrentFuelHours = tree.GetFloat("CurrentFuelHours");
            State.CurrentDepletionMul = tree.GetFloat("CurrentDepletionMul");
        }


        // save attributes to tree
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (State == null)
                return;

            tree.SetDouble("TimeLastChecked", State.LastUpdatedTotalHours);
            tree.SetDouble("CurrentFuelHours", State.CurrentFuelHours);
            tree.SetDouble("CurrentDepletionMul", State.CurrentDepletionMul);
        }


        // transfer state from itemStack
        public override void OnBlockPlaced(ItemStack byItemStack)
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

            foreach (BlockDropItemStack blockDrop in block.Drops)
            {
                blockDrop.ResolvedItemstack.Attributes.SetDouble("state.CurrentFuel", State.CurrentFuelHours);
                blockDrop.ResolvedItemstack.Attributes.SetDouble("state.CurrentDepletionMul", State.CurrentDepletionMul);
            }
        }
    }
}
