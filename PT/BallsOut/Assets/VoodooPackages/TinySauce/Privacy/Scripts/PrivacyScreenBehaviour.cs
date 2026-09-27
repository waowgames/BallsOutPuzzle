using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Internal;
using VoodooPackages.TinySauce.Privacy.Localisation;

namespace Voodoo.Tiny.Sauce.Privacy
{
    public class PrivacyScreenBehaviour : MonoBehaviour, IPointerClickHandler
    {
        private const string TAG = "PrivacyScreenBehaviour";
        [SerializeField] private Button continueWithoutAcceptingButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button dataSharingPartnersButton;
        [SerializeField] private Button saveChoicesButton;
        [SerializeField] private Button acceptAllButton;
        
        [SerializeField] private AcceptDeclineToggle advertisingConsent;
        [SerializeField] private AcceptDeclineToggle supportAndAnalyticsConsent;

        [SerializeField] private TMP_Text choicesMakeGameBetterText;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Text advertisingText;
        [SerializeField] private Text analyticsText;
        [SerializeField] private TMP_Text audienceMeasurementDescriptionText;
        [SerializeField] private TMP_Text adPersonalizationText;
        [SerializeField] private TMP_Text optionsText;
        [SerializeField] private TMP_Text dataSharingPartnersText;
        
        [SerializeField] private TMP_Text continueWithoutAcceptingButtonText;
        [SerializeField] private TMP_Text playButtonText;
        [SerializeField] private TMP_Text settingsButtonText;
        [SerializeField] private Text saveChoicesText;
        [SerializeField] private TMP_Text acceptAllText;
        [SerializeField] private Text[] acceptButtonText;
        [SerializeField] private Text[] declineButtonText;
        
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private LocalisationDict localisationDict;
        
        private TinySauceSettings _sauceSettings;
        
        [SerializeField] private GameObject mainUIObject;
        [SerializeField] private GameObject settingsUIObject;
        
        private EventSystem _eventSystemPrefab;
        private EventSystem _eventSystem;
        
        private List<TMP_Text> _hyperlinkedTexts = new List<TMP_Text>();
        
        public TaskCompletionSource<bool[]> ConfirmWaitTask;

        private void Awake()
        {
            _sauceSettings = TinySauceSettings.Load();
            if (_sauceSettings == null)
            {
                throw new Exception("Can't find the Settings ScriptableObject in the Resources/TinySauce folder.");
            }

            // Keep dialog panels inside Screen.safeArea (Android 16+ edge-to-edge); leave blockers full-bleed.
            PrivacySafeAreaFitter.EnsureContainer(
                (RectTransform)transform,
                mainUIObject,
                settingsUIObject
            );
        }

        private void Start()
        {
            continueWithoutAcceptingButton.onClick.AddListener(OnContinueWithoutAccepting);
            playButton.onClick.AddListener(OnAccept);
            saveChoicesButton.onClick.AddListener(OnSaveChoices);
            acceptAllButton.onClick.AddListener(OnAccept);
            settingsButton.onClick.AddListener(OnSettings);
            dataSharingPartnersButton.onClick.AddListener(OnDataSharingPartners);

            _hyperlinkedTexts.Add(labelText);
            _hyperlinkedTexts.Add(optionsText);
            
            InitEventSystem();
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;

            Camera eventCamera = eventData.pressEventCamera;
            if (eventCamera == null && _hyperlinkedTexts.Count > 0 && _hyperlinkedTexts[0] != null)
            {
                Canvas canvas = _hyperlinkedTexts[0].GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    eventCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }

            for (int i = 0; i < _hyperlinkedTexts.Count; i++)
            {
                TMP_Text hyperlinkedText = _hyperlinkedTexts[i];
                if (hyperlinkedText == null)
                {
                    Debug.LogWarning($"[{TAG}] Hyperlinked text at index {i} is null. Check that Label Text and Options Text are assigned in the GDPR popup prefab.", this);
                    continue;
                }

                hyperlinkedText.ForceMeshUpdate();
                if (hyperlinkedText.textInfo == null || hyperlinkedText.textInfo.linkInfo == null)
                    continue;

                int linkIndex = TMP_TextUtilities.FindIntersectingLink(
                    hyperlinkedText,
                    eventData.position,
                    eventCamera
                );

                if (linkIndex < 0 || linkIndex >= hyperlinkedText.textInfo.linkInfo.Length)
                    continue;

                TMP_LinkInfo linkInfo = hyperlinkedText.textInfo.linkInfo[linkIndex];
                string url = linkInfo.GetLinkID();

                if (string.IsNullOrEmpty(url))
                {
                    Debug.LogError("⚠️ IMPORTANT ⚠️ Please fill a Policy Privacy URL in your TinySauce Settings by using the top menu : TinySauce > TinySauce Settings > Edit Settings");
                }
                else
                {
                    Application.OpenURL(url);
                }
                return;
            }
        }

