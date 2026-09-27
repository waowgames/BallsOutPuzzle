using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoodooPackages.TinySauce.Privacy.Localisation;

namespace Voodoo.Tiny.Sauce.Privacy {
    public class PrivacyPartnersScreenBehaviour : MonoBehaviour
    {
        private const string TAG = "PrivacyPartnersScreenBehaviour";
        private const string adjustPrivacyLink = "https://www.adjust.com/terms/privacy-policy/";
        private const string facebookPrivacyLink = "https://www.facebook.com/privacy/policy/";
        private const string gameAnalyticsPrivacyLink = "https://gameanalytics.com/privacy/";
        private const string voodooPrivacyLink = "https://www.voodoo.io/privacy";

        [SerializeField] private Button AdjustLearnMoreButton;
        [SerializeField] private Button FacebookLearnMoreButton;
        [SerializeField] private Button GameAnalyticsLearnMoreButton;
        [SerializeField] private Button VoodooLearnMoreButton;
        [SerializeField] private Button CloseButton;

        [SerializeField] private Text headerTitle;
        [SerializeField] private Text advertisingText;
        [SerializeField] private Text analyticsText;
        [SerializeField] private TMP_Text adjustLearnMoreText;
        [SerializeField] private TMP_Text facebookLearnMoreText;
        [SerializeField] private TMP_Text gameAnalyticsLearnMoreText;
        [SerializeField] private TMP_Text voodooLearnMoreText;

        [SerializeField] private LocalisationDict localisationDict;
        [SerializeField] private GameObject dialogPanel;

        private void Awake()
        {
            // Partners dialog must respect safe area; full-screen blocker stays outside the container.
            GameObject panel = dialogPanel != null ? dialogPanel : FindDialogPanel();
            PrivacySafeAreaFitter.EnsureContainer((RectTransform)transform, panel);
        }

        private void Start()
        {
            AdjustLearnMoreButton.onClick.AddListener(OnPressAdjustLearnMore);
            FacebookLearnMoreButton.onClick.AddListener(OnPressFacebookLearnMore);
            GameAnalyticsLearnMoreButton.onClick.AddListener(OnPressGameAnalyticsLearnMore);
            VoodooLearnMoreButton.onClick.AddListener(OnPressVoodooLearnMore);
            CloseButton.onClick.AddListener(OnPressClose);
        }

        private GameObject FindDialogPanel()
        {
            Transform background = transform.Find("Background");
            return background != null ? background.gameObject : null;
        }

        internal void Localize(string countryCode = "EN")
        {
            
            Dictionary<string,string> dict = localisationDict.GetLocalisationDict(countryCode);
            
            headerTitle.text = dict[GdprLocalisationStrings.gdpr_data_sharing_partners.ToString()];
            advertisingText.text = dict[GdprLocalisationStrings.gdpr_advertising.ToString()];
            analyticsText.text = dict[GdprLocalisationStrings.gdpr_analytics.ToString()];
            adjustLearnMoreText.text = dict[GdprLocalisationStrings.gdpr_learn_more.ToString()];
            facebookLearnMoreText.text = dict[GdprLocalisationStrings.gdpr_learn_more.ToString()];
            gameAnalyticsLearnMoreText.text = dict[GdprLocalisationStrings.gdpr_learn_more.ToString()];
            voodooLearnMoreText.text = dict[GdprLocalisationStrings.gdpr_learn_more.ToString()];
        }

        private void OnPressAdjustLearnMore()
        {
            Application.OpenURL(adjustPrivacyLink);
        }

        private void OnPressFacebookLearnMore()
        {
            Application.OpenURL(facebookPrivacyLink);
        }

        private void OnPressGameAnalyticsLearnMore()
        {
            Application.OpenURL(gameAnalyticsPrivacyLink);
        }

        private void OnPressVoodooLearnMore()
        {
            Application.OpenURL(voodooPrivacyLink);
        }

        private void OnPressClose()
        {
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            AdjustLearnMoreButton.onClick.RemoveAllListeners();
            FacebookLearnMoreButton.onClick.RemoveAllListeners();
            GameAnalyticsLearnMoreButton.onClick.RemoveAllListeners();
            VoodooLearnMoreButton.onClick.RemoveAllListeners();
            CloseButton.onClick.RemoveAllListeners();
        }
    }
}
