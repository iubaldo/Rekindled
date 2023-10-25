using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

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


        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(MaxFuelHours);
            writer.Write(BaseDepletionMul);
        }


        public void FromBytes(BinaryReader reader, IClassRegistryAPI instancer)
        {
            MaxFuelHours = reader.ReadDouble();
            BaseDepletionMul = reader.ReadDouble();
        }
    }


    public class TransientLightState
    {
        public TransientLightProps Props;

        public EnumLightState LightState;
        public double LastUpdatedTotalHours;
        public double CurrentFuelHours;
        public double CurrentDepletionMul;
        public double CreatedTotalHours;


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


    public class TransientUtil
    {
        public const string ATTR_STATE = "transientState"; // name of the actual attribute containing the state

        public const string ATTR_CREATED_HOURS = "createdTotalHours";
        public const string ATTR_UPDATED_HOURS = "lastUpdatedTotalHours";
        public const string ATTR_CURR_STATE = "currentLightState";
        public const string ATTR_CURR_HOURS = "currentFuelHours";
        public const string ATTR_CURR_DEPLETION = "currentDepletionMul";
    }
}
