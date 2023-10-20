﻿using System;
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

using HarmonyLib;

namespace Rekindled.src
{
    /*
     * TODO:
     * 
     * edit light source info to read:
     *  State: lightState
     *  Fuel: x%
     *  Time Remaining: x time (copy syntax of food spoilage)
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
     * ticking fuel
     *  inventory
     *  placed
     *  dropped itementity
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
            api.RegisterBlockBehaviorClass("blockbehaviortransientlight", typeof(BlockBehaviorTransientLight));
            api.RegisterBlockEntityBehaviorClass("bebehaviortransientlight", typeof(BEBehaviorTransientLight));
            api.RegisterCollectibleBehaviorClass("collectibleBehaviorTLDescription", typeof(CollectibleBehaviorTLDescription));

            // apply harmony patches
            harmony.PatchAll();

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
                    // block.BlockEntityBehaviors = block.BlockEntityBehaviors.Append(bebehavior);
                }
            }

            //foreach (Item item in api.World.Items) // TODO: apply behaviors to item transient lights
            //{
            //    if (item.Code == null)
            //        continue;

            //    if (item.Code.FirstPathPart == "candle")
            //        // add behavior to item
            //}
        }


        bool IsBlockTransientLight(Block block)
        {
            if (block.Code.Path.Contains("torch-basic")
                || block.Code.Path.Contains("torch-crude")
                || block.Code.Path.Contains("torch-cloth")
                || block.Code.FirstCodePart() == "bunchocandles"
                || block.Code.FirstCodePart() == "lantern"
                || block.Code.FirstCodePart() == "oillamp")
                return true;
            else if (block.Code.SecondCodePart() == "torchholder" && block.Code.Path.Contains("filled"))
                return true;

            return false;
        }


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
                    .WithDescription("adjust the transientState attributes of a transientlight object in hand")
                    .HandleWith(OnCmdSetAttr)
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


        bool IsSlotTransientLight(ItemSlot slot)
        {
            Block block = slot.Itemstack.Block;
            if (block == null)
                return false;

            var behavior = block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;
            if (behavior == null)
                return false;

            return true;
        }


        public override void Dispose()
        {
            base.Dispose();

            if (harmony != null)
                harmony.UnpatchAll();
        }
    }
}