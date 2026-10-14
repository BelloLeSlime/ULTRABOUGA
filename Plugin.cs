using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using System.Net.WebSockets;
using System.Reflection;
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

        static readonly AccessTools.FieldRef<MeleeAttack, Transform> HitPointRef =
            AccessTools.FieldRefAccess<MeleeAttack, Transform>("hitPoint");

        static readonly AccessTools.FieldRef<MeleeAttack, float> RadiusRef =
            AccessTools.FieldRefAccess<MeleeAttack, float>("radius");

        static readonly MethodInfo DamageGetter =
            AccessTools.PropertyGetter(typeof(MeleeAttack), "Damage");

        static readonly MethodInfo KnockbackGetter =
            AccessTools.PropertyGetter(typeof(MeleeAttack), "Knockback");

        MeleeAttack swordMelee;
        Transform punchPoint;
        float nextPunch;
        float nextPunchKnuckleBlaster;
        float nextShockwave;

        static float? damageOverride;
        static float? knockbackOverride;

        [HarmonyPatch(typeof(MeleeAttack), "Damage", MethodType.Getter)]
        class DamageGetterPatch
        {
            static void Postfix(ref float __result)
            {
                if (damageOverride.HasValue) __result = damageOverride.Value;
            }
        }

        [HarmonyPatch(typeof(MeleeAttack), "Knockback", MethodType.Getter)]
        class KnockbackGetterPatch
        {
            static void Postfix(ref float __result)
            {
                if (knockbackOverride.HasValue) __result = knockbackOverride.Value;
            }
        }

        static string GetPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6)) DumpGameScripts();
            if (Input.GetKeyDown(KeyCode.F7)) DumpRootObjects();

            if (Input.GetKeyDown(KeyCode.F) && Time.time > nextPunch)
            {
                nextPunch = Time.time + 0.3f;
                nextPunchKnuckleBlaster = Time.time + 0.3f;
                Punch();
            }
            if (Input.GetKeyDown(KeyCode.G) && Time.time > nextPunchKnuckleBlaster)
            {
                nextPunch = Time.time + 0.3f;
                nextPunchKnuckleBlaster = Time.time + 0.5f;
                nextShockwave = Time.time + 0.4f;
                PunchKnuckleBlaster();
            }
            if (Time.time > nextShockwave && Input.GetKey(KeyCode.G) && nextShockwave > 0f)
            {
                nextPunch = Time.time + 0.3f;
                nextPunchKnuckleBlaster = Time.time + 0.5f;
                nextShockwave = 0f;
                Shockwave();
            }
        }

        void Punch()
        {
            if (swordMelee == null)
            {
                foreach (var b in FindObjectsByType<HandAnimationBridge>(FindObjectsInactive.Include))
                    if (GetPath(b.transform).Contains("Hand"))
                        swordMelee = b.GetComponentInChildren<MeleeAttack>(true);
                if (swordMelee == null) return;
            }

            var cam = Camera.main.transform;
            if (punchPoint == null) punchPoint = new GameObject("PunchPoint").transform;
            punchPoint.position = cam.position + cam.forward * 1.5f;

            var original = HitPointRef(swordMelee);
            HitPointRef(swordMelee) = punchPoint;
            try { swordMelee.DealDamage(); }
            finally { HitPointRef(swordMelee) = original;}
        }

        void PunchKnuckleBlaster()
        {
            if (swordMelee == null)
            {
                foreach (var b in FindObjectsByType<HandAnimationBridge>(FindObjectsInactive.Include))
                    if (GetPath(b.transform).Contains("Hand"))
                        swordMelee = b.GetComponentInChildren<MeleeAttack>(true);
                if (swordMelee == null) return;
            }

            var cam = Camera.main.transform;
            if (punchPoint == null) punchPoint = new GameObject("PunchPoint").transform;
            punchPoint.position = cam.position + cam.forward * 1.5f;

            var original = HitPointRef(swordMelee);
            HitPointRef(swordMelee) = punchPoint;
            var originalDamage = (float)DamageGetter.Invoke(swordMelee, null);
            damageOverride = originalDamage * 3f;
            var originalKnockback = (float)KnockbackGetter.Invoke(swordMelee, null);
            knockbackOverride = originalKnockback * 3f;
            try { swordMelee.DealDamage(); }
            finally { HitPointRef(swordMelee) = original; damageOverride = null; knockbackOverride = null; }
        }

        void Shockwave()
        {
            if (swordMelee == null)
            {
                foreach (var b in FindObjectsByType<HandAnimationBridge>(FindObjectsInactive.Include))
                    if (GetPath(b.transform).Contains("Hand"))
                        swordMelee = b.GetComponentInChildren<MeleeAttack>(true);
                if (swordMelee == null) return;
            }

            var cam = Camera.main.transform;
            if (punchPoint == null) punchPoint = new GameObject("PunchPoint").transform;
            punchPoint.position = cam.position;

            var original = HitPointRef(swordMelee);
            HitPointRef(swordMelee) = punchPoint;
            var originalKnockback = (float)KnockbackGetter.Invoke(swordMelee, null);
            knockbackOverride = originalKnockback * 3f;
            var originalRadius = RadiusRef(swordMelee);
            RadiusRef(swordMelee) = originalRadius * 3f;
            try { swordMelee.DealDamage(); }
            finally { HitPointRef(swordMelee) = original; knockbackOverride = null; RadiusRef(swordMelee) = originalRadius; }
        }
    

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
        /*
        [HarmonyPatch(typeof(PlayerMouvement), "dashSpeed", MethodType.Getter)]
        class DashSpeedGetterPatch
        {
            static void Postfix(ref float __result)
            {
                __result = 400f;
            }
        }*/

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