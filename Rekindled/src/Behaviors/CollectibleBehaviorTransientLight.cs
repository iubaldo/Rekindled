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

namespace Rekindled.src.Behaviors
{
    // collects and stores props/state from json attributes
    class CollectibleBehaviorTransientLight : CollectibleBehavior
    {
        ICoreServerAPI sapi;

        public TransientLightProps Props;
        public TransientLightState State;

        public CollectibleBehaviorTransientLight(CollectibleObject collObj) : base(collObj)
        {
            if (collObj.Attributes == null)
                return;
            if (!collObj.Attributes["transientLightProps"].Exists)
                return;

            Props = collObj.Attributes["transientLightProps"].AsObject<TransientLightProps>();
            if (Props == null)
                return;

            State = new TransientLightState(Props);

            if (!Enum.TryParse(collObj.Variant["state"], true, out EnumLightState lightState))
                return;
            State.LightState = lightState;
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Server)
                sapi = api as ICoreServerAPI;
        }
    }
}
