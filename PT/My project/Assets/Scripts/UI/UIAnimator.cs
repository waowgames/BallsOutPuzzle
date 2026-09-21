using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Upgrade_Systems
{
    public class UIAnimator : MonoBehaviour
    {
        [SerializeField] private List<RectTransform> uiElements;
        [SerializeField] private float duration = 0.2f;   // Her element için animasyon süresi
        [SerializeField] private float delay = 0.05f;     // Bir sonrakine geçmeden önce bekleme

        [SerializeField] private float endValue = 1f;
        
    
        [Button]
        public void PlayAnimation()
        {
            Sequence seq = DOTween.Sequence();

            foreach (var element in uiElements)
            {
                // Başlangıçta küçük ve saydam yap
                element.localScale = Vector3.zero;
                element.GetComponent<CanvasGroup>()?.DOFade(0, 0);

                // Animasyonu sıraya ekle
                seq.AppendCallback(() =>
                {
                    element.gameObject.SetActive(true);
                });

                seq.Append(element.DOScale(Vector3.one*endValue, duration).SetEase(Ease.OutBack));

                if (element.GetComponent<CanvasGroup>() != null)
                    seq.Join(element.GetComponent<CanvasGroup>().DOFade(1, duration));

                seq.AppendInterval(delay);
            }
        }

    }
}
