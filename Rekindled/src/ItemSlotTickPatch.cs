using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

using HarmonyLib;

namespace Rekindled.src
{
    [HarmonyPatch]
    public class ItemSlotTickPatch
    {
        // magic strings for attributes
        const string ATTR_TRANSIENT_STATE = "transientState";
        const string ATTR_CREATED_TOTAL_HOURS = "createdTotalHours";
        const string ATTR_LAST_UPDATED_TOTAL_HOURS = "lastUpdatedTotalHours";
        const string ATTR_CURRENT_LIGHT_STATE = "currentLightState";
        const string ATTR_CURRENT_FUEL_HOURS = "currentFuelHours";
        const string ATTR_CURRENT_DEPLETION_MUL = "currentDepletionMul";


        [HarmonyPostfix]
        [HarmonyPatch(typeof(CollectibleObject), "UpdateAndGetTransitionStatesNative")]
        public static void PostfixUpdateAndGetTransitionStatesNative(IWorldAccessor world, ItemSlot inslot)
        {
            if (inslot is ItemSlotCreative)
                return;

            ItemStack itemStack = inslot.Itemstack;
            if (itemStack == null) 
                return;

            if (!inslot.Itemstack.Collectible.HasBehavior(typeof(BlockBehaviorTransientLight))) // TODO: update this for generic light sources (not just blocks)
                return;


            var behavior = inslot.Itemstack.Block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
            TransientLightProps props = behavior.Props;
            TransientLightState state = behavior.State;

            double currentTotalHours = world.Calendar.TotalHours;

            if (itemStack.Attributes == null)
                itemStack.Attributes = new TreeAttribute();

            if (!itemStack.Attributes.HasAttribute(ATTR_TRANSIENT_STATE))
                itemStack.Attributes[ATTR_TRANSIENT_STATE] = new TreeAttribute();

            ITreeAttribute attr = (ITreeAttribute)itemStack.Attributes[ATTR_TRANSIENT_STATE];


            if (!attr.HasAttribute(ATTR_CREATED_TOTAL_HOURS)) // create new data
            {
                attr.SetDouble(ATTR_CREATED_TOTAL_HOURS, currentTotalHours);
                attr.SetDouble(ATTR_LAST_UPDATED_TOTAL_HOURS, currentTotalHours);

                attr[ATTR_CURRENT_LIGHT_STATE] = new IntAttribute((int)state.LightState);
                attr[ATTR_CURRENT_FUEL_HOURS] = new DoubleAttribute(props.MaxFuelHours);
                attr[ATTR_CURRENT_DEPLETION_MUL] = new DoubleAttribute(props.BaseDepletionMul);
            }

            double hoursPassed = currentTotalHours - state.LastUpdatedTotalHours;

            if (hoursPassed > 0.05f)
            {
                double hoursPassedAdjusted = hoursPassed * state.CurrentDepletionMul;
                attr.SetDouble(ATTR_CURRENT_FUEL_HOURS, state.CurrentFuelHours + hoursPassedAdjusted);
            }

            double fuelHoursLeft = Math.Max(0, props.MaxFuelHours - state.CurrentFuelHours);

            if (fuelHoursLeft <= 0 && world.Side == EnumAppSide.Server) // currently causing a stackoverflow, stop the infinite loop
            {
                ItemStack newStack = behavior.OnTransitionStack(inslot, EnumLightState.Burnedout);

                itemStack.SetFrom(newStack);

                inslot.MarkDirty();
            }

            if (hoursPassed > 0.05f)
                attr.SetDouble(ATTR_LAST_UPDATED_TOTAL_HOURS, currentTotalHours);

            state.LightState = (EnumLightState)attr.GetInt(ATTR_CURRENT_LIGHT_STATE);
            state.LastUpdatedTotalHours = attr.GetDouble(ATTR_LAST_UPDATED_TOTAL_HOURS);
            state.CurrentFuelHours = attr.GetDouble(ATTR_CURRENT_FUEL_HOURS);
            state.CurrentDepletionMul = attr.GetDouble(ATTR_CURRENT_DEPLETION_MUL);
        }
    }
}
