using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using System.Net.WebSockets;
using UnityEngine.XR.WSA;

namespace ULTRABOUGA
{
    [BepInPlugin("fr.bello.ultrabouga", "ULTRABOUGA", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            Logger.LogInfo("Mod chargé !");
            new Harmony("fr.bello.ultrakillmod").PatchAll();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6)) DumpGameScripts();
            if (Input.GetKeyDown(KeyCode.F7)) DumpRootObjects();
            if (Input.GetKeyDown(KeyCode.F1)) {
                var player = FindAnyObjectByType<PlayerMain>();
                if (player != null) {
                    Logger.LogInfo(player.Data.dashSpeed);
                }
            }
        }

        // Liste tous les scripts du jeu (Assembly-CSharp) présents dans la scène
        void DumpGameScripts()
        {
            var scripts = FindObjectsByType<MonoBehaviour>();
            foreach (var s in scripts)
            {
                var t = s.GetType();
                if (t.Assembly.GetName().Name != "Assembly-CSharp") continue;
                Logger.LogInfo($"{t.FullName}  ->  objet '{s.gameObject.name}'");
            }
        }

        // Liste les objets racine de la scène active
        void DumpRootObjects()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                Logger.LogInfo($"Racine : {go.name}");
        }
        
        void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 300, 25), $"Position du joueur : {FindAnyObjectByType<PlayerMain>()?.transform.position}");
        }

        [HarmonyPatch(typeof(PlayerMouvement), "UpdateInput")]
        class UpdateInputPatch
        {
            static bool wasHeld;

            static void Prefix(ref MouvementInput __0)
            {
                bool held = __0.Run;

                __0.Dash = held && !wasHeld;
                wasHeld = held;

                __0.Run = false;
                __0.RunSunstain = false;
            }
        }
        [HarmonyPatch(typeof(PlayerMouvement), "dashSpeed", MethodType.Getter)]
        class DashSpeedGetterPatch
        {
            static void Postfix(ref float __result)
            {
                __result = 400f;
            }
        }

        [HarmonyPatch(typeof(PlayerMain), "Init")]
        class PlayerInitPatch
        {
            static void Postfix(PlayerMain __instance)
            {
                __instance.Inventory.UpdateMoney(500000000);
            }
        }

    }
}