using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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


        public TransientLightState(TransientLightProps props, ITreeAttribute tree)
        {
            Props = props;

            LightState = (EnumLightState)tree.GetInt(TransientUtil.ATTR_CURR_LIGHTSTATE);
            LastUpdatedTotalHours = tree.GetDouble(TransientUtil.ATTR_UPDATED_HOURS);
            CurrentFuelHours = tree.GetDouble(TransientUtil.ATTR_CURR_HOURS);
            CurrentDepletionMul = tree.GetDouble(TransientUtil.ATTR_CURR_DEPLETION);
            CreatedTotalHours = tree.GetDouble(TransientUtil.ATTR_CREATED_HOURS);
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
        public const string ATTR_TRANSIENTSTATE = "transientState"; // name of the actual attribute containing the state

        public const string ATTR_CREATED_HOURS = "createdTotalHours";
        public const string ATTR_UPDATED_HOURS = "lastUpdatedTotalHours";
        public const string ATTR_CURR_LIGHTSTATE = "currentLightState";
        public const string ATTR_CURR_HOURS = "currentFuelHours";
        public const string ATTR_CURR_DEPLETION = "currentDepletionMul";
    }
}
