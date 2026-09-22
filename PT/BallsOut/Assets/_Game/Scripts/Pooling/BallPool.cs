using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public sealed class BallPool
    {
        private readonly Dictionary<BallColorDefinition, Stack<Transform>> available = new Dictionary<BallColorDefinition, Stack<Transform>>();
        private readonly PrefabRegistry registry;
        private readonly Transform parent;

        public BallPool(PrefabRegistry registry, Transform parent)
        {
            this.registry = registry;
            this.parent = parent;
        }

        public Transform Rent(BallColorDefinition color)
        {
            if (!available.TryGetValue(color, out var pool))
            {
                pool = new Stack<Transform>();
                available.Add(color, pool);
            }
            if (pool.Count > 0)
            {
                Transform reused = pool.Pop();
                reused.gameObject.SetActive(true);
                return reused;
            }
            GameObject prefab = color.ballPrefabOverride != null ? color.ballPrefabOverride : registry != null ? registry.ballPrefab : null;
            if (prefab == null) return null;
            GameObject visual = Object.Instantiate(prefab, parent);
            visual.name = "Ball " + color.name;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = registry != null ? registry.ballScale : Vector3.one;
            PrefabRegistry.ApplyMaterial(visual, color.ballMaterial);
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            return visual.transform;
        }

        public void Return(BallState ball)
        {
            if (ball.Visual == null) return;
            ball.Visual.gameObject.SetActive(false);
            ball.Visual.SetParent(parent, false);
            ball.Visual.localPosition = Vector3.zero;
            ball.Visual.localRotation = Quaternion.identity;
            ball.Visual.localScale = registry != null ? registry.ballScale : Vector3.one;
            available[ball.Color].Push(ball.Visual);
            ball.Visual = null;
        }
    }
}
