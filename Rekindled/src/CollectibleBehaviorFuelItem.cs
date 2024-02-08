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
    public class CollectibleBehaviorFuelItem : CollectibleBehavior
    {
        public CollectibleBehaviorFuelItem(CollectibleObject collObj) : base(collObj) { }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;

            if (byEntity.Api.Side == EnumAppSide.Client)
                return;

            if (slot.Empty)
                return;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be == null)
                return;

            BEBehaviorTransientLight behavior = be.GetBehavior<BEBehaviorTransientLight>();
            if (behavior == null) 
                return;

            if (behavior.Props.IsValidFuelItem(collObj))
            {
                behavior.State.CurrentFuelHours += (behavior.Props.MaxFuelHours / 2); // will be clamped
                slot.TakeOut(1);

                be.MarkDirty(true);

                RekindledMain.sapi.Logger.Notification("refueled light source");
                handling = EnumHandling.PreventDefault;
            }
        }
    }
}
