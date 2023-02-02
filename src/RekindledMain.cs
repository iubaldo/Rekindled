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

namespace rekindled.src
{

    /*
     * TODO:
     * 
     * edit light source info to read:
     *  State: lightState
     *  Fuel: x%
     *  Time Remaining: x time (copy syntax of food spoilage)
     *  
     * patch-add "lightState" variant code to light sources and use that to determine model
     *  remove "state" from torch and replace with lightState
     * 
     * patch-reenable unused torches and recipes
     */

    class RekindledMain : ModSystem
    {
        ICoreServerAPI sapi;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            System.Diagnostics.Debug.WriteLine("LOADING REKINDLED MAIN");

            api.RegisterBlockBehaviorClass("blockbehaviortransientlight", typeof(BlockBehaviorTransientLight));
            api.RegisterBlockEntityBehaviorClass("bebehaviortransientlight", typeof(BEBehaviorTransientLight));
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            base.StartServerSide(sapi);

            // sapi.Event.RegisterGameTickListener(UpdateLightSources, 2000);
            sapi.RegisterCommand("transitionBE", "Attempt to transition the block you're currently looking at.", "", DebugUpdateBlockEntity);
            sapi.RegisterCommand("transitionHand", "Attempt to transition the block currently in hand.", "", DebugUpdateBlockInHand);
            sapi.RegisterCommand("transitionAround", "Attempt to transition entityItems in a radius around you.", "", DebugUpdateBlocksAround);
        }


        void DebugUpdateBlockEntity(IServerPlayer player, int groupId, CmdArgs args)
        {
            BlockEntity entity = sapi.World.BlockAccessor.GetBlockEntity(player.CurrentBlockSelection.Position);
            if (entity.Block.Attributes?["transientLightProps"].Exists == false)
            {
                sapi.Logger.Warning("DebugUpdateBlockEntity: @{0}, could not find transientLightProps for @{1}", entity.Pos, entity.Block.Code.ToShortString());
                return;
            }

            entity.GetBehavior<BEBehaviorTransientLight>().TryBETransition(EnumLightState.unlit);
            sapi.Logger.Warning("DebugUpdateBlockEntity: @{0}, perfomed transition for @{1}", entity.Pos, entity.Block.Code.ToShortString());
        }


        void DebugUpdateBlockInHand(IServerPlayer player, int groupId, CmdArgs args)
        {
            ItemSlot slot = player.InventoryManager.ActiveHotbarSlot;
            Block block = slot.Itemstack.Block;
            if (block == null)
            {
                sapi.Logger.Warning("DebugUpdateBlockInHand: could not find block in slot: {0}", slot.ToString());
                return;
            }

            var behavior = block.GetBehavior<BlockBehaviorTransientLight>();
            if (behavior == null)
            {
                sapi.Logger.Warning("DebugUpdateBlockInHand: could not find BlockBehaviorTransientLight for {0} in slot: {1}", block.Code.ToShortString(), slot.ToString());
                return;
            }

            behavior.TryBlockTransition(EnumLightState.burntout, slot);
            sapi.Logger.Warning("DebugUpdateBlockInHand: performed transition for {0} in slot: {1}", block.Code.ToShortString(), slot.ToString());

        }


        void DebugUpdateBlocksAround(IServerPlayer player, int groupId, CmdArgs args)
        {
            bool foundTarget = false;
            foreach (Entity entity in sapi.World.GetEntitiesAround(player.Entity.SidedPos.XYZ, 32, 32, (Entity entity) => { return IsEntityTransientLight(entity); }))
            {
                var entityItem = entity as EntityItem;
                TryTransitionEntityItem(entityItem.Slot);
                sapi.Logger.Warning("DebugUpdateBlocksAround: @{0}, performed transition for {1}", entityItem.Itemstack.Block.Code.ToShortString());
                foundTarget = true;
            }

            if (!foundTarget)
                sapi.Logger.Warning("DebugUpdateBlocksAround: could not find valid targets");

        }


        void UpdateLightSources(float deltaTime)
        {
            foreach(IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.ConnectionState != EnumClientState.Playing) // only check near active players
                    return;

                foreach (Entity entity in sapi.World.GetEntitiesAround(player.Entity.SidedPos.XYZ, 32, 32, (Entity entity) => { return IsEntityTransientLight(entity); }))
                {
                    var entityItem = entity as EntityItem;
                    TryTransitionEntityItem(entityItem.Slot);
                }
            }
        }


        void TryTransitionEntityItem(ItemSlot slot)
        {
            var behavior = slot.Itemstack.Block.GetBehavior(typeof(BlockBehaviorTransientLight)) as BlockBehaviorTransientLight;
            behavior.TryBlockTransition(EnumLightState.burntout, slot);
        }


        bool IsEntityTransientLight(Entity entity)
        {
            var entityItem = entity as EntityItem;
            TransientLightProperties props = entityItem.Itemstack.Block.Attributes["transientLightProps"].AsObject<TransientLightProperties>();
            return props != null;
        }
    }
}