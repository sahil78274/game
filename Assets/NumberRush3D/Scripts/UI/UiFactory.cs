using NumberRush3D.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRush3D.UI
{
    /// <summary>
    /// Small runtime UI toolkit used by the panels that build themselves (level select, pause,
    /// level complete / failed).
    ///
    /// It deliberately mirrors the editor generator: legacy <see cref="Text"/> + <see cref="RawImage"/>
    /// + <see cref="Button"/>, no sprites to import and no TextMeshPro dependency, so the same look
    /// is available whether the UI was authored in the scene or created at runtime.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color CardColor = new Color(0.06f, 0.08f, 0.15f, 0.98f);
        public static readonly Color DimColor = new Color(0.01f, 0.02f, 0.05f, 0.78f);
        public static readonly Color CaptionColor = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color PrimaryButton = new Color(0.16f, 0.45f, 0.95f, 1f);
        public static readonly Color SecondaryButton = new Color(0.18f, 0.2f, 0.28f, 1f);
        public static readonly Color SuccessColor = new Color(0.16f, 0.85f, 0.36f, 1f);
        public static readonly Color DangerColor = new Color(0.93f, 0.24f, 0.26f, 1f);
        public static readonly Color GoldColor = new Color(1f, 0.78f, 0.25f, 1f);

        private static Font _font;

        /// <summary>Built-in legacy font, cached.</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = RuntimeAssets.LegacyFont;
                }

                return _font;
            }
        }

        /// <summary>Creates a RectTransform with the given anchors.</summary>
        public static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        /// <summary>Creates a child that fills its parent.</summary>
        public static RectTransform MakeStretch(string name, Transform parent)
        {
            RectTransform rect = MakeRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Full screen dim background that swallows taps behind a panel.</summary>
        public static RawImage MakeDim(Transform parent, float alpha = 0.78f)
        {
            RawImage dim = MakePanel("Dim", parent, new Color(DimColor.r, DimColor.g, DimColor.b, alpha), true);
            Stretch(dim.rectTransform);
            return dim;
        }

        /// <summary>Rectangular, sprite free panel.</summary>
        public static RawImage MakePanel(string name, Transform parent, Color color, bool raycastTarget = false)
        {
            RectTransform rect = MakeRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>Centred card used by every panel.</summary>
        public static RectTransform MakeCard(Transform parent, Vector2 size)
        {
            RawImage card = MakePanel("Card", parent, CardColor, true);
            RectTransform rect = card.rectTransform;
            rect.sizeDelta = size;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        /// <summary>Text label, bold, no raycast.</summary>
        public static Text MakeLabel(Transform parent, string name, string text, int fontSize, Color color, TextAnchor anchor)
        {
            RectTransform rect = MakeRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 60f));

            Text label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Filled button with a bold centred caption.</summary>
        public static Button MakeButton(Transform parent, string name, string caption, Color background, int fontSize = 40)
        {
            RawImage background = MakePanel(name, parent, background, true);
            RectTransform rect = background.rectTransform;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;

            Text label = MakeLabel(rect, "Label", caption, fontSize, Color.white, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return button;
        }

        /// <summary>Positions a rect (anchor + pivot in the same place).</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        /// <summary>Positions a rect with an explicit pivot (used for centred rows).</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Sets the caption of a button created by <see cref="MakeButton"/>.</summary>
        public static void SetButtonCaption(Button button, string caption)
        {
            if (button == null)
            {
                return;
            }

            Text[] labels = button.GetComponentsInChildren<Text>(true);
            if (labels.Length > 0)
            {
                labels[0].text = caption;
            }
        }

        /// <summary>Colours + enables/disables a button created by <see cref="MakeButton"/>.</summary>
        public static void SetButtonState(Button button, Color background, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = background;
            }
        }
    }
}
