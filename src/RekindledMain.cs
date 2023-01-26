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
     * notes:
     * register a gameticklistener server-side to tick light sources instead of doing in within the block itself
     * IWorldAccessor.GetEntitiesAround for ticking dropped entityItems
     */

    class RekindledMain : ModSystem
    {
        ICoreServerAPI sapi;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            System.Diagnostics.Debug.WriteLine("LOADING REKINDLED MAIN");

            api.RegisterBlockBehaviorClass("bbehaviortransferattributesondrop", typeof(BlockBehaviorTransientLight));
            api.RegisterBlockEntityBehaviorClass("bebehaviortransientlight", typeof(BEBehaviorTransientLight));
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            base.StartServerSide(sapi);

            sapi.Event.RegisterGameTickListener(UpdateLightSources, 2000);
        }


        void UpdateLightSources(float deltaTime)
        {
            foreach(IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.ConnectionState != EnumClientState.Playing) // only check near active players
                    return;

                foreach (Entity entity in sapi.World.GetEntitiesAround(player.Entity.SidedPos.XYZ, 32, 32, (Entity entity) => { return IsEntityTransientLight(entity); }))
                {
                    if (entity is EntityItem)
                    {
                        var entityItem = entity as EntityItem;
                        if (entityItem.Itemstack.Class == EnumItemClass.Block && entityItem.Itemstack.Block.Attributes?["transientLightProps"].Exists == true)
                        {
                            // transition
                        }
                    }
                }
            }
        }


        bool IsEntityTransientLight(Entity entity)
        {
            var entityItem = entity as EntityItem;
            TransientLightProperties props = entityItem.Itemstack.Block.Attributes["transientLightProps"].AsObject<TransientLightProperties>();
            return props != null;
        }
    }
}