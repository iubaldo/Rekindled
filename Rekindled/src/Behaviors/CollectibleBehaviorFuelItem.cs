using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Rekindled.src.Behaviors
{
    public class CollectibleBehaviorFuelItem : CollectibleBehavior
    {
        public CollectibleBehaviorFuelItem(CollectibleObject collObj) : base(collObj) { }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;

            //if (byEntity.Api.Side == EnumAppSide.Client)
            //    return;

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
                behavior.State.CurrentFuelHours += behavior.Props.MaxFuelHours / 2; // will be clamped when set
                slot.TakeOut(1);

                be.MarkDirty(true);

                if (byEntity.Api.Side == EnumAppSide.Client)
                    ((ICoreClientAPI)byEntity.Api).ShowChatMessage(Lang.Get("Refueled light source."));
                
                handling = EnumHandling.PreventDefault;
            }
        }
    }
}
