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
    class BlockEntityExtinguishable : BlockEntityTransient
    {
        WeatherSystemBase wsys;
        Vec3d tmpPos = new Vec3d();

        public float oneSecAccum = 0f;

        public override void Initialize(ICoreAPI api)
        {
            CheckIntervalMs = 1000;
            wsys = api.ModLoader.GetModSystem<WeatherSystemBase>();

            base.Initialize(api);

            RegisterGameTickListener(onGameTick, 50);
        }


        // called every 50 ms, tick durability every second
        public void onGameTick(float dt)
        {
            Api.Logger.Warning("[Rekindled] torch durability tick start");
            if (oneSecAccum > -1)
                oneSecAccum += dt;

            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (oneSecAccum >= 1)
            {
                // transition block to burned out state when durability runs out
                if (Block.Durability <= 0)
                {
                    Api.Logger.Warning("[Rekindled] torch is burned out");                
                    if (block.Attributes == null) 
                        return;

                    var toCode = block.CodeWithVariant("state", "burnedout").ToShortString();
                    tryTransition(toCode);

                    oneSecAccum = -1; // torch is burnt out, stop ticking

                    return;
                }

                block.Attributes.SetInt("durability", block.GetDurability - 1);
                oneSecAccum = 0f;
                System.Diagnostics.Debug.WriteLine("[Rekindled] tick! durability of " + block.GetPlacedBlockName(Api.World, Pos) + " is now " + block.Durability);
                
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("oneSecAccum", oneSecAccum);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            oneSecAccum = tree.GetFloat("oneSecAccum");
        }


        // handle extinguishing when raining
        public override void CheckTransition(float dt)
        {
            tmpPos.Set(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
            double rainLevel = 0;
            bool rainCheck =
                Api.Side == EnumAppSide.Server
                && Api.World.Rand.NextDouble() < 0.15
                && Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y
                && (rainLevel = wsys.GetPrecipitation(tmpPos)) > 0.04
            ;

            if (rainCheck && Api.World.Rand.NextDouble() < rainLevel * 5)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos.X + 0.5, Pos.Y + 0.75, Pos.Z + 0.5, null, false, 16);
                if (Api.World.Rand.NextDouble() < 0.2 + rainLevel / 2f)
                {
                    Block block = Api.World.BlockAccessor.GetBlock(Pos);
                    if (block.Attributes == null) return;
                    var toCode = block.CodeWithVariant("state", "extinguished").ToShortString();
                    tryTransition(toCode);
                }
            }

            if (Api.World.Rand.NextDouble() < 0.3)
            {
                base.CheckTransition(dt);
            }
        }
    }
}