        internal void Localize(string countryCode = "EN")
        {
            Dictionary<string,string> dict = localisationDict.GetLocalisationDict(countryCode);
            
            continueWithoutAcceptingButtonText.text = dict[GdprLocalisationStrings.gdpr_continue_without_accepting.ToString()];
            playButtonText.text = dict[GdprLocalisationStrings.gdpr_continue.ToString()];
            settingsButtonText.text = dict[GdprLocalisationStrings.gdpr_settings.ToString()];
            saveChoicesText.text = dict[GdprLocalisationStrings.gdpr_save_choices.ToString()];
            acceptAllText.text = dict[GdprLocalisationStrings.gdpr_accept_all.ToString()];
            titleText.text = dict[GdprLocalisationStrings.gdpr_title.ToString()];
            choicesMakeGameBetterText.text = dict[GdprLocalisationStrings.gdpr_choices_make_this_game_better.ToString()];
            advertisingText.text = dict[GdprLocalisationStrings.gdpr_advertising.ToString()];
            dataSharingPartnersText.text = dict[GdprLocalisationStrings.gdpr_data_sharing_partners.ToString()];
            audienceMeasurementDescriptionText.text = dict[GdprLocalisationStrings.gdpr_audience_description.ToString()];
            
            if (_sauceSettings.doesYourGameDisplayAds)
            {
                analyticsText.text = dict[GdprLocalisationStrings.gdpr_analytics.ToString()];
                labelText.text = dict[GdprLocalisationStrings.gdpr_text.ToString()];
                optionsText.text = dict[GdprLocalisationStrings.gdpr_options.ToString()];
                adPersonalizationText.text = dict[GdprLocalisationStrings.gdpr_ads_personalization_description.ToString()];
            }
            else
            {
                analyticsText.text = dict[GdprLocalisationStrings.gdpr_analytics_no_ads.ToString()];
                labelText.text = dict[GdprLocalisationStrings.gdpr_text_no_ads.ToString()];
                optionsText.text = dict[GdprLocalisationStrings.gdpr_options_no_ads.ToString()];
                adPersonalizationText.text = dict[GdprLocalisationStrings.gdpr_attribution_no_ads.ToString()];
            }
            
            string studioName = _sauceSettings.companyName;

            titleText.text = titleText.text.Replace(
                "{STUDIO_NAME}",
                studioName    
            );
            
            optionsText.text = optionsText.text.Replace(
                "{STUDIO_NAME}",
                studioName
            );

            labelText.text = labelText.text.Replace(
                "{STUDIO_NAME}",
                studioName
            );
            
            string privacyUrl = _sauceSettings.privacyPolicyURL;
            string privacyPolicyTranslation = dict[GdprLocalisationStrings.gdpr_privacy_policy.ToString()];
            string linkedVersion = $"<link={privacyUrl}><color=#6699FF>{privacyPolicyTranslation}</color></link>";
            
            optionsText.text = optionsText.text.Replace(
                "{PRIVACY_POLICY}",
                linkedVersion
            );
            
            labelText.text = labelText.text.Replace(
                "{PRIVACY_POLICY}",
                linkedVersion
            );
            
            foreach (var text in acceptButtonText)
                text.text = dict[GdprLocalisationStrings.gdpr_continue.ToString()];
            
            foreach (var text in declineButtonText)
                text.text = dict[GdprLocalisationStrings.gdpr_decline.ToString()];
        }

        private void OnToggleAge(bool consent)
        {
            playButton.interactable = consent;
        }

        public void SetToggleStates(bool adConsent, bool analyticsConsent)
        {
            advertisingConsent.SetConsent(adConsent);
            supportAndAnalyticsConsent.SetConsent(analyticsConsent);
            //OnToggleAge(ageToggle);
        }

        private void OnDataSharingPartners()
        {
            TinySauceBehaviour.Instance.privacyManager.OpenPrivacyPartnersScreen();
        }

        /*private void OnPressPlay()
        {
            mainUIObject.SetActive(false);
          
            ConfirmWaitTask?.TrySetResult(new []{advertisingToggle.isOn, analyticsToggle.isOn});
            
            RemoveListeners();
            
            if (_eventSystem != null)
                Destroy(_eventSystem.gameObject);
            
            Destroy(gameObject);
        }*/

        private void OnSaveChoices()
        {
            mainUIObject.SetActive(false);
            settingsUIObject.SetActive(false);
           
            ConfirmWaitTask?.TrySetResult(new []{supportAndAnalyticsConsent.Consent, advertisingConsent.Consent});
            
            OnGDPRAnswered();
        }

        public void SetToSettings()
        {
            OnSettings();
        }

        private void OnSettings()
        {
            mainUIObject.gameObject.SetActive(false);
            settingsUIObject.SetActive(true);
        }

        private void OnAccept()
        {
            mainUIObject.SetActive(false);
            settingsUIObject.SetActive(false);
            
            ConfirmWaitTask?.TrySetResult(new []{true, true});
            
            OnGDPRAnswered();
        }

        public void OnReturn()
        {
            mainUIObject.SetActive(true);
            settingsUIObject.SetActive(false);
        }

        private void OnContinueWithoutAccepting()
        {
            mainUIObject.SetActive(false);
            
            ConfirmWaitTask?.TrySetResult(new []{false, false});

            OnGDPRAnswered();
        }

        private void OnGDPRAnswered()
        {
            RemoveListeners();
                
            if (_eventSystem != null)
                Destroy(_eventSystem.gameObject);
                
            Destroy(gameObject);
        }

        private void OnPressPrivacyPolicy()
        {
            if (_sauceSettings.privacyPolicyURL != "")
            {
                Application.OpenURL(_sauceSettings.privacyPolicyURL);
            }
        }

        private void RemoveListeners()
        {
            playButton.onClick.RemoveAllListeners();
        }
        
        private void InitEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            
#if ENABLE_INPUT_SYSTEM
            if (_eventSystemPrefab == null)
            {
                _eventSystemPrefab = Resources.LoadAll<EventSystem>("Prefabs/NEW_INPUT_SYSTEM")[0];
            }
#else
            if (_eventSystemPrefab == null)
            {
                _eventSystemPrefab = Resources.LoadAll<EventSystem>("Prefabs/OLD_INPUT_SYSTEM")[0];
            }
#endif
        
            if (_eventSystemPrefab == null)
                Debug.LogError("There is no TSEventSystem prefab in the 'Assets/VoodooPackages/TinySauce/Resources/Prefabs' folder");

            _eventSystem = Instantiate(_eventSystemPrefab);
        }
    }
}
