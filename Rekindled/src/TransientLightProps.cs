using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rekindled.src
{
    public class TransientLightProps
    {
        public float MaxFuelHours;
        public float BaseDepletionMul = 1;  // modifies how quickly fuel depletes


        public override string ToString()
        {
            return "MaxFuelHours: " + MaxFuelHours +
                  "\nBaseDepletionMul: " + BaseDepletionMul;
        }
    }


    public class TransientLightState
    {
        TransientLightProps Props;
        public EnumLightState LightState;
        public float TimeLastChecked = 0;
        public float CurrentFuelHours;
        public float CurrentDepletionMul;


        public TransientLightState(TransientLightProps props)
        {
            Props = props;
            CurrentFuelHours = props.MaxFuelHours;
            CurrentDepletionMul = props.BaseDepletionMul;
        }


        public override string ToString()
        {
            return "State: " + Enum.GetName(typeof(EnumLightState), LightState) +
                   "\nFuel: " + Math.Round(CurrentFuelHours / Props.MaxFuelHours * 100.0, 2) + "%" +
                   "\nTime Remaining: " + Math.Round(CurrentFuelHours, 2);
        }
    }

}
