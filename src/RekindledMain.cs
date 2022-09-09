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

namespace rekindled.src
{
    class RekindledMain : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            System.Diagnostics.Debug.WriteLine("LOADING REKINDLED MAIN");

            api.RegisterBlockClass("blockrekindledtorch", typeof(BlockRekindledTorch));
            api.RegisterBlockEntityClass("blockentityextinguishable", typeof(BlockEntityExtinguishable));
        }
    }
}