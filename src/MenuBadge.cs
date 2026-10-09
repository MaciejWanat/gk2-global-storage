using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Main menu, top right: mod name and version, whether it is on, and where it was installed from.
    // Drawn as a copy of the game's own credits label, so it uses the menu font and colors.
    // If another mod already shows a label in that corner, this one goes right below it.
    public sealed class MenuBadge : MonoBehaviour
    {
        private const float Margin = 12f;
        private const float Top = 8f;
        private const float Gap = 6f;

        // How far from the right edge something may sit and still count as being in the corner.
        private const float CornerWidth = 500f;

        private TMP_Text label;
        private bool failed;
        private float nextCheck;
        private string accent = "#F0A24B";
        private float line = 24f;
        private string source;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }
            nextCheck = Time.unscaledTime + 0.5f;
            try
            {
                if (!AtMenu())
                {
                    if (label != null && label.gameObject.activeSelf)
                    {
                        label.gameObject.SetActive(false);
                    }
                    return;
                }
                if (label == null && (failed || !Create()))
                {
                    return;
                }
                if (!label.gameObject.activeSelf)
                {
                    label.gameObject.SetActive(true);
                }
                string text = Text();
                if (label.text != text)
                {
                    label.text = text;
                }
                ((RectTransform)label.transform).anchoredPosition = new Vector2(-Margin, Below());
            }
            catch (Exception ex)
            {
                failed = true;
                Plugin.ReportOnce("Main menu badge", ex);
            }
        }

        private void OnDestroy()
        {
            if (label != null)
            {
                Destroy(label.gameObject);
            }
        }

        private static bool AtMenu()
        {
            return MainGame.Instance != null && MainGame.Instance.gameState == MainGame.GameState.MainMenu;
        }

        private string Text()
        {
            string status;
            if (Plugin.Patched < 0)
            {
                status = Accent("off, failed to load (see Player.log)", "#E6503F");
            }
            else if (!Config.Enabled)
            {
                status = Accent("off (Shift+F2)", "#F2BF33");
            }
            else if (Plugin.Patched < Plugin.PatchCount)
            {
                status = Accent("partly on (" + Plugin.Patched + "/" + Plugin.PatchCount + ")", "#F2BF33");
            }
            else
            {
                status = Accent("on");
            }
            if (source == null)
            {
                source = Plugin.InstallSource;
            }
            return Accent("Global Storage") + " v" + Plugin.Version
                + "\nstatus: " + status
                + "\ninstalled: " + Accent(source);
        }

        private string Accent(string value, string color = null)
        {
            return "<color=" + (color ?? accent) + ">" + value + "</color>";
        }

        // Top edge for this label: below anything else anchored to the same top right corner of the menu canvas
        // (e.g. labels of other mods).
        private float Below()
        {
            Transform parent = label.transform.parent;
            float y = -Top;
            Vector2 corner = new Vector2(1f, 1f);
            foreach (Transform child in parent)
            {
                if (child == label.transform || !child.gameObject.activeInHierarchy || !(child is RectTransform rt)
                    || rt.anchorMin != corner || rt.anchorMax != corner || rt.anchoredPosition.x < -CornerWidth)
                {
                    continue;
                }
                TMP_Text text = child.GetComponent<TMP_Text>();
                float height = text != null ? Mathf.Min(text.preferredHeight, rt.rect.height) : rt.rect.height;
                float bottom = rt.anchoredPosition.y - height * rt.pivot.y;
                y = Mathf.Min(y, bottom - Gap);
            }
            return y;
        }

        // Copies the game's credits label (the one that mentions the publisher) on the main menu canvas.
        private bool Create()
        {
            TMP_Text template = null;
            foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (t != null && t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text)
                    && (t.text.IndexOf("tinyBuild", StringComparison.OrdinalIgnoreCase) >= 0
                        || t.text.IndexOf("Lazy Bear", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    template = t;
                    break;
                }
            }
            Canvas canvas = template != null && template.canvas != null ? template.canvas.rootCanvas : null;
            if (canvas == null)
            {
                // The menu is still loading; try again on the next check.
                return false;
            }
            Match match = Regex.Match(template.text, "<color=(#[0-9A-Fa-f]{6,8})>");
            if (match.Success)
            {
                accent = match.Groups[1].Value;
            }
            line = ((RectTransform)template.transform).rect.height;

            GameObject go = Instantiate(template.gameObject, canvas.transform, false);
            go.name = "GK2GlobalStorage.MenuBadge";
            foreach (Component c in go.GetComponents<Component>())
            {
                if (!(c is RectTransform) && !(c is TMP_Text) && !(c is CanvasRenderer))
                {
                    DestroyImmediate(c);
                }
            }
            foreach (Transform child in go.transform)
            {
                Destroy(child.gameObject);
            }
            label = go.GetComponent<TMP_Text>();
            label.alignment = TextAlignmentOptions.TopRight;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            RectTransform rect = (RectTransform)go.transform;
            rect.pivot = rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(420f, line * 4f);
            return true;
        }
    }
}
