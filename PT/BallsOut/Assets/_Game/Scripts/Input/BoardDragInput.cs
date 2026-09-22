using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BallsOut
{
    public sealed class BoardDragInput : MonoBehaviour
    {
        [SerializeField] private Camera inputCamera;
        private BoardGrid board;
        private BoxMovementSystem movement;
        private int touchId = -1;
        private bool mouseDrag;

        internal void Initialize(BoardGrid board, BoxMovementSystem movement)
        {
            this.board = board;
            this.movement = movement;
            if (inputCamera == null) inputCamera = Camera.main;
        }

        private void Update()
        {
            if (movement == null || inputCamera == null) return;
#if ENABLE_INPUT_SYSTEM
            var screen = Touchscreen.current;
            if (!mouseDrag && screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    int id = touch.touchId.ReadValue();
                    if (touchId < 0 && touch.press.wasPressedThisFrame)
                    {
                        if (Begin(touch.position.ReadValue())) touchId = id;
                    }
                    if (touchId != id || touchId < 0) continue;
                    Move(touch.position.ReadValue());
                    if (!touch.press.isPressed) { movement.Release(); touchId = -1; }
                    return;
                }
                if (touchId >= 0) { movement.Release(); touchId = -1; return; }
            }
            var mouse = Mouse.current;
            if (touchId >= 0 || mouse == null) return;
            Vector2 point = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) mouseDrag = Begin(point);
            if (!mouseDrag) return;
            Move(point);
            if (mouse.leftButton.wasReleasedThisFrame) { movement.Release(); mouseDrag = false; }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!mouseDrag && UnityEngine.Input.touchCount > 0)
            {
                for (int i = 0; i < UnityEngine.Input.touchCount; i++)
                {
                    Touch touch = UnityEngine.Input.GetTouch(i);
                    if (touchId < 0 && touch.phase == TouchPhase.Began && Begin(touch.position)) touchId = touch.fingerId;
                    if (touch.fingerId != touchId) continue;
                    Move(touch.position);
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) { movement.Release(); touchId = -1; }
                    return;
                }
            }
            if (touchId >= 0) { movement.Release(); touchId = -1; return; }
            Vector2 point = UnityEngine.Input.mousePosition;
            if (UnityEngine.Input.GetMouseButtonDown(0)) mouseDrag = Begin(point);
            if (!mouseDrag) return;
            Move(point);
            if (UnityEngine.Input.GetMouseButtonUp(0)) { movement.Release(); mouseDrag = false; }
#endif
        }

        private bool Project(Vector2 screenPoint, out Vector3 worldPoint)
        {
            var plane = new Plane(board.Root.up, board.Root.position);
            Ray ray = inputCamera.ScreenPointToRay(screenPoint);
            bool hit = plane.Raycast(ray, out float distance);
            worldPoint = hit ? ray.GetPoint(distance) : default;
            return hit;
        }

        private bool Begin(Vector2 point) => Project(point, out var world) && movement.Begin(world);
        private void Move(Vector2 point) { if (Project(point, out var world)) movement.Drag(world); }
        private void OnDisable() { movement?.Cancel(); touchId = -1; mouseDrag = false; }
        private void OnApplicationFocus(bool focus) { if (!focus) OnDisable(); }
    }
}
