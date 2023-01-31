using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace rekindled.src
{
    public enum EnumLightState
    {
        lit,            // Light source is currently consuming fuel, unstackable
        unlit,          // "Fresh" light source that hasn't yet been lit/used, stackable
        extinguished,   // "Used" light source, isn't currently consuming fuel, unstackable
        burntout       // No fuel remaining, stackable
    }


    public class TransientLightProperties
    {
        public EnumLightState LightState;

        public float TimeLastChecked;

        public readonly float MaxFuel;
        public float CurrentFuel;

        public readonly float BaseDepletionMul; // modifies how quickly fuel depletes
        public float CurrentDepletionMul;
    }
}
