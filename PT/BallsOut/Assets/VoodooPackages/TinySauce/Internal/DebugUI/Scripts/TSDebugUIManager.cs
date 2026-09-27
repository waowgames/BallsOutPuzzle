using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Voodoo.Tiny.Sauce.Internal.Analytics;
using Voodoo.Tiny.Sauce.Internal.Debugger.Screens;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Voodoo.Tiny.Sauce.Internal.Debugger
{
    public class TSDebugUIManager : MonoBehaviour
    {
        private const string TAG = "TSDebugUIManager";

        private static TSDebugUIManager _instance;
        public static TSDebugUIManager Instance => _instance;

        private bool isDebugUIOpen = false;
        private TSDebugMainMenuScreen _mainMenuScreen;
        private TSDebugAppInformationScreen _appInformationScreen;
        private TSDebugGameAnalyticsScreen _gameAnalyticsScreen;
        private TSDebugVanEventConsoleScreen _vanEventConsoleScreen;
        private readonly Dictionary<Type, TSDebugScreen> _registeredScreens = new();
        private TSDebugVanEventDetailsScreen _vanEventDetailsScreen;
        private EventSystem _eventSystem;

        private float maxDurationBetweenTap = 2.5f;
        private float countDown;

        private int countTapTL = 0;
        private int countTapTR = 0;

        private Vector3 mousePos;
        private int smallerScreenSliceNb = 6;
        private int biggerScreenSliceNb = 8;
        private int screenWidthSliceNb;
        private int screenHeightSliceNb;

        private int _screenSliceWidth;
        public int ScreenSliceWidth { get => _screenSliceWidth; }

        private int _screenSliceHeight;
        public int ScreenSliceHeight { get => _screenSliceHeight; }


        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Start()
        {
            if (Screen.width < Screen.height)
            {
                screenWidthSliceNb = smallerScreenSliceNb;
                screenHeightSliceNb = biggerScreenSliceNb;
            }
            else
            {
                screenWidthSliceNb = biggerScreenSliceNb;
                screenHeightSliceNb = smallerScreenSliceNb;
            }

            _screenSliceWidth = Screen.width / screenWidthSliceNb;
            _screenSliceHeight = Screen.height / screenHeightSliceNb;
        }

        private void Update()
        {
            mousePos = GetPosition();

            if (!isDebugUIOpen)
            {
                if (countDown > 0)
                {
                    countDown -= Time.unscaledDeltaTime;

                    if (countDown <= 0) ResetCountsTap();
                }

                if (GetTap())
                {
                    if (countTapTL < 1) TapTopLeftElseReset();
                    else if (countTapTR < 2) TapTopRightElseReset();
                    else if (countTapTL < 4) TapTopLeftElseReset();
                    else if (countTapTR < 6) TapTopRightElseReset();
                }

                if (countTapTL == 4 && countTapTR == 6)
                {
                    OpenDebugUI();
                    ResetCountsTap();
                }
            }
        }

        public void OpenDebugUI()
        {
            if (isDebugUIOpen)
                return;

            EnsureEventSystem();
            ShowMainMenu();
            isDebugUIOpen = true;
            TinySauce.TrackCustomEvent("DebugUI_OpeningEvent");
        }

        public void ShowMainMenu()
        {
            EnsureScreen(ref _mainMenuScreen);
            _mainMenuScreen.ShowScreen();
        }

        public void ShowAppInformationScreen()
        {
            EnsureScreen(ref _appInformationScreen);
            _appInformationScreen.ShowScreen();
        }

        public void ShowGameAnalyticsScreen()
        {
            EnsureScreen(ref _gameAnalyticsScreen);
            _gameAnalyticsScreen.ShowScreen();
        }

        public void ShowScreen<T>() where T : TSDebugScreen
        {
            var screenType = typeof(T);
            if (!_registeredScreens.TryGetValue(screenType, out var screen) || screen == null)
            {
                var screenObject = new GameObject(screenType.Name, screenType);
                DontDestroyOnLoad(screenObject);
                screen = screenObject.GetComponent<T>();
                _registeredScreens[screenType] = screen;
            }

            screen.ShowScreen();
        }

        public void ShowVanEventConsoleScreen()
        {
            EnsureScreen(ref _vanEventConsoleScreen);
            _vanEventConsoleScreen.ShowScreen();
        }

        internal void ShowVanEventDetailsScreen(DebugAnalyticsLog log)
        {
            EnsureScreen(ref _vanEventDetailsScreen);
            _vanEventDetailsScreen.SetLog(log);
            _vanEventDetailsScreen.ShowScreen();
        }

        private static void EnsureScreen<T>(ref T screen) where T : TSDebugScreen
        {
            if (screen != null)
                return;

            var screenObject = new GameObject(typeof(T).Name, typeof(T));
            DontDestroyOnLoad(screenObject);
            screen = screenObject.GetComponent<T>();
        }

        public void CloseDebugUI()
        {
            TSDebugScreen.CloseActiveScreen();

            if (_eventSystem != null)
            {
                Destroy(_eventSystem.gameObject);
                _eventSystem = null;
            }

            isDebugUIOpen = false;
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

#if ENABLE_INPUT_SYSTEM
            var prefab = Resources.LoadAll<EventSystem>("Prefabs/NEW_INPUT_SYSTEM")[0];
#else
            var prefab = Resources.LoadAll<EventSystem>("Prefabs/OLD_INPUT_SYSTEM")[0];
#endif
            if (prefab != null)
                _eventSystem = Instantiate(prefab);
        }
        
        #region [HANDLING_INPUT_SYSTEMS]
        private bool GetTap()
        {
#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
            return Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_INPUT_SYSTEM && (UNITY_ANDROID || UNITY_IOS)
            return Pointer.current.press.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
        
        private Vector3 GetPosition()
        {
#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
            return Mouse.current.position.ReadValue();
#elif ENABLE_INPUT_SYSTEM && (UNITY_ANDROID || UNITY_IOS)
            return Pointer.current.position.ReadValue();
#else
            return Input.mousePosition;
#endif
        }

        #endregion

        #region [TAP_FUNCTIONS]


        
        private void TapTopLeftElseReset()
        {
            if (mousePos.x <= ScreenSliceWidth && mousePos.y >= ScreenSliceHeight * (screenHeightSliceNb - 1))
                ValidTap(ref countTapTL);
            else
                ResetCountsTap();
        }
        private void TapTopRightElseReset()
        {
            if (mousePos.x >= ScreenSliceWidth * (screenWidthSliceNb - 1) && mousePos.y >= ScreenSliceHeight * (screenHeightSliceNb - 1))
                ValidTap(ref countTapTR);
            else
                ResetCountsTap();
        }

        private void ValidTap(ref int countToIncrement)
        {
            countDown = maxDurationBetweenTap;
            countToIncrement++;
        }

        private void ResetCountsTap()
        {
            //Debug.Log("RESET : TL=" + countTapTL + " // TR=" + countTapTR + " // countDown=" + countDown);
            countDown = 0;
            countTapTL = 0;
            countTapTR = 0;
        }
        #endregion
    }
}