using Rekindled.src.Behaviors;

using System.Text;

using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Rekindled.src
{
    internal class RekindledCommands
    {
        static ICoreServerAPI sapi;
        
        internal static void RegisterCommands(ICoreServerAPI serverApi)
        {
            sapi = serverApi;
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

                .BeginSubCommand("getBEBehaviors")
                    .WithDescription("Displays the blockbehaviors/bebehaviors of the block the player is looking at, if any")
                    .HandleWith(OnCmdGetBEBlockBehaviors)
                .EndSubCommand()
                ;
        }


        static TextCommandResult OnCmdUpdateBlockEntity(TextCommandCallingArgs args)
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


        static TextCommandResult OnCmdUpdateBlockInHand(TextCommandCallingArgs args)
        {
            ItemSlot slot = args.Caller.Player.InventoryManager.ActiveHotbarSlot;

            if (!TransientUtil.IsSlotTransientLight(slot))
                return TextCommandResult.Error("Error, current slot does not contain a transientLight.");

            Block block = slot.Itemstack.Block;
            var behavior = block.GetBehavior(typeof(BlockBehaviorTransientLight), false) as BlockBehaviorTransientLight;

            behavior.TryBlockTransition(EnumLightState.Burnedout, slot);

            return TextCommandResult.Success("DebugUpdateBlockInHand: performed transition for " + block.Code);
        }


        static TextCommandResult OnCmdUpdateBlocksAround(TextCommandCallingArgs args)
        {
            sapi.SendMessage(args.Caller.Player, args.Caller.FromChatGroupId, "DebugUpdateBlockEntity: attempting transition around...", EnumChatType.Notification);

            bool foundTarget = false;
            foreach (Entity entity in sapi.World.GetEntitiesAround(args.Caller.Player.Entity.SidedPos.XYZ, 32, 32, TransientUtil.IsEntityItemTransientLight))
            {
                var entityItem = entity as EntityItem;
                TransientUtil.TryTransitionEntityItem(entityItem.Slot);

                string message = "DebugUpdateBlocksAround: @{0}, performed transition for " + entityItem.Itemstack.Block.Code.ToShortString();
                sapi.SendMessage(args.Caller.Player, args.Caller.FromChatGroupId, message, EnumChatType.Notification);

                foundTarget = true;
            }

            if (!foundTarget)
                return TextCommandResult.Error("DebugUpdateBlocksAround: could not find valid targets");

            return TextCommandResult.Success();
        }


        static TextCommandResult OnCmdGetAttr(TextCommandCallingArgs args)
        {
            ItemSlot slot = args.Caller.Player.InventoryManager.ActiveHotbarSlot;

            if (!TransientUtil.IsSlotTransientLight(slot))
                return TextCommandResult.Error("Error, current slot does not contain a transientLight.");

            Block block = slot.Itemstack.Block;

            return TextCommandResult.Success(block.Code.Path + ":\n" + slot.Itemstack.Attributes.ToJsonToken());
        }


        static TextCommandResult OnCmdSetAttr(TextCommandCallingArgs args)
        {
            ItemSlot slot = args.Caller.Player.InventoryManager.ActiveHotbarSlot;

            if (!TransientUtil.IsSlotTransientLight(slot))
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

            sapi.Logger.Notification("Set attribute " + attributeName + " to " + value);

            return TextCommandResult.Success();
        }


        static TextCommandResult OnCmdGetBEState(TextCommandCallingArgs args)
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


        static TextCommandResult OnCmdGetBEBlockBehaviors(TextCommandCallingArgs args)
        {
            BlockSelection blockSel = args.Caller.Player.CurrentBlockSelection;
            if (blockSel == null)
                return TextCommandResult.Error("Error, not currently selecting a block");

            BlockEntity be = args.Caller.Player.Entity.World.BlockAccessor.GetBlockEntity(blockSel.Position);

            if (be.GetBehavior<BEBehaviorTransientLight>() == null)
                return TextCommandResult.Error("Error, selected block is not a transient light");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(be.Block.Code.ToShortString() + " BlockBehaviors:");
            foreach (var behavior in be.Block.BlockBehaviors)
                sb.AppendLine("\t" + behavior.GetType().Name);


            sb.AppendLine("\n" + be.Block.Code.ToShortString() + " BlockEntityBehaviors:");
            foreach (var behavior in be.Block.BlockEntityBehaviors)
                sb.AppendLine("\t" + behavior.GetType().Name);


            sapi.Logger.Notification(sb.ToString());

            return TextCommandResult.Success();
        }
    }
}
