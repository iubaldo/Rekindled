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

namespace Rekindled.src
{
    /*
     * TODO:
     * 
     * Adjust lang file to use {0} placeholder text for showing max fuel
     *  
     * patch-add appropriate light sources with state + variants
     *  change recipes to default craft as unlit
     *  reenable unused torches and recipes
     *  add state for lanterns depending on fuel source
     *      animal fat
     *      tallow candle
     *      wax candle
     *      vegetable oil (expandedfoods compat)
     * 
     * see if it's possible to combine and average fuel time for extinguished light sources
     *  similar to how decay works on food
     * 
     * interactions
     *  add fuel
     *      look at quern, right click with item to insert, right click when empty to grind
     *  extinguish
     *      convert to extinguished form
     *  light
     *      look at other mod that lets torches light other torches
     *      also recipe to convert sources to lit via crafting
     *  
     *  new firestarters
     *  
     *  fix extinguish transitions for rain/submerge
     *  
     *  reduce depletion mul if not in hand/hotbar
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
                harmony.Unpatch(typeof(BELantern).GetMethod(nameof(BELantern.GetBlockInfo)), HarmonyPatchType.Postfix);

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

            RegisterCommands();
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

            api.Logger.StoryEvent("[DEBUG] Loading Rekindled...");
        }


        void AddBehaviors(ICoreAPI api)
        {
            foreach (Block block in api.World.Blocks)
            {
                if (block.Code != null && IsBlockTransientLight(block))
                {
                    block.BlockBehaviors = block.BlockBehaviors.Append(new BlockBehaviorTransientLight(block));
                    block.CollectibleBehaviors = block.CollectibleBehaviors.Append(new CollectibleBehaviorTLDescription(block));

                    var bebehavior = new BlockEntityBehaviorType
                    {
                        Name = "bebehaviortransientlight",
                        properties = null
                    };
                    block.BlockEntityBehaviors = block.BlockEntityBehaviors.Append(bebehavior);
                    if (block.Code.FirstCodePart() == "oillamp2")
                        continue;
                }
            }

            foreach (Item item in api.World.Items) // TODO: apply behaviors to item transient lights
            {
                if (item.Code == null)
                    continue;

                if (item.Code.Path == "fat")
                    item.CollectibleBehaviors = item.CollectibleBehaviors.Append(new CollectibleBehaviorFuelItem(item));
            }
        }


        public static bool IsBlockTransientLight(Block block)
        {
            if (block.Code.Path.Contains("torch-basic")
                || block.Code.Path.Contains("torch-crude")
                || block.Code.Path.Contains("torch-cloth")
                || block.Code.FirstCodePart() == "bunchocandles"
                || block.Code.FirstCodePart() == "lantern"
                || block.Code.FirstCodePart() == "oillamp"
                || block.Code.FirstCodePart() == "oillamp2")
                return true;
            else if (block.Code.SecondCodePart() == "torchholder" && block.Code.Path.Contains("filled"))
                return true;

            return false;
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


        // TODO: move command registry to seperate file
        void RegisterCommands()
        {
            CommandArgumentParsers parsers = sapi.ChatCommands.Parsers;

            sapi.ChatCommands
                .GetOrCreate("rekindled")
                .IgnoreAdditionalArgs()
                .RequiresPrivilege("worldedit")
                .WithDescription("Rekindled Mod debug commands")

                .BeginSubCommand("transitionBE")
                    .WithDescription("Attempt to transition the block you're currently looking at.")
                    .HandleWith(OnCmdUpdateBlockEntity)
                .EndSubCommand()

                .BeginSubCommand("transitionHand")
                    .WithDescription("Attempt to transition the block in your main hand, if any.")
                    .HandleWith(OnCmdUpdateBlockInHand)
                .EndSubCommand()

                .BeginSubCommand("transitionAround")
                    .WithDescription("Attempt to transition entityItems in a radius around you.")
                    .HandleWith(OnCmdUpdateBlocksAround)
                .EndSubCommand()

                .BeginSubCommand("getAttr")
                    .WithDescription("display ITreeAttributes of transientlight object in hand, if any.")
                    .HandleWith(OnCmdGetAttr)
                .EndSubCommand()

                .BeginSubCommand("setAttr")
                    .WithArgs(parsers.WordRange("attribute",
                                                "createdTotalHours", "lastUpdatedTotalHours", "currentLightState", "currentFuelHours", "currentDepletionMul"),
                              parsers.Double("value"))
                    .WithDescription("Adjust the transientState attributes of a transientlight object in hand, if any.")
                    .HandleWith(OnCmdSetAttr)
                .EndSubCommand()

                .BeginSubCommand("getBEState")
                    .WithDescription("Displays the state of the currently selected blockentity, if it is a transient light.")
                    .HandleWith(OnCmdGetBEState)
                .EndSubCommand()
                ;
        }


        TextCommandResult OnCmdUpdateBlockEntity(TextCommandCallingArgs args)
        {
            System.Diagnostics.Debug.WriteLine("DebugUpdateBlockEntity: attempting transition on block entity...");

            BlockEntity entity = sapi.World.BlockAccessor.GetBlockEntity(args.Caller.Player.CurrentBlockSelection.Position);

            if (entity.Block.Attributes == null || entity.Block.Attributes["transientLightProps"].Exists == false)
            {
                return TextCommandResult.Error("DebugUpdateBlockEntity: @" + entity.Pos + ", could not find transientLightProps for @" + entity.Block.Code.ToShortString());
            }

            var behavior = entity.GetBehavior<BEBehaviorTransientLight>();
            if (behavior == null)
            {
                return TextCommandResult.Error("@" + entity.Pos + ", could not find bebehaviortransientlight for @" + entity.Block.Code.ToShortString());
            }

            // behavior.TryBETransition(EnumLightState.unlit);
            System.Diagnostics.Debug.WriteLine("DebugUpdateBlockEntity: @{0}, perfomed transition for @{1}", entity.Pos, entity.Block.Code.ToShortString());

            return TextCommandResult.Success();
        }


        TextCommandResult OnCmdUpdateBlockInHand(TextCommandCallingArgs args)
        {
            ItemSlot slot = args.Caller.Player.InventoryManager.ActiveHotbarSlot;

            if (!IsSlotTransientLight(slot))
                return TextCommandResult.Error("Error, current slot does not contain a transientLight.");

            Block block = slot.Itemstack.Block;
            var behavior = block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;

            behavior.TryBlockTransition(EnumLightState.Burnedout, slot);

            return TextCommandResult.Success("DebugUpdateBlockInHand: performed transition for " + block.Code);
        }


        TextCommandResult OnCmdUpdateBlocksAround(TextCommandCallingArgs args)
        {
            sapi.SendMessage(args.Caller.Player, args.Caller.FromChatGroupId, "DebugUpdateBlockEntity: attempting transition around...", EnumChatType.Notification);

            bool foundTarget = false;
            foreach (Entity entity in sapi.World.GetEntitiesAround(args.Caller.Player.Entity.SidedPos.XYZ, 32, 32, IsEntityItemTransientLight))
            {
                var entityItem = entity as EntityItem;
                TryTransitionEntityItem(entityItem.Slot);

                string message = "DebugUpdateBlocksAround: @{0}, performed transition for " + entityItem.Itemstack.Block.Code.ToShortString();
                sapi.SendMessage(args.Caller.Player, args.Caller.FromChatGroupId, message, EnumChatType.Notification);

                foundTarget = true;
            }

            if (!foundTarget)
                return TextCommandResult.Error("DebugUpdateBlocksAround: could not find valid targets");

            return TextCommandResult.Success();
        }


        TextCommandResult OnCmdGetAttr(TextCommandCallingArgs args)
        {
            ItemSlot slot = args.Caller.Player.InventoryManager.ActiveHotbarSlot;

            if (!IsSlotTransientLight(slot))
                return TextCommandResult.Error("Error, current slot does not contain a transientLight.");

            Block block = slot.Itemstack.Block;

            return TextCommandResult.Success(block.Code.Path + ":\n" + slot.Itemstack.Attributes.ToJsonToken());
        }


        TextCommandResult OnCmdSetAttr(TextCommandCallingArgs args)
        {
            ItemSlot slot = args.Caller.Player.InventoryManager.ActiveHotbarSlot;

            if (!IsSlotTransientLight(slot))
                return TextCommandResult.Error("Error, current slot does not contain a transientLight.");

            ITreeAttribute attr = (ITreeAttribute)slot.Itemstack.Attributes["transientState"];

            double value = (double)args[1];
            string attributeName = (string)args[0];
            switch (attributeName)
            {
                case "createdTotalHours":
                    attr.SetDouble(attributeName, value); break;
                case "lastUpdatedTotalHours":
                    attr.SetDouble(attributeName, value); break;
                case "currentLightState":
                    attr.SetInt(attributeName, (int)value); break;
                case "currentFuelHours":
                    attr.SetDouble(attributeName, value); break;
                case "currentDepletionMul":
                    attr.SetDouble(attributeName, value); break;
            }
            slot.MarkDirty();

            return TextCommandResult.Success();
        }


        TextCommandResult OnCmdGetBEState(TextCommandCallingArgs args)
        {
            BlockSelection blockSel = args.Caller.Player.CurrentBlockSelection;
            if (blockSel == null)
                return TextCommandResult.Error("Error, not currently selecting a block");

            BlockEntity be = args.Caller.Player.Entity.World.BlockAccessor.GetBlockEntity(blockSel.Position);

            if (be.GetBehavior<BEBehaviorTransientLight>() == null)
                return TextCommandResult.Error("Error, selected block is not a transient light");

            sapi.Logger.Notification(be.GetBehavior<BEBehaviorTransientLight>().State.ToString());

            return TextCommandResult.Success();
        }


        // TODO: move these to a seperate util class
        void TryTransitionEntityItem(ItemSlot slot)
        {
            var behavior = slot.Itemstack.Block.GetBehavior(typeof(BlockBehaviorTransientLight)) as BlockBehaviorTransientLight;
            behavior.TryBlockTransition(EnumLightState.Burnedout, slot);
        }


        bool IsEntityItemTransientLight(Entity entity)
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


        public static TransientLightProps ResolvePropsFromBlock(Block block)
        {
            if (block.Attributes == null)
                return null;
            if (!block.Attributes["transientLightProps"].Exists)
                return null;

            return block.Attributes["transientLightProps"].AsObject<TransientLightProps>();
        }


        public override void Dispose()
        {
            base.Dispose();

            if (harmony != null)
                harmony.UnpatchAll();
        }
    }
}