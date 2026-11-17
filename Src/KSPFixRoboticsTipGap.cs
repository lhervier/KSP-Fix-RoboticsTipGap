using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace com.github.lhervier.ksp.fix.roboticstipgap
{
    /// <summary>
    /// Installs the fix the settings turn on, once, when KSP starts: from then on, the attachment of a part carried by
    /// the moving side of a robotic part is anchored at their attach node.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class KSPFixRoboticsTipGap : MonoBehaviour
    {
        private const string HarmonyId = "com.github.lhervier.ksp.fix.roboticstipgap";

        private void Start()
        {
            Settings.Load();
            string version = typeof(KSPFixRoboticsTipGap).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Log.Info($"Version {version}, " +
                $"anchorJointAtNode {Settings.AnchorJointAtNode}, log level {Log.Level}");
            if (!Settings.AnchorJointAtNode)
            {
                return;
            }
            try
            {
                AnchorJointAtNode.Install(new Harmony(HarmonyId));
                Log.Info("Anchor joint at node fix installed");
            }
            catch (Exception e)
            {
                Log.Error("Could not install the anchor joint at node fix, a part carried by a robotic part can still " +
                    $"hang away from it under load, as without it: {e}");
            }
        }
    }
}
