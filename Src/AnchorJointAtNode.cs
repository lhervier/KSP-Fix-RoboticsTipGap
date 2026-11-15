using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Expansions.Serenity;
using HarmonyLib;
using UnityEngine;

namespace com.github.lhervier.ksp.fix.roboticstipgap
{
    /// <summary>
    /// Anchors the joint between a robotic part and the part it hangs from at their attach node, when that node is on
    /// the moving side of the robotic part (a piston docked by its tip, or attached upside down in the part tree).
    /// PartJoint.create gives SetupJoint the joint position and axes in the child's partTransform frame. For a robotic
    /// child, SetupJoint puts the joint on the rigidbody of the moving side, and also reads that position in the frame
    /// of the moving object (stack node) or of that rigidbody (surface node). On a piston, that object is turned and
    /// shifted by the extension: the joint ends up beside the piston, up to a few metres from the node, and the load
    /// twists it around that point. The fix keeps the frame the position is given in; the joint stays on the same
    /// body.
    /// </summary>
    internal static class AnchorJointAtNode
    {
        /// <summary>PartJoint.SetupJoint(Vector3, Vector3, Vector3, int, AttachNode, AttachNode) is private.</summary>
        private static readonly MethodInfo SetupJointMethod = AccessTools.DeclaredMethod(typeof(PartJoint), "SetupJoint",
            new[] { typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(int), typeof(AttachNode), typeof(AttachNode) });

        /// <summary>Patches PartJoint.SetupJoint.</summary>
        /// <exception cref="MissingMemberException">PartJoint has no SetupJoint of that signature.</exception>
        /// <exception cref="HarmonyException">SetupJoint does not have the shape the transpiler expects.</exception>
        public static void Install(Harmony harmony)
        {
            if (SetupJointMethod == null)
            {
                throw new MissingMemberException("PartJoint.SetupJoint");
            }
            harmony.Patch(SetupJointMethod,
                transpiler: new HarmonyMethod(typeof(AnchorJointAtNode), nameof(Transpiler)),
                postfix: new HarmonyMethod(typeof(AnchorJointAtNode), nameof(Postfix)));
        }

        /// <summary>
        /// Drops the two assignments that replace the frame of the joint position (local 1, set to
        /// child.partTransform first) for a robotic child.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Local 1 is not a Transform, or is not assigned exactly three times.
        /// </exception>
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            MethodBase original)
        {
            IList<LocalVariableInfo> locals = original.GetMethodBody().LocalVariables;
            if (locals.Count < 2 || locals[1].LocalType != typeof(Transform))
            {
                throw new InvalidOperationException("PartJoint.SetupJoint: local 1 is not a Transform");
            }
            var code = new List<CodeInstruction>(instructions);
            int stores = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.opcode != OpCodes.Stloc_1)
                {
                    continue;
                }
                stores++;
                // The first store is child.partTransform, the frame the joint position is given in: kept.
                if (stores > 1)
                {
                    // Labels and exception blocks stay on the instruction, only the opcode changes.
                    instruction.opcode = OpCodes.Pop;
                }
            }
            if (stores != 3)
            {
                throw new InvalidOperationException($"PartJoint.SetupJoint: {stores} stores to local 1, 3 expected");
            }
            return code;
        }

        /// <summary>At Debug, logs where the joint of a robotic child was anchored.</summary>
        private static void Postfix(PartJoint __instance, ConfigurableJoint __result, Vector3 jointPos)
        {
            if (!Log.IsDebugEnabled || __result == null || __instance.Child.FindModuleImplementing<BaseServo>() == null)
            {
                return;
            }
            Vector3 node = __instance.Child.partTransform.TransformPoint(jointPos);
            Vector3 anchor = __result.transform.TransformPoint(__result.anchor);
            Log.Debug($"Joint of {__instance.Child.partInfo.name} ({__instance.Child.persistentId}) to " +
                $"{__instance.Parent.partInfo.name} ({__instance.Parent.persistentId}) on {__result.gameObject.name}, " +
                $"anchor {__result.anchor.ToString("F3")}, connected anchor {__result.connectedAnchor.ToString("F3")}, " +
                $"{(anchor - node).magnitude:F3} m from the node");
        }
    }
}
