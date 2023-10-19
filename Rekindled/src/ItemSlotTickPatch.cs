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

            double hoursPassed = currentTotalHours - attr.GetDouble(ATTR_LAST_UPDATED_TOTAL_HOURS);

            if (state.LightState == EnumLightState.Lit)
            {
                if (hoursPassed > 0.05f)
                {
                    double hoursPassedAdjusted = hoursPassed * state.CurrentDepletionMul;
                    attr.SetDouble(ATTR_CURRENT_FUEL_HOURS, state.CurrentFuelHours - hoursPassedAdjusted);
                }

                if (state.CurrentFuelHours <= 0 && world.Side == EnumAppSide.Server) // perform transition to burnedout state
                {
                    ItemStack newStack = behavior.OnTransitionStack(inslot, EnumLightState.Burnedout);

                    inslot.Itemstack.SetFrom(newStack);

                    behavior = inslot.Itemstack.Block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
                    state = behavior.State;
                    attr.SetInt(ATTR_CURRENT_LIGHT_STATE, (int)state.LightState);
                    attr.SetInt(ATTR_CURRENT_FUEL_HOURS, 0);

                    inslot.MarkDirty();
                }
            }

            if (hoursPassed > 0.05f)
                attr.SetDouble(ATTR_LAST_UPDATED_TOTAL_HOURS, currentTotalHours);

            state.LightState = (EnumLightState)attr.GetInt(ATTR_CURRENT_LIGHT_STATE);
            state.CurrentFuelHours = attr.GetDouble(ATTR_CURRENT_FUEL_HOURS);
            state.CurrentDepletionMul = attr.GetDouble(ATTR_CURRENT_DEPLETION_MUL);
        }
    }
}
