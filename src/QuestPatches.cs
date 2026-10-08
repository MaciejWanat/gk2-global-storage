using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Quest and dialog code reads the player's bag directly. In the classes below (and their lambdas /
    // local functions), calls to the bag's item count / has / remove methods are swapped for QuestRedirect,
    // which adds the global chests when the inventory is the player's bag.
    internal static class QuestPatches
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly string[] TargetTypes =
        {
            "QuestFinishCheck",                     // quest "ready to finish" check
            "UIMultiAnswerOption",                  // dialog answers with item lock / price, and paying the price
            "UIMultiAnswerIcon",                    // have/need counters on dialog answers
            "UIQuestInfoWindow",                    // quest window enough/not enough colouring
            "GK2.FlowCanvasNodes.Flow_HasItem",     // quest scripts: player has item?
            "GK2.FlowCanvasNodes.Flow_AddItem",     // quest scripts: take item from player
            "LazyExpression",                       // HasPlayerItemInInv(...) expression
        };

        private static Dictionary<MethodInfo, MethodInfo> map;

        public static int Apply(Harmony harmony, out int methods)
        {
            methods = 0;
            map = new Dictionary<MethodInfo, MethodInfo>
            {
                [AccessTools.Method(typeof(Item), nameof(Item.HasItemQuantityInInventory), new[] { typeof(string), typeof(int) })]
                    = AccessTools.Method(typeof(QuestRedirect), nameof(QuestRedirect.HasItemQuantityInInventory)),
                [AccessTools.Method(typeof(Item), nameof(Item.GetTotalCountInInventory), new[] { typeof(string), typeof(Item), typeof(bool) })]
                    = AccessTools.Method(typeof(QuestRedirect), nameof(QuestRedirect.GetTotalCountInInventory)),
                [AccessTools.Method(typeof(Inventory), nameof(Inventory.RemoveItemById), new[] { typeof(string), typeof(int), typeof(Item), typeof(Item), typeof(bool) })]
                    = AccessTools.Method(typeof(QuestRedirect), nameof(QuestRedirect.RemoveItemById)),
            };
            if (map.Keys.Any(k => k == null) || map.Values.Any(v => v == null))
            {
                Debug.LogWarning("[GK2GlobalStorage] Quest patch: inventory methods not found (game update?)");
                return 0;
            }
            HashSet<int> tokens = new HashSet<int>(map.Keys.Select(k => k.MetadataToken));
            Module module = typeof(Item).Module;
            HarmonyMethod transpiler = new HarmonyMethod(typeof(QuestPatches), nameof(Transpiler));
            int types = 0;
            foreach (string name in TargetTypes)
            {
                Type type = AccessTools.TypeByName(name);
                if (type == null)
                {
                    Debug.LogWarning("[GK2GlobalStorage] Quest patch: type not found: " + name);
                    continue;
                }
                types++;
                foreach (MethodBase method in MethodsOf(type))
                {
                    if (method.Module != module || !Calls(method, tokens))
                    {
                        continue;
                    }
                    try
                    {
                        harmony.Patch(method, transpiler: transpiler);
                        methods++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[GK2GlobalStorage] Quest patch failed on " + type.Name + "." + method.Name + ": " + ex.Message);
                    }
                }
            }
            return types == TargetTypes.Length && methods > 0 ? 1 : 0;
        }

        private static IEnumerable<MethodBase> MethodsOf(Type type)
        {
            foreach (MethodInfo m in type.GetMethods(All))
            {
                if (!m.IsAbstract && !m.ContainsGenericParameters)
                {
                    yield return m;
                }
            }
            foreach (ConstructorInfo c in type.GetConstructors(All))
            {
                yield return c;
            }
            // Lambdas, local functions and iterators are compiled into nested types.
            foreach (Type nested in type.GetNestedTypes(All))
            {
                if (nested.IsGenericTypeDefinition)
                {
                    continue;
                }
                foreach (MethodBase m in MethodsOf(nested))
                {
                    yield return m;
                }
            }
        }

        // Cheap pre-filter: look for call/callvirt with one of the target tokens in the raw IL.
        private static bool Calls(MethodBase method, HashSet<int> tokens)
        {
            byte[] il;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch
            {
                return false;
            }
            if (il == null)
            {
                return false;
            }
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if ((il[i] == 0x28 || il[i] == 0x6F) && tokens.Contains(BitConverter.ToInt32(il, i + 1)))
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction ins in instructions)
            {
                if ((ins.opcode == System.Reflection.Emit.OpCodes.Call || ins.opcode == System.Reflection.Emit.OpCodes.Callvirt)
                    && ins.operand is MethodInfo called && map.TryGetValue(called, out MethodInfo replacement))
                {
                    ins.opcode = System.Reflection.Emit.OpCodes.Call;
                    ins.operand = replacement;
                }
                yield return ins;
            }
        }
    }
}
