using BepInEx;
using UnityEngine;

namespace ULTRABOUGA
{
    [BepInPlugin("fr.bello.ultrabouga", "ULTRABOUGA", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            Logger.LogInfo("Mod chargé !");
        }
    }
}