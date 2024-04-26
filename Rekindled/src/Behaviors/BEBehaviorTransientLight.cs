using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Rekindled.src.Behaviors
{
    public class BEBehaviorTransientLight : BlockEntityBehavior
    {
        public int checkIntervalMs = 2000; // every 2 seconds

        public TransientLightProps Props;
        public TransientLightState State;
        long listenerId; // ensure the GameTickListener is unique

        public BEBehaviorTransientLight(BlockEntity blockEntity) : base(blockEntity) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            BlockBehaviorTransientLight behavior = Blockentity.Block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
            if (behavior == null)
                return;

            // Sanity check from BlockEntityTransient
            if (api.Side == EnumAppSide.Server)
            {
                Block block = api.World.BlockAccessor.GetBlock(Pos);
                if (block.Id != Blockentity.Block.Id)
                {
                    if (block.EntityClass != Block.EntityClass)
                    {
                        Api.World.Logger.Warning("[Rekindled] BETransient @{0} for Block {1}, but there is {2} at this position? Will delete BE and attempt to recreate it", Pos, Block.Code.ToShortString(), block.Code.ToShortString());
                        api.Event.EnqueueMainThreadTask(delegate
                        {
                            api.World.BlockAccessor.RemoveBlockEntity(Pos);
                            Block block2 = api.World.BlockAccessor.GetBlock(Pos);
                            api.World.BlockAccessor.SetBlock(block2.Id, Pos);
                        }, "delete betransient");
                    }
                    else
                    {
                        Api.World.Logger.Error("[Rekindled] BETransient @{0} for Block {1}, but there is {2} at this position? Will delete BE", Pos, Block.Code.ToShortString(), block.Code.ToShortString());
                        api.Event.EnqueueMainThreadTask(delegate
                        {
                            api.World.BlockAccessor.RemoveBlockEntity(Pos);
                        }, "delete betransient");
                    }
                    return;
                }
            }

            if (Block.Attributes == null)
                return;
            if (!Block.Attributes["transientLightProps"].Exists)
                return;

            Props = Block.Attributes["transientLightProps"].AsObject<TransientLightProps>();

            if (api.Side == EnumAppSide.Server)
            {
                if (listenerId != 0L)
                    throw new InvalidOperationException("[Rekindled] Initializing BEBehaviorTransientLight twice would create a memory and performance leak");

                listenerId = Blockentity.RegisterGameTickListener(UpdateAndCheckTransition, checkIntervalMs);
            }
        }


        public void UpdateAndCheckTransition(float deltaTime)
        {
            if (State == null)
            {
                RekindledMain.sapi.Logger.Notification("state is null for BE");
                return;
            }

            if (Blockentity.Block.Attributes == null)
            {
                Api.World.Logger.Error("[Rekindled] BEBehaviorTransientLight @{0}: cannot find block attributes for {1}. Will stop transition timer", Blockentity.Pos, Blockentity.Block.Code.ToShortString());
                Blockentity.UnregisterGameTickListener(listenerId);
                return;
            }

            double hoursPassed = Api.World.Calendar.TotalHours - State.LastUpdatedTotalHours;

            if (hoursPassed > 0.05f)
            {
                double hoursPassedAdjusted = hoursPassed * State.CurrentDepletionMul;
                RekindledMain.sapi.Logger.Notification("[BE] Fuel: " + Math.Round(State.CurrentFuelHours, 2) + " -> " + Math.Round(State.CurrentFuelHours - hoursPassedAdjusted, 2)); // TODO: remove this for release
                State.CurrentFuelHours -= hoursPassedAdjusted;

                if (State.CurrentFuelHours <= 0 && State.LightState == EnumLightState.Lit)
                    TryTransition(EnumLightState.Burnedout);

                State.LastUpdatedTotalHours = Api.World.Calendar.TotalHours;
            }

            Blockentity.MarkDirty(true);
        }


        // attempts to transition the placed block
        public void TryTransition(EnumLightState toLightState)
        {
            RekindledMain.sapi.Logger.Notification("TryTransition: @{0}, attempting transition on {1}", Blockentity.Pos, Blockentity.Block.Code.ToShortString());

            Block toBlock;

            // sanity check
            if (Blockentity.Block.Attributes == null)
                return;

            string toState = toLightState.GetName().ToLower();
            if (toState == null)
                return;

            AssetLocation blockCode = Blockentity.Block.CodeWithVariant("state", toState);

            toBlock = Api.World.GetBlock(blockCode);
            if (toBlock == null)
                return;

            Api.World.BlockAccessor.SetBlock(toBlock.BlockId, Blockentity.Pos);

            RekindledMain.sapi.Logger.Notification("TryTransition: @{0}, successfully transitioned {1} to {2}", Blockentity.Pos, Blockentity.Block.Code.ToShortString(), toState);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            if (Props == null)
                return;

            if (State == null
                && tree.HasAttribute(TransientUtil.ATTR_CURR_HOURS)
                && tree.HasAttribute(TransientUtil.ATTR_CURR_DEPLETION)
                && tree.HasAttribute(TransientUtil.ATTR_UPDATED_HOURS))
            {
                State = new TransientLightState(Props)
                {
                    CurrentFuelHours = tree.GetDouble(TransientUtil.ATTR_CURR_HOURS),
                    CurrentDepletionMul = tree.GetDouble(TransientUtil.ATTR_CURR_DEPLETION),
                    LastUpdatedTotalHours = tree.GetDouble(TransientUtil.ATTR_UPDATED_HOURS)
                };

            }
            else if (State != null)
            {
                State.CurrentFuelHours = tree.GetDouble(TransientUtil.ATTR_CURR_HOURS);
                State.CurrentDepletionMul = tree.GetDouble(TransientUtil.ATTR_CURR_DEPLETION);
                State.LastUpdatedTotalHours = tree.GetDouble(TransientUtil.ATTR_UPDATED_HOURS);

                // Blockentity.Block.GetBehavior<BlockBehaviorTransientLight>().State = State;
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (State == null)
                return;

            if (Api.Side == EnumAppSide.Server)
            {
                tree.SetDouble(TransientUtil.ATTR_CURR_HOURS, State.CurrentFuelHours);
                tree.SetDouble(TransientUtil.ATTR_CURR_DEPLETION, State.CurrentDepletionMul);
                tree.SetDouble(TransientUtil.ATTR_UPDATED_HOURS, State.LastUpdatedTotalHours);
            }
        }


        // transfer state from itemStack
        public override void OnBlockPlaced(ItemStack byItemStack)
        {
            RekindledMain.sapi.Logger.Notification("placed transient light of type: " + Blockentity.Block.Code.Path);
            SetFromItemStack(byItemStack);
        }


        public void SetFromItemStack(ItemStack byItemStack)
        {
            if (byItemStack == null) // placed by worldgen/already exists, just load treeattributes instead          
                return;

            if (!byItemStack.Attributes.HasAttribute(TransientUtil.ATTR_TRANSIENTSTATE))
                return;

            ITreeAttribute attr = (ITreeAttribute)byItemStack.Attributes[TransientUtil.ATTR_TRANSIENTSTATE];

            Props = RekindledMain.ResolvePropsFromBlock(Blockentity.Block);
            State = new TransientLightState(Props, attr);

            Blockentity.MarkDirty(true);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (State == null)
            {
                dsc.AppendLine("State is null.");
                return;
            }

            dsc.AppendLine("State: " + State.LightState.GetName() +
                    "\nFuel Hours Remaining: " + Math.Round(State.CurrentFuelHours, 2) + "/" + Math.Round(Props.MaxFuelHours, 2) + " (" + Math.Round(State.CurrentFuelHours / Props.MaxFuelHours * 100.0, 2) + "%)" +
                    "\nCurrent Depletion Multiplier: x" + Math.Round(State.CurrentDepletionMul, 2) + "\n");
        }
    }
}
