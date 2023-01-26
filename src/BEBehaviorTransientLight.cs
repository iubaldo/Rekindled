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
    class TransientLightProperties 
    {
        public float MaxFuel = -1;
        public float DepletionMul = 1; // modifies how quickly fuel depletes

        public string ConvertTo;
        public string ConvertFrom;
    }


    // A custom version of BlockEntityTransient, specifically for light sources
    class BEBehaviorTransientLight : BlockEntityBehavior
    {
        public virtual int CheckIntervalMs { get; set; } = 2000; // every 2 seconds

        float TimeLastChecked = 0; // the last time the transition was checked
        float CurrentFuel = -1;
        float CurrentDepletionMul = 1;

        TransientLightProperties props;

        long listenerId; // ensure the GameTickListener is unique

        public string ConvertToOverride; // overrides ConvertTo in TransientLightProperties


        public BEBehaviorTransientLight(BlockEntity blockentity) : base(blockentity) { }


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            if (Blockentity.Block.Attributes?["transientLightProps"].Exists != true) return;

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

            if (CurrentFuel < 0)
            {
                CurrentFuel = props.MaxFuel;
                CurrentDepletionMul = props.DepletionMul;
                SetBlockDrops();
            }               

            if (api.Side == EnumAppSide.Server)
            {
                if (listenerId != 0)
                {
                    throw new InvalidOperationException("Initializing BEBehaviorTransientLight twice would create a memory and performance leak");
                }
                listenerId = Blockentity.RegisterGameTickListener(CheckBETransition, CheckIntervalMs);
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
            TimeLastChecked = (float) Math.Min(TimeLastChecked, Api.World.Calendar.TotalDays);

            float oneHour = 1f / Api.World.Calendar.HoursPerDay;
            while (Api.World.Calendar.TotalDays - TimeLastChecked > oneHour) // only attempt to transition every in-game hour
            {
                TimeLastChecked += oneHour;
                CurrentFuel -= 1f;

                if (CurrentFuel <= 0)
                {
                    TryBETransition(ConvertToOverride ?? props.ConvertTo);
                    break;
                }
            }
        }

        public void TryBETransition(string toCode)
        {
            Block block = Api.World.BlockAccessor.GetBlock(Blockentity.Pos);
            Block tblock;

            if (block.Attributes == null) return;

            string fromCode = props.ConvertFrom;
            if (fromCode == null || toCode == null) return;

            if (fromCode.IndexOf(":") == -1) fromCode = block.Code.Domain + ":" + fromCode;
            if (toCode.IndexOf(":") == -1) toCode = block.Code.Domain + ":" + toCode;


            if (fromCode == null || !toCode.Contains("*"))
            {
                tblock = Api.World.GetBlock(new AssetLocation(toCode));
                if (tblock == null) return;

                Api.World.BlockAccessor.SetBlock(tblock.BlockId, Blockentity.Pos);
                return;
            }

            AssetLocation blockCode = block.WildCardReplace(
                new AssetLocation(fromCode),
                new AssetLocation(toCode)
            );

            tblock = Api.World.GetBlock(blockCode);
            if (tblock == null) return;

            Api.World.BlockAccessor.SetBlock(tblock.BlockId, Blockentity.Pos);
        }


        // load attributes from tree
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            TimeLastChecked = tree.GetFloat("timeLastChecked");
            CurrentFuel = tree.GetFloat("currentFuel");
            CurrentDepletionMul = tree.GetFloat("currentDepletionMul");
        }


        // save attributes to tree
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat("timeLastChecked", TimeLastChecked);
            tree.SetFloat("currentFuel", CurrentFuel);
            tree.SetFloat("currentDepletionMul", CurrentDepletionMul);
        }


        // transfer attributes from itemStack.Block.Attributes
        public override void OnBlockPlaced()
        {
           // need a way to access fromStack
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
                blockDrop.ResolvedItemstack.Attributes.SetFloat("currentFuel", CurrentFuel);
                blockDrop.ResolvedItemstack.Attributes.SetFloat("currentDepletionMul", CurrentDepletionMul);
            }
        }
    }
}
