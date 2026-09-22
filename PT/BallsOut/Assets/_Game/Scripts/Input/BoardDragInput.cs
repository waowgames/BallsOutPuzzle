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
#if ENABLE_INPUT_SYSTEM
        private InputAction pressAction;
        private InputAction positionAction;
        private Pointer activePointer;
#else
        private int touchId = -1;
        private bool mouseDrag;
#endif

        internal void Initialize(BoardGrid board, BoxMovementSystem movement)
        {
            this.board = board;
            this.movement = movement;
            if (inputCamera == null) inputCamera = Camera.main;
        }

#if ENABLE_INPUT_SYSTEM
        private void Awake()
        {
            pressAction = new InputAction("Board Press", InputActionType.PassThrough);
            pressAction.AddBinding("<Mouse>/leftButton");
            pressAction.AddBinding("<Touchscreen>/primaryTouch/press");
            pressAction.performed += HandlePress;
            positionAction = new InputAction("Board Position", InputActionType.PassThrough);
            positionAction.AddBinding("<Mouse>/position");
            positionAction.AddBinding("<Touchscreen>/primaryTouch/position");
            positionAction.performed += HandlePosition;
        }

        private void OnEnable()
        {
            pressAction.Enable();
            positionAction.Enable();
        }

        private void HandlePress(InputAction.CallbackContext context)
        {
            var pointer = context.control.device as Pointer;
            if (pointer == null || movement == null || inputCamera == null) return;
            if (context.ReadValue<float>() > 0.5f)
            {
                if (activePointer == null && Begin(pointer.position.ReadValue())) activePointer = pointer;
            }
            else if (activePointer == pointer)
            {
                Move(pointer.position.ReadValue());
                movement.Release();
                activePointer = null;
            }
        }

        private void HandlePosition(InputAction.CallbackContext context)
        {
            if (context.control.device == activePointer) Move(context.ReadValue<Vector2>());
        }

        private void OnDestroy()
        {
            pressAction?.Dispose();
            positionAction?.Dispose();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        private void Update()
        {
            if (movement == null || inputCamera == null) return;
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
        }
#endif

        private bool Project(Vector2 screenPoint, out Vector3 worldPoint)
        {
            var plane = new Plane(board.Root.up, board.Root.position);
            Ray ray = inputCamera.ScreenPointToRay(screenPoint);
            bool hit = plane.Raycast(ray, out float distance);
            worldPoint = hit ? ray.GetPoint(distance) : default;
            return hit;
        }

        private bool Begin(Vector2 point) =>
            (UIManager.Instance == null || !UIManager.Instance.HasActivePopup) &&
            Project(point, out var world) && movement.Begin(world);
        private void Move(Vector2 point) { if (Project(point, out var world)) movement.Drag(world); }
        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            pressAction?.Disable();
            positionAction?.Disable();
#endif
            CancelDrag();
        }

        private void CancelDrag()
        {
            movement?.Cancel();
#if ENABLE_INPUT_SYSTEM
            activePointer = null;
#else
            touchId = -1;
            mouseDrag = false;
#endif
        }

        private void OnApplicationFocus(bool focus) { if (!focus) CancelDrag(); }
    }
}
