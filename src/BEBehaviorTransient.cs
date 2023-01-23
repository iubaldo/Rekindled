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
    // This class allows for a block to transition to another form on a timely basis

    class BEBehaviorTransient : BlockEntityTransient
    {
        int fuel = 0;

        // transfer fuel to dropped block
        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
        }

        // transfer fuel from itemStack.Block.Attributes
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
        }
    }
}
