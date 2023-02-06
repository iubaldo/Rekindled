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
        burnedout       // No fuel remaining, stackable
    }


    // immutable properties read from JSON
    public class TransientLightProperties
    {        
        public float MaxFuelHours;      
        public float BaseDepletionMul; // modifies how quickly fuel depletes        

        public string ToString()
        {
            return "MaxFuelHours: " + MaxFuelHours +
                   "\nBaseDepletionMul: " + BaseDepletionMul;
        }
    }

    
    // mutable state for this specific instance, read from and written to treeattributes
    public class TransientLightState 
    {
        public EnumLightState LightState;
        public float TimeLastChecked = 0;
        public float CurrentFuelHours = -1;
        public float CurrentDepletionMul = 1;

        public TransientLightState(TransientLightProperties props)
        {
            CurrentFuelHours = props.MaxFuelHours;
            CurrentDepletionMul = props.BaseDepletionMul;
        }

        public string ToString()
        {
            return "EnumLightState: " + Enum.GetName(typeof(EnumLightState), LightState) +
                   "\nTimeLastChecked: " + TimeLastChecked +
                   "\nCurrentFuelHours: " + CurrentFuelHours +
                   "\nCurrentDepletionMul: " + CurrentDepletionMul;
        }
    }

}
