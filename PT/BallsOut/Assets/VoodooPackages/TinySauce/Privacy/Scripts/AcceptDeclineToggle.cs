using System;
using UnityEngine;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Privacy
{

    public class AcceptDeclineToggle : MonoBehaviour
    {
        [SerializeField] private Image acceptButtonBackground;
        [SerializeField] private Image declineButtonBackground;
        [SerializeField] private Text acceptButtonText;
        [SerializeField] private Text declineButtonText;
        
        [SerializeField] private Color acceptButtonColor;
        [SerializeField] private Color declineButtonColor;
        
        public bool Consent => _consent;
        
        private bool _consent;

        private readonly Color _baseButtonColor = new Color(0.8588235294117647f, 0.8705882352941177f, 0.8980392156862745f, 1);

        public void SetConsent(bool consent)
        {
            _consent =  consent;

            if (consent)
            {
                acceptButtonBackground.color = acceptButtonColor;
                declineButtonBackground.color = _baseButtonColor;
                
                acceptButtonText.color = Color.white;
                declineButtonText.color = Color.black;
            }
            else
            {
                acceptButtonBackground.color = _baseButtonColor;
                declineButtonBackground.color = declineButtonColor;
                
                acceptButtonText.color = Color.black;
                declineButtonText.color = Color.white;
            }
        }
    }

}