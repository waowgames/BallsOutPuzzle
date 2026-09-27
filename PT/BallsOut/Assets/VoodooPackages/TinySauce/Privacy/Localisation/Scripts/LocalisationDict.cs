using System;
using System.Collections.Generic;
#if NEWTONSOFT
using Newtonsoft.Json;
#endif
using UnityEngine;

namespace VoodooPackages.TinySauce.Privacy.Localisation
{
    [CreateAssetMenu(fileName = "TranslationData", menuName = "Localization/Translation Data")]
    public class LocalisationDict : ScriptableObject
    {
        public TextAsset jsonFile;
        private Dictionary<string, Dictionary<string, string>> _dictionary;
        
        private void OnEnable()
        {
            LoadLocalisationString();
        }
        
        
        public Dictionary<string, string> GetLocalisationDict(string countryCode)
        {
            try
            {
                if (_dictionary.ContainsKey(countryCode))
                {
                    return _dictionary[countryCode];
                }
            }
            catch(NullReferenceException e)
            {
#if !UNITY_IOS && !UNITY_ANDROID
                Debug.LogError("⚠️ IMPORTANT ⚠️ Please switch your Platform to iOS or Android");
#else
                Debug.LogError("⚠️ IMPORTANT ⚠️ Please import dependencies by using the top menu : TinySauce > Import dependencies");
#endif
                return null;
            }
            
            return _dictionary["GB"];
        }
        
        public string GetLocalisationString(string countryCode, string fieldKey)
        {
            if (_dictionary.ContainsKey(countryCode) && _dictionary[countryCode].ContainsKey(fieldKey))
            {
                return _dictionary[countryCode][fieldKey];
            }
            return "";
        }

        internal void LoadLocalisationString()
        {
            
#if NEWTONSOFT
            _dictionary = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string,string>>>(jsonFile.text);
#endif
        }
    }

    public enum GdprLocalisationStrings
    {
        gdpr_text,
        gdpr_text_no_ads,
        gdpr_advertising,
        gdpr_analytics,
        gdpr_analytics_no_ads,
        gdpr_title,
        gdpr_age,
        gdpr_privacy,
        gdpr_data_sharing_partners,
        gdpr_continue_without_accepting,
        gdpr_choices_make_this_game_better,
        gdpr_continue,
        gdpr_settings,
        gdpr_options,
        gdpr_options_no_ads,
        gdpr_privacy_policy,
        gdpr_audience_description,
        gdpr_ads_personalization_description,
        gdpr_attribution_no_ads,
        gdpr_save_choices,
        gdpr_accept_all,
        gdpr_decline,
        gdpr_learn_more
    }
}