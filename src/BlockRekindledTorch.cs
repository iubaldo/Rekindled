using System;
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

namespace rekindled.src
{
    class BlockRekindledTorch : BlockGroundAndSideAttachable
    {
        EnumLitState litState;

        Dictionary<string, Cuboidi> attachmentAreas;

        WorldInteraction[] interactions;

        public Block UnlitVariant { get; private set; } 
        public Block ExtinguishedVariant { get; private set; } 


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            switch (Variant["state"])
            {
                case "lit": litState = EnumLitState.Lit; break;                         // torch is currently being burned, durability steadily decreases
                case "unlit": litState = EnumLitState.Unlit; break;                     // "fresh" torch, hasn't been lit yet, stackable
                case "extinguished": litState = EnumLitState.Extinguished; break;       // "used" torch, not burnt out and still has fuel, no longer stackable
                case "burnedout": litState = EnumLitState.BurnedOut; break;             // torch is fully burnt out, no fuel remaining
                default: litState = EnumLitState.Unlit; break;
            }

            // copied from base
            if (Attributes == null)
                return;

            var areas = Attributes["attachmentAreas"].AsObject<Dictionary<string, RotatableCube>>(null);
            if (areas != null)
            {
                attachmentAreas = new Dictionary<string, Cuboidi>();
                foreach (var val in areas)
                {
                    val.Value.Origin.Set(8, 8, 8);
                    attachmentAreas[val.Key] = val.Value.RotatedCopy().ConvertToCuboidi();
                }
            }

            // setup variants
            if (Variant.ContainsKey("state"))
            {
                AssetLocation loc = CodeWithVariant("state", "unlit");
                UnlitVariant = api.World.GetBlock(loc);

                loc = CodeWithVariant("state", "extinguished");
                ExtinguishedVariant = api.World.GetBlock(loc);
            }

            if (litState == EnumLitState.Unlit)
            {
                interactions = ObjectCacheUtil.GetOrCreate(api, "torchInteractions" + FirstCodePart(), () =>
                {
                    List<ItemStack> canIgniteStacks = new List<ItemStack>();

                    foreach (CollectibleObject obj in api.World.Collectibles)
                    {
                        string firstCodePart = obj.FirstCodePart();

                        if (obj is Block && (obj as Block).HasBehavior<BlockBehaviorCanIgnite>() || obj is ItemFirestarter)
                        {
                            List<ItemStack> stacks = obj.GetHandBookStacks(api as ICoreClientAPI);
                            if (stacks != null) canIgniteStacks.AddRange(stacks);
                        }
                    }

                    return new WorldInteraction[]
                    {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-firepit-ignite",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak",
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            return wi.Itemstacks;
                        }
                    }
                    };
                });
            }
        }


        // handle extinguishing in when held in water
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (api.World.Side == EnumAppSide.Server && byEntity.Swimming && litState == EnumLitState.Lit && ExtinguishedVariant != null)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), byEntity.Pos.X + 0.5, byEntity.Pos.Y + 0.75, byEntity.Pos.Z + 0.5, null, false, 16);

                int q = slot.Itemstack.StackSize;
                slot.Itemstack = new ItemStack(ExtinguishedVariant);
                slot.Itemstack.StackSize = q;
                slot.MarkDirty();
            }
        }


        // handle extinguishing when in item form in the world, and dropped in water
        public override void OnGroundIdle(EntityItem entityItem)
        {
            if (litState == EnumLitState.Lit && entityItem.Swimming && ExtinguishedVariant != null)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), entityItem.Pos.X + 0.5, entityItem.Pos.Y + 0.75, entityItem.Pos.Z + 0.5, null, false, 16);

                int q = entityItem.Itemstack.StackSize;
                entityItem.Itemstack = new ItemStack(ExtinguishedVariant);
                entityItem.Itemstack.StackSize = q;
            }
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (Variant["state"] == "burnedout") return new ItemStack[0]; // burned out torches drop nothing

            Block block = world.BlockAccessor.GetBlock(CodeWithVariant("orientation", "up"));
            return new ItemStack[] { new ItemStack(block) };
        }


        // when this block is being ignited, not being used to ignite
        public override EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            if (Variant["state"] == "burnedout") return EnumIgniteState.NotIgnitablePreventDefault;

            if (litState == EnumLitState.Unlit || litState == EnumLitState.Extinguished) return secondsIgniting > 1 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;

            return base.OnTryIgniteBlock(byEntity, pos, secondsIgniting);
        }


        // attempt is complete, check if successfully lit
        public override void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            var block = api.World.GetBlock(CodeWithVariant("state", "lit"));
            if (block != null)
            {
                api.World.BlockAccessor.SetBlock(block.Id, pos);
            }
        }


        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot itemslot)
        {
            base.OnAttackingWith(world, byEntity, attackedEntity, itemslot);

            float stormstr = api.ModLoader.GetModSystem<SystemTemporalStability>().StormStrength;

            if (litState == EnumLitState.Lit && attackedEntity != null && byEntity.World.Side == EnumAppSide.Server && api.World.Rand.NextDouble() < 0.1 + stormstr)
            {
                attackedEntity.Ignite();
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(interactions);
        }
    }
}
