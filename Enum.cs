using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEngine.Assertions;
using System.Reflection.Emit;
using HarmonyLib.Tools;

namespace ElinOpt;

// Chara.Hostility.get
[HarmonyPatch(typeof(Chara), nameof(Chara.hostility), MethodType.Getter)]
class CharaHostilityPatch
{
    static bool Prefix(Chara __instance, ref Hostility __result)
    {
        /* origin code:
         * return this._cints[4].ToEnum<Hostility>();
         */
        switch(__instance._cints[4])
        {
            case 1:
                __result = Hostility.Enemy;
                break;
            case 3:
                __result = Hostility.Neutral;
                break;
            case 5:
                __result = Hostility.Friend;
                break;
            case 8:
                __result = Hostility.Ally;
                break;
            default:
                __result = (Hostility)__instance._cints[4];
                break;
        }
        return false;
    }
}

// Card.c_originalHostility.get
[HarmonyPatch(typeof(Card), nameof(Card.c_originalHostility), MethodType.Getter)]
class CharaCHostilityPatch
{
    static bool Prefix(Chara __instance, ref Hostility __result)
    {
        /* origin code:
         * return base.GetInt(12, null).ToEnum<Hostility>();
         */
        switch(__instance.GetInt(12, null))
        {
            case 1:
                __result = Hostility.Enemy;
                break;
            case 3:
                __result = Hostility.Neutral;
                break;
            case 5:
                __result = Hostility.Friend;
                break;
            case 8:
                __result = Hostility.Ally;
                break;
            default:
                __result = (Hostility)__instance.GetInt(12, null);
                break;
        }
        return false;
    }
}

// Chara.OriginalHostility.get
[HarmonyPatch(typeof(Chara), nameof(Chara.OriginalHostility), MethodType.Getter)]
class CharaOriginalHostilityPatch
{
    static bool Prefix(Chara __instance, ref Hostility __result)
    {
        /* origin code:
			if (EClass.pc != null && this.IsPCFaction)
			{
				return Hostility.Ally;
			}
			if (base.c_originalHostility != (Hostility)0)
			{
				return base.c_originalHostility;
			}
			if (!this.source.hostility.IsEmpty())
			{
				return this.source.hostility.ToEnum(true);
			}
			return Hostility.Enemy;
         */
        if (EClass.pc != null && __instance.IsPCFaction)
        {
            __result = Hostility.Ally;
            return false;
        }
        if (__instance.GetInt(12, null) != 0)
        {
            __result = __instance.c_originalHostility;
            return false;
        }
        if (!__instance.source.hostility.IsEmpty())
        {
            switch(__instance.source.hostility[0])
            {
                case 'E':
                    __result = Hostility.Enemy;
                    break;
                case 'N':
                    __result = Hostility.Neutral;
                    break;
                case 'F':
                    __result = Hostility.Friend;
                    break;
                case 'A':
                    __result = Hostility.Ally;
                    break;
                default:
                    __result = Hostility.Enemy;
                    break;
            }
            return false;
        }
        __result = Hostility.Enemy;
        return false;
    }
}

[HarmonyPatch]
// GameUpdater.SensorUpdater.FixedUpdate
class SensorUpdaterPatch
{
    /*
    Reference:
	public bool IsHostile(Chara c)
	{
		if (c == null)
		{
			return false;
		}
		if (base.IsPCFactionOrMinion)
		{
			if ((c == EClass.pc.enemy && !c.IsPCFactionOrMinion) || c.hostility <= Hostility.Enemy)   // PASS 1
			{
				return true;
			}
		}
		else
		{
			if (this.trait is TraitGuard && c.IsPCParty && EClass.player.IsCriminal && EClass._zone.instance == null)   // PASS 2
			{
				return true;
			}
			if (this.OriginalHostility >= Hostility.Friend)
			{
				if (c.hostility <= Hostility.Enemy && c.OriginalHostility == Hostility.Enemy)   // PASS 3
				{
					return true;
				}
			}
			else if (this.OriginalHostility <= Hostility.Enemy && (c.IsPCFactionOrMinion || (c.OriginalHostility != Hostility.Enemy && c.hostility >= Hostility.Friend)))   // PASS 4
			{
				return true;
			}
		}
		return false;
	}
    */

    static List<Chara> bucketPass1 = new List<Chara>();
    static List<Chara> bucketPass2 = new List<Chara>();
    static List<Chara> bucketPass3 = new List<Chara>();
    static List<Chara> bucketPass4 = new List<Chara>();

    static List<Chara> bucketPass2And3 = new List<Chara>();
    static List<Chara> bucketPass2And4 = new List<Chara>();

    static void BuildBucket()
    {
        List<Chara> charas = EClass._map.charas;

        foreach(Chara c in charas)
        {
            // Pass 1
            if ((c == EClass.pc.enemy && !c.IsPCFactionOrMinion) || c.hostility <= Hostility.Enemy)
            {
                bucketPass1.Add(c);
            }
            // Pass 2
            if (c.IsPCParty && EClass.player.IsCriminal && EClass._zone.instance == null)
            {
                bucketPass2.Add(c);
                bucketPass2And3.Add(c);
                bucketPass2And4.Add(c);
            }
            // Pass 3
            if (c.hostility <= Hostility.Enemy && c.OriginalHostility == Hostility.Enemy)
            {
                bucketPass3.Add(c);
                bucketPass2And3.Add(c);
            }
            // Pass 4
            if (c.IsPCFactionOrMinion || (c.OriginalHostility != Hostility.Enemy && c.hostility >= Hostility.Friend))
            {
                bucketPass4.Add(c);
                bucketPass2And4.Add(c);
            }
        }
    }

    static void ClearBucket()
    {
        bucketPass1.Clear();
        bucketPass2.Clear();
        bucketPass3.Clear();
        bucketPass4.Clear();
        bucketPass2And3.Clear();
        bucketPass2And4.Clear();
    }

    public static List<Chara> GetBucket(Chara targetChara)
    {
        if (targetChara.IsPCFactionOrMinion)
        {
            return bucketPass1;
        }
        else
        {
            if (targetChara.trait is TraitGuard)
            {
                if (targetChara.OriginalHostility >= Hostility.Friend)
                {
                    return bucketPass2And3;
                }
                else if (targetChara.OriginalHostility <= Hostility.Enemy)
                {
                    return bucketPass2And4;
                }
                return bucketPass2;
            }
            if (targetChara.OriginalHostility >= Hostility.Friend)
            {
                return bucketPass3;
            }
            else if (targetChara.OriginalHostility <= Hostility.Enemy)
            {
                return bucketPass4;
            }
        }
        return new List<Chara>();
    }

    [HarmonyPatch(typeof(Chara), nameof(Chara.FindNewEnemy))]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var instructionList = new CodeMatcher(instructions, generator)
            .Start()
            .MatchStartForward(
                new CodeMatch(o => o.opcode == OpCodes.Call &&
                                o.operand.ToString().Contains("_map")))
            .SetOpcodeAndAdvance(OpCodes.Nop)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                Transpilers.EmitDelegate(GetBucket))
            .RemoveInstructions(1)
            .MatchStartForward(
                new CodeMatch(o => o.opcode == OpCodes.Call &&
                                o.operand.ToString().Contains("_map")))
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                Transpilers.EmitDelegate(GetBucket))
            .RemoveInstructions(2)
            .InstructionEnumeration();

        foreach (CodeInstruction instruction in instructionList)
        {
            Plugin.ModLog(instruction.ToString(), PrivateLogLevel.Error);
        }
        return instructionList;
    }

    [HarmonyPatch(typeof(GameUpdater.SensorUpdater), nameof(GameUpdater.SensorUpdater.FixedUpdate))]
    static bool Prefix(GameUpdater.SensorUpdater __instance)
    {
        List<Chara> charas = EClass._map.charas;
        if (charas.Count > 200)
        {
            __instance.SetUpdatesPerFrame(charas.Count, 1f);
            BuildBucket();
            for (int i = 0; i < __instance.updatesPerFrame; i++)
            {
                __instance.index++;
                if (__instance.index >= charas.Count)
                {
                    __instance.index = 0;
                }
                Chara chara = charas[__instance.index];
                if (chara.IsAliveInCurrentZone && !chara.IsPC)
                {
                    chara.FindNewEnemy();
                }
            }
            ClearBucket();
            return false;
        }
        return true;
    }
}