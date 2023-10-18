using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace Rekindled.src
{
    public class TransientLightProps
    {
        public double MaxFuelHours;
        public double BaseDepletionMul = 1;  // modifies how quickly fuel depletes


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
        public double LastUpdatedTotalHours = 0;
        public double CurrentFuelHours;
        public double CurrentDepletionMul;


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
