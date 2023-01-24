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
        public float TotalFuel = -1;
        public float DepletionMul = 1; // modifies how quickly fuel depletes

        public string ConvertTo;
        public string ConvertFrom;
    }


    // A custom version of BlockEntityTransient, specifically for light sources
    class BEBehaviorTransientLight : BlockEntity
    {
        public virtual int CheckIntervalMs { get; set; } = 2000; // every 2 seconds

        double TimeLastChecked = 0; // the last time the transition was checked
        double CurrentFuel = -1;

        TransientLightProperties props;

        long listenerId; // ensure the GameTickListener is unique

        public string ConvertToOverride; // overrides ConvertTo in TransientLightProperties

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (Block.Attributes?["transientLightProps"].Exists != true) return;

            // Sanity check
            if (api.Side == EnumAppSide.Server)
            {
                var block = Api.World.BlockAccessor.GetBlock(Pos);
                if (block.Id != Block.Id)
                {
                    Api.World.Logger.Error("BEBehaviorTransientLight @{0} for Block {1}, but there is {2} at this position? Will delete BE", Pos, this.Block.Code.ToShortString(), block.Code.ToShortString());
                    // Can't delete during init
                    api.Event.EnqueueMainThreadTask(() => api.World.BlockAccessor.RemoveBlockEntity(Pos), "delete betransientlight");
                    return;
                }
            }

            props = Block.Attributes["transientLightProps"].AsObject<TransientLightProperties>();
            if (props == null) 
                return;

            if (CurrentFuel <= 0)
                CurrentFuel = props.TotalFuel;           

            if (api.Side == EnumAppSide.Server)
            {
                if (listenerId != 0)
                {
                    throw new InvalidOperationException("Initializing BEBehaviorTransientLight twice would create a memory and performance leak");
                }
                listenerId = RegisterGameTickListener(CheckTransition, CheckIntervalMs);
            }
        }

        public void CheckTransition(float deltaTime)
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (block.Attributes == null)
            {
                Api.World.Logger.Error("BEBehaviorTransientLight @{0}: cannot find block attributes for {1}. Will stop transient timer", Pos, this.Block.Code.ToShortString());
                UnregisterGameTickListener(listenerId);
                return;
            }

            // In case this block was imported from another older world. In that case lastCheckAtTotalDays would be a future date.
            TimeLastChecked = Math.Min(TimeLastChecked, Api.World.Calendar.TotalDays);

            float oneHour = 1f / Api.World.Calendar.HoursPerDay;
            while (Api.World.Calendar.TotalDays - TimeLastChecked > oneHour) // only attempt to transition every in-game hour
            {
                TimeLastChecked += oneHour;
                CurrentFuel -= 1f;

                if (CurrentFuel <= 0)
                {
                    TryTransition(ConvertToOverride ?? props.ConvertTo);
                    break;
                }
            }
        }

        public void TryTransition(string toCode)
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
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

                Api.World.BlockAccessor.SetBlock(tblock.BlockId, Pos);
                return;
            }

            AssetLocation blockCode = block.WildCardReplace(
                new AssetLocation(fromCode),
                new AssetLocation(toCode)
            );

            tblock = Api.World.GetBlock(blockCode);
            if (tblock == null) return;

            Api.World.BlockAccessor.SetBlock(tblock.BlockId, Pos);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        // transfer attributes to dropped block
        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
        }

        // transfer attributes from itemStack.Block.Attributes
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
        }
    }



}
