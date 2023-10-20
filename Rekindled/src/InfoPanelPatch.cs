using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using Vintagestory.GameContent;


namespace Rekindled.src
{
    //[HarmonyPatch(typeof(BlockTorch), "GetHeldItemInfo")]
    //public class InfoPanelPatch
    //{
    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //    {
    //        var codes = new List<CodeInstruction>(instructions);
    //        for (int i = 0; i < codes.Count; i++) 
    //        {
    //            if (codes[i].opcode == OpCodes.Brfalse_S)
    //            {
    //                codes[i].opcode = OpCodes.Br; // skip straight to return to skip appending additional info text
    //                break;
    //            }  
    //        }

    //        return codes;
    //    }
    //}
}
