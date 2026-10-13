using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ULTRABOUGA
{
    [BepInPlugin("fr.bello.ultrabouga", "ULTRABOUGA", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            Logger.LogInfo("Mod chargé !");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6)) DumpGameScripts();
            if (Input.GetKeyDown(KeyCode.F7)) DumpRootObjects();
        }

        // Liste tous les scripts du jeu (Assembly-CSharp) présents dans la scène
        void DumpGameScripts()
        {
            var scripts = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
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
            GUI.Label(new Rect(10, 10, 300, 25), $"Position du joueur : {FindObjectOfType<PlayerMain>()?.transform.position}");
        }
    }
}