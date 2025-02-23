using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.IO;
using UnityEngine.Assertions;
using System.Diagnostics;
using DG.Tweening;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;

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
            default:
                __result = Hostility.Ally;
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
            default:
                __result = Hostility.Ally;
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
        if (__instance.c_originalHostility != (Hostility)0)
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
                default:
                    __result = Hostility.Ally;
                    break;
            }
            return false;
        }
        __result = Hostility.Enemy;
        return false;
    }
}