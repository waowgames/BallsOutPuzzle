using UnityEngine;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Internal.Debugger
{

    public class TSDebugWidget : MonoBehaviour
    {
        protected const float WidgetScale = 1.25f;

        protected static int Scaled(int value) => Mathf.RoundToInt(value * WidgetScale);

        protected static float Scaled(float value) => value * WidgetScale;

        protected static Vector2 ScaledVector(float x, float y) => new Vector2(Scaled(x), Scaled(y));

        protected static RectOffset ScaledOffset(int left, int right, int top, int bottom) =>
            new RectOffset(Scaled(left), Scaled(right), Scaled(top), Scaled(bottom));

        protected static void ScaleLayoutElement(LayoutElement layoutElement)
        {
            if (layoutElement == null)
                return;

            if (layoutElement.minWidth >= 0f)
                layoutElement.minWidth = Scaled(layoutElement.minWidth);

            if (layoutElement.minHeight >= 0f)
                layoutElement.minHeight = Scaled(layoutElement.minHeight);

            if (layoutElement.preferredWidth >= 0f)
                layoutElement.preferredWidth = Scaled(layoutElement.preferredWidth);

            if (layoutElement.preferredHeight >= 0f)
                layoutElement.preferredHeight = Scaled(layoutElement.preferredHeight);
        }

        protected static void ScaleLayoutGroup(HorizontalOrVerticalLayoutGroup layoutGroup)
        {
            if (layoutGroup == null)
                return;

            layoutGroup.padding = ScaledOffset(
                layoutGroup.padding.left,
                layoutGroup.padding.right,
                layoutGroup.padding.top,
                layoutGroup.padding.bottom);
            layoutGroup.spacing = Scaled(layoutGroup.spacing);
        }
    }
}