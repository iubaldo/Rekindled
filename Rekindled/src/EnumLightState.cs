using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rekindled.src.Behaviors;
using Vintagestory.API.Common;

namespace Rekindled.src
{
    public enum EnumLightState
    {
        Lit,
        Unlit,
        Extinct,
        Burnedout
    }


    public static class LightStateUtil
    {
        public static string GetName(this EnumLightState state) => Enum.GetName(typeof(EnumLightState), state);

        public static EnumLightState GetLightState(this BlockBehaviorTransientLight behavior)
        {
            Enum.TryParse(behavior.block.Variant["state"], true, out EnumLightState lightState);
            return lightState;
        }


        public static bool IsValidFuelItem(this TransientLightProps props, CollectibleObject obj) => props.FuelItems.Contains(obj.Code.ToString());
    }
}
