using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

using Rekindled.src.Behaviors;
using System;
using Vintagestory.API.Datastructures;

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


        public static ItemStack UpdateAndGetTransientState(IWorldAccessor world, ItemSlot inslot)
        {
            if (inslot is ItemSlotCreative)
                return null;

            ItemStack itemStack = inslot.Itemstack;
            if (itemStack == null)
                return null;

            if (!inslot.Itemstack.Collectible.HasBehavior(typeof(BlockBehaviorTransientLight))) // TODO: update this for generic light sources (not just blocks)
                return null;


            var behavior = inslot.Itemstack.Block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
            TransientLightProps props = behavior.Props;

            double currentTotalHours = world.Calendar.TotalHours;

            if (itemStack.Attributes == null)
                itemStack.Attributes = new TreeAttribute();

            if (!itemStack.Attributes.HasAttribute(TransientUtil.ATTR_TRANSIENTSTATE))
                itemStack.Attributes[TransientUtil.ATTR_TRANSIENTSTATE] = new TreeAttribute();

            ITreeAttribute attr = (ITreeAttribute)itemStack.Attributes[TransientUtil.ATTR_TRANSIENTSTATE];


            EnumLightState lightState;
            double currentFuelHours;
            double currentDepletionMul;


            if (!attr.HasAttribute(TransientUtil.ATTR_CREATED_HOURS)) // create new data
            {
                attr.SetDouble(TransientUtil.ATTR_CREATED_HOURS, currentTotalHours);
                attr.SetDouble(TransientUtil.ATTR_UPDATED_HOURS, currentTotalHours);

                lightState = behavior.GetLightState();
                currentFuelHours = behavior.Props.MaxFuelHours;
                currentDepletionMul = behavior.Props.BaseDepletionMul;

                attr.SetInt(TransientUtil.ATTR_CURR_LIGHTSTATE, (int)lightState);
                attr.SetDouble(TransientUtil.ATTR_CURR_HOURS, currentFuelHours);
                attr.SetDouble(TransientUtil.ATTR_CURR_DEPLETION, currentDepletionMul);
            }
            else
            {
                lightState = (EnumLightState)attr.GetInt(TransientUtil.ATTR_CURR_LIGHTSTATE);
                currentFuelHours = attr.GetDouble(TransientUtil.ATTR_CURR_HOURS);
                currentDepletionMul = attr.GetDouble(TransientUtil.ATTR_CURR_DEPLETION);
            }

            double hoursPassed = currentTotalHours - attr.GetDouble(TransientUtil.ATTR_UPDATED_HOURS);

            ItemStack transitionStack = null;
            if (lightState == EnumLightState.Lit)
            {
                if (hoursPassed > 0.05f)
                {
                    double hoursPassedAdjusted = hoursPassed * currentDepletionMul;
                    RekindledMain.sapi.Logger.Notification("[ItemSlot] Fuel: " + Math.Round(currentFuelHours, 2) + " -> " + Math.Round(currentFuelHours - hoursPassedAdjusted, 2));
                    currentFuelHours -= hoursPassedAdjusted;
                    attr.SetDouble(TransientUtil.ATTR_CURR_HOURS, currentFuelHours);
                }

                if (currentFuelHours <= 0 && world.Side == EnumAppSide.Server) // perform transition to burnedout state
                {
                    ItemStack newStack = behavior.OnTransitionStack(inslot, EnumLightState.Burnedout);

                    inslot.Itemstack.SetFrom(newStack);

                    behavior = inslot.Itemstack.Block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
                    attr = (ITreeAttribute)inslot.Itemstack.Attributes[TransientUtil.ATTR_TRANSIENTSTATE];

                    attr.SetInt(TransientUtil.ATTR_CURR_LIGHTSTATE, (int)behavior.GetLightState());
                    attr.SetInt(TransientUtil.ATTR_CURR_HOURS, 0);

                    transitionStack = inslot.Itemstack;

                    inslot.MarkDirty();
                }
            }

            if (hoursPassed > 0.05f)
                attr.SetDouble(TransientUtil.ATTR_UPDATED_HOURS, currentTotalHours);

            if (behavior.State == null)
                behavior.State = new TransientLightState(props)
                {
                    LightState = lightState,
                    LastUpdatedTotalHours = attr.GetDouble(TransientUtil.ATTR_UPDATED_HOURS),
                    CurrentFuelHours = currentFuelHours,
                    CurrentDepletionMul = currentDepletionMul,
                    CreatedTotalHours = attr.GetDouble(TransientUtil.ATTR_CREATED_HOURS)
                };
            else
            {
                behavior.State.LightState = lightState;
                behavior.State.LastUpdatedTotalHours = attr.GetDouble(TransientUtil.ATTR_UPDATED_HOURS);
                behavior.State.CurrentFuelHours = currentFuelHours;
                behavior.State.CurrentDepletionMul = currentDepletionMul;
                behavior.State.CreatedTotalHours = attr.GetDouble(TransientUtil.ATTR_CREATED_HOURS);
            }

            return transitionStack;
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
