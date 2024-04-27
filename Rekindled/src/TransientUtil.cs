using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

using Rekindled.src.Behaviors;

namespace Rekindled.src
{
    public class TransientUtil
    {
        public const string ATTR_TRANSIENTSTATE = "transientState"; // name of the actual attribute containing the state

        public const string ATTR_CREATED_HOURS = "createdTotalHours";
        public const string ATTR_UPDATED_HOURS = "lastUpdatedTotalHours";
        public const string ATTR_CURR_LIGHTSTATE = "currentLightState";
        public const string ATTR_CURR_HOURS = "currentFuelHours";
        public const string ATTR_CURR_DEPLETION = "currentDepletionMul";


        public static void TryTransitionEntityItem(ItemSlot slot)
        {
            var behavior = slot.Itemstack.Block.GetBehavior(typeof(BlockBehaviorTransientLight)) as BlockBehaviorTransientLight;
            behavior.TryBlockTransition(EnumLightState.Burnedout, slot);
        }


        public static bool IsEntityItemTransientLight(Entity entity)
        {
            var entityItem = entity as EntityItem;
            TransientLightProps props = entityItem.Itemstack.Block.Attributes["transientLightProps"].AsObject<TransientLightProps>();
            return props != null;
        }


        public static bool IsSlotTransientLight(ItemSlot slot)
        {
            Block block = slot.Itemstack.Block;
            if (block == null)
                return false;

            var behavior = block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
            if (behavior == null)
                return false;

            return true;
        }


        public static bool IsBlockTransientLight(Block block)
        {
            //if (block.Code.Path.Contains("torch-basic")
            //    || block.Code.Path.Contains("torch-crude")
            //    || block.Code.Path.Contains("torch-cloth")
            //    || block.Code.FirstCodePart() == "bunchocandles"
            //    || block.Code.FirstCodePart() == "lantern"
            //    || block.Code.FirstCodePart() == "oillamp")
            //    return true;
            //else if (block.Code.SecondCodePart() == "torchholder" && block.Code.Path.Contains("filled"))
            //    return true;

            if (block.Code.Path.Contains("transient"))
                return true;

            return false;
        }


        public static TransientLightProps ResolvePropsFromBlock(Block block)
        {
            if (block.Attributes == null)
                return null;
            if (!block.Attributes["transientLightProps"].Exists)
                return null;

            return block.Attributes["transientLightProps"].AsObject<TransientLightProps>();
        }
    }
}
