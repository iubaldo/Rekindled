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

            if (!itemStack.Attributes.HasAttribute(TransientUtil.ATTR_STATE))
                itemStack.Attributes[TransientUtil.ATTR_STATE] = new TreeAttribute();

            ITreeAttribute attr = (ITreeAttribute)itemStack.Attributes[TransientUtil.ATTR_STATE];


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

                attr.SetInt(TransientUtil.ATTR_CURR_STATE, (int)lightState);
                attr.SetDouble(TransientUtil.ATTR_CURR_HOURS, currentFuelHours);
                attr.SetDouble(TransientUtil.ATTR_CURR_DEPLETION, currentDepletionMul);
            }
            else
            {
                lightState = (EnumLightState)attr.GetInt(TransientUtil.ATTR_CURR_STATE);
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
                    RekindledMain.sapi.Logger.Notification("Fuel: " + Math.Round(currentFuelHours, 2) + " -> " + Math.Round(currentFuelHours - hoursPassedAdjusted, 2));
                    currentFuelHours -= hoursPassedAdjusted;
                    attr.SetDouble(TransientUtil.ATTR_CURR_HOURS, currentFuelHours);
                }

                if (currentFuelHours <= 0 && world.Side == EnumAppSide.Server) // perform transition to burnedout state
                {
                    ItemStack newStack = behavior.OnTransitionStack(inslot, EnumLightState.Burnedout);

                    inslot.Itemstack.SetFrom(newStack);

                    behavior = inslot.Itemstack.Block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
                    attr = (ITreeAttribute)inslot.Itemstack.Attributes[TransientUtil.ATTR_STATE];

                    attr.SetInt(TransientUtil.ATTR_CURR_STATE, (int)behavior.GetLightState());
                    attr.SetInt(TransientUtil.ATTR_CURR_HOURS, 0);

                    transitionStack = inslot.Itemstack;

                    inslot.MarkDirty();
                }
            }

            if (hoursPassed > 0.05f)
                attr.SetDouble(TransientUtil.ATTR_UPDATED_HOURS, currentTotalHours);

            return transitionStack;
        }
    }
}
