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


        [HarmonyPrefix]
        [HarmonyPatch(typeof(CollectibleObject), "UpdateAndGetTransitionStatesNative")]
        public static void PrefixUpdateAndGetTransitionStatesNative(IWorldAccessor world, ItemSlot inslot)
        {
            UpdateTransientState(world, inslot);
        }


        public static ItemStack UpdateTransientState(IWorldAccessor world, ItemSlot inslot)
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

            if (!itemStack.Attributes.HasAttribute(ATTR_TRANSIENT_STATE))
                itemStack.Attributes[ATTR_TRANSIENT_STATE] = new TreeAttribute();

            ITreeAttribute attr = (ITreeAttribute)itemStack.Attributes[ATTR_TRANSIENT_STATE];


            EnumLightState lightState;
            double currentFuelHours;
            double currentDepletionMul;


            if (!attr.HasAttribute(ATTR_CREATED_TOTAL_HOURS)) // create new data
            {
                attr.SetDouble(ATTR_CREATED_TOTAL_HOURS, currentTotalHours);
                attr.SetDouble(ATTR_LAST_UPDATED_TOTAL_HOURS, currentTotalHours);

                lightState = behavior.GetLightState();
                currentFuelHours = behavior.Props.MaxFuelHours;
                currentDepletionMul = behavior.Props.BaseDepletionMul;

                attr.SetInt(ATTR_CURRENT_LIGHT_STATE, (int)lightState);
                attr.SetDouble(ATTR_CURRENT_FUEL_HOURS, currentFuelHours);
                attr.SetDouble(ATTR_CURRENT_DEPLETION_MUL, currentDepletionMul);
            }
            else
            {
                lightState = (EnumLightState)attr.GetInt(ATTR_CURRENT_LIGHT_STATE);
                currentFuelHours = attr.GetDouble(ATTR_CURRENT_FUEL_HOURS);
                currentDepletionMul = attr.GetDouble(ATTR_CURRENT_DEPLETION_MUL);
            }

            double hoursPassed = currentTotalHours - attr.GetDouble(ATTR_LAST_UPDATED_TOTAL_HOURS);

            ItemStack transitionStack = null;
            if (lightState == EnumLightState.Lit)
            {
                if (hoursPassed > 0.05f)
                {
                    double hoursPassedAdjusted = hoursPassed * currentDepletionMul;
                    RekindledMain.sapi.Logger.Notification("Fuel: " + currentFuelHours + " -> " + (currentFuelHours - hoursPassedAdjusted));
                    currentFuelHours -= hoursPassedAdjusted;
                    attr.SetDouble(ATTR_CURRENT_FUEL_HOURS, currentFuelHours);
                }

                if (currentFuelHours <= 0 && world.Side == EnumAppSide.Server) // perform transition to burnedout state
                {
                    ItemStack newStack = behavior.OnTransitionStack(inslot, EnumLightState.Burnedout);

                    inslot.Itemstack.SetFrom(newStack);

                    behavior = inslot.Itemstack.Block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
                    attr = (ITreeAttribute)inslot.Itemstack.Attributes[ATTR_TRANSIENT_STATE];

                    attr.SetInt(ATTR_CURRENT_LIGHT_STATE, (int)behavior.GetLightState());
                    attr.SetInt(ATTR_CURRENT_FUEL_HOURS, 0);

                    transitionStack = inslot.Itemstack;

                    inslot.MarkDirty();
                }
            }

            if (hoursPassed > 0.05f)
                attr.SetDouble(ATTR_LAST_UPDATED_TOTAL_HOURS, currentTotalHours);

            return transitionStack;
        }
    }
}
