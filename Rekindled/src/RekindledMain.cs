using System;
using System.Linq;
using System.Text;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Util;

using HarmonyLib;
using Rekindled.src.Behaviors;
using Rekindled.src.Blocks;

namespace Rekindled.src
{
    /*
     * TODO:
     * 
     * Adjust lang file to use {0} placeholder text for showing max fuel
     * 
     * interactions
     *  extinguish
     *      convert to extinguished form
     *  light source from other sources
     *      this is vanilla now, just make sure to reproduce functionality on other light sources
     *      
     *  crafting
     *      refuel items/stacks via crafting
     *          should require more fuel the more are in the stack, otherwise one item could refuel multiple sources
     *          use copyattributes, but change to different variant
     *      new idea: create a new fuel source item crafted from animal fat/oil/whatever, and use those to refuel instead
     *          each light source would take 1 portion of the appropriate fuel to convert extinct/burnedout -> unlit with full fuel
     *          or, the fuel portion has a durability bar and takes damage whenever it's used to refuel a light source
     *              functionally the same, but might feel better than having 100 portion items clogging the inventory
     *      
     *  fix extinguish transitions for rain/submerge
     *      also extinguish all lit sources in inventory when player is submerged
     *      except lanterns
     *  
     *  reduce depletion mul if not in hand/hotbar
     *  player emit light while a lit source is in inventory
     *  
     *  Bugs:
     *      model doesn't transition correctly? -> might be an old one, need to retest
     *  
     *      
     * After 1.0 release:
     *  new light sources
     *      rushlights
     *      alternate fuel lamps/lanterns (wax, fat, plant-based oils)
     *  new firestarters + related items
     *      bow drill
     *      pump drill
     *  endgame items
     *      temporal sensor
     *      temporal lantern
     *      temporal candle
     *      vacuous brazier
     *  minor features
     *      lantern latch
     *  add state for lanterns depending on fuel source
     *      animal fat
     *      tallow candle
     *      wax candle
     *      vegetable oil (expandedfoods compat)
     *  
     *  see if it's possible to combine and average fuel time for extinguished light sources
     *  similar to how decay works on food
     */


    public class RekindledMain : ModSystem
    {
        const string PATCH_CODE = "Landar.Rekindled.RekindledMain";

        internal static ICoreServerAPI sapi;
        ICoreClientAPI capi;

        public static Harmony harmony = new Harmony(PATCH_CODE);


        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            System.Diagnostics.Debug.WriteLine("Loading Rekindled...");

            // register behaviors
            api.RegisterBlockEntityClass("blockentitymock", typeof(BlockEntityMock));
            api.RegisterBlockBehaviorClass("blockbehaviortransientlight", typeof(BlockBehaviorTransientLight));
            api.RegisterBlockEntityBehaviorClass("bebehaviortransientlight", typeof(BEBehaviorTransientLight));
            api.RegisterCollectibleBehaviorClass("collectibleBehaviorTLDescription", typeof(CollectibleBehaviorTLDescription));
            api.RegisterCollectibleBehaviorClass("collectibleBehaviorFuelItem", typeof(CollectibleBehaviorFuelItem));

            // apply harmony patches
            harmony.PatchAll();

            // only do this clientside
            if (api.Side == EnumAppSide.Server)
            {
                harmony.Unpatch(typeof(BELantern).GetMethod(nameof(BELantern.GetBlockInfo)), HarmonyPatchType.Postfix);
            }

            // only do this serverside
            if (api.Side == EnumAppSide.Server)
            {
                harmony.Unpatch(typeof(BlockEntityTransient).GetMethod(nameof(BlockEntityTransient.OnBlockPlaced)), HarmonyPatchType.Prefix);
            }

            // log patched methods
            if (!harmony.GetPatchedMethods().Any())
                Mod.Logger.Notification("No Harmony Patches were applied.");
            else
            {
                StringBuilder builder = new StringBuilder("Harmony Patched Methods: ");
                foreach (var val in harmony.GetPatchedMethods())
                    builder.Append(val.Name + ", ");

                Mod.Logger.Notification(builder.ToString());
            }
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(sapi);

            sapi = api;

            RekindledCommands.RegisterCommands(sapi);
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;
        }


        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);

            if (api.Side == EnumAppSide.Server)
                AddBehaviors(api);

            api.Logger.StoryEvent("The flames in the dark...");
        }


        void AddBehaviors(ICoreAPI api)
        {
            foreach (Block block in api.World.Blocks)
            {
                if (block.Code != null && TransientUtil.IsBlockTransientLight(block))
                {
                    block.BlockBehaviors = block.BlockBehaviors.Append(new BlockBehaviorTransientLight(block));
                    block.CollectibleBehaviors = block.CollectibleBehaviors.Append(new CollectibleBehaviorTLDescription(block));

                    var bebehavior = new BlockEntityBehaviorType
                    {
                        Name = "bebehaviortransientlight",
                        properties = null
                    };
                    block.BlockEntityBehaviors = block.BlockEntityBehaviors.Append(bebehavior);
                }
            }

            foreach (Item item in api.World.Items) // TODO: apply behaviors to item transient lights
            {
                if (item.Code == null)
                    continue;

                if (item.Code.Path == "fat") // TODO: loop through all fuel items from props to add behaviors dynamically
                    item.CollectibleBehaviors = item.CollectibleBehaviors.Append(new CollectibleBehaviorFuelItem(item));
            }
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


        public override void Dispose()
        {
            base.Dispose();

            if (harmony != null)
                harmony.UnpatchAll();
        }
    }
}