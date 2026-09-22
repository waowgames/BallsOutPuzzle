using UnityEngine;

namespace BallsOut
{
    [CreateAssetMenu(menuName = "Balls Out/Color")]
    public sealed class BallColorDefinition : ScriptableObject
    {
        public string id;
        public Color displayColor = Color.white;
        public Material ballMaterial;
        public Material boxMaterial;
        public GameObject ballPrefabOverride;

        // Asset identity is the gameplay color key; display RGB is visual only.
    }
}
