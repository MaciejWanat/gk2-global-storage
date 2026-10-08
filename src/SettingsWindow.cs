using System;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.UI;

namespace GK2GlobalStorage
{
    // In-game settings page: Shift+F2 (Esc closes). Changes apply immediately and are saved to the config file.
    public sealed class SettingsWindow : MonoBehaviour
    {
        private const int WindowId = 27461;

        private sealed class Row
        {
            public string name;
            public string hint;
            public Func<bool> get;
            public Action<bool> set;
            // 0 = top level, 1 = under "Global storage", 2 = under "Show items from other chests".
            public int level;
            public bool dev;
        }

        // Rows marked dev only show when DeveloperMode = true is set by hand in the config file.
        private static Row[] Rows
        {
            get
            {
                if (visibleRows == null || visibleForDev != Config.DeveloperMode)
                {
                    visibleForDev = Config.DeveloperMode;
                    visibleRows = Array.FindAll(AllRows, r => !r.dev || visibleForDev);
                }
                return visibleRows;
            }
        }

        private static Row[] visibleRows;
        private static bool visibleForDev;

        private static readonly Row[] AllRows =
        {
            new Row { name = "Global storage", hint = "Turns the whole mod on or off",
                get = () => Config.Enabled, set = v => { Perf.Flush("switched Global storage " + (v ? "ON" : "OFF")); Config.Enabled = v; } },
            new Row { name = "Building, alchemy, garden...", hint = "Building, alchemy, garden beds and prayers use items from all your chests", level = 1,
                get = () => Config.Building, set = v => Config.Building = v },
            new Row { name = "Workbenches", hint = "Workbenches you use yourself take materials from all your chests", level = 1,
                get = () => Config.Workbenches, set = v => Config.Workbenches = v },
            new Row { name = "Quests and dialogs", hint = "Items a quest or dialog asks for can come from any chest", level = 1,
                get = () => Config.Quests, set = v => Config.Quests = v },
            new Row { name = "Zombie workers", hint = "Workbenches run by zombies also take from all your chests", level = 1,
                get = () => Config.ZombieWorkers, set = v => Config.ZombieWorkers = v },
            new Row { name = "Automatic crafters", hint = "Crafters with no worker also take from all your chests; can empty them unnoticed", level = 1,
                get = () => Config.AutoCrafters, set = v => Config.AutoCrafters = v },
            new Row { name = "Fuel containers", hint = "Fuel slots of furnaces and similar objects count as chests", level = 1,
                get = () => Config.FuelContainers, set = v => { Config.FuelContainers = v; GlobalStorage.Invalidate(); } },
            new Row { name = "Pickups go to nearest chest", hint = "Items you pick up go to the nearest chest with room in this area, otherwise to your bag", level = 1,
                get = () => Config.PickupsToChest, set = v => Config.PickupsToChest = v },
            new Row { name = "Show items from other chests", hint = "Chests from other areas appear below your inventory", level = 1,
                get = () => Config.ShowOtherChests, set = v => Config.ShowOtherChests = v },
            new Row { name = "Vendor windows", hint = "Sell straight from your chests", level = 2,
                get = () => Config.Trading, set = v => Config.Trading = v },
            new Row { name = "Item pickers", hint = "Choosing seeds, organs, grave decorations and similar", level = 2,
                get = () => Config.Pickers, set = v => Config.Pickers = v },
            new Row { name = "Chest windows", hint = "Opening any chest; other chests are view only there. Slightly affects performance.", level = 2,
                get = () => Config.ChestWindows, set = v => Config.ChestWindows = v },
            new Row { name = "Performance report", hint = "Developer: every 30 s writes timings to GK2GlobalStorage-perf.txt", dev = true,
                get = () => Config.PerfReport, set = v => { Config.PerfReport = v; Perf.SetActive(v); } },
        };

        private bool open;
        private bool inputWasActive;
        private int restoreInputAtFrame = -1;
        private GameObject blocker;
        private Rect rect;
        private GUIStyle panel, title, label, hint, button, on, off, row, rowSelected;
        private int selected;
        private Vector2 scroll;
        private bool scrollToSelected;
        private float listContentHeight;
        private Rect[] rowRects = new Rect[0];

        private void Update()
        {
            if (restoreInputAtFrame >= 0 && Time.frameCount >= restoreInputAtFrame)
            {
                restoreInputAtFrame = -1;
                LazyInput.SetInputActivity(inputWasActive);
                if (LazyInput.IsInitialized)
                {
                    LazyInput.ClearAllKeysDown();
                }
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            // Keyboard: Shift+F2. Gamepad / Steam Deck: click both sticks (L3 + R3).
            bool toggle = (shift && !ctrl && Input.GetKeyDown(KeyCode.F2)) || Pad.ToggleCombo();
            if (open)
            {
                if (LazyInput.IsInitialized)
                {
                    LazyInput.ClearAllKeysDown();
                }
                if (toggle || Input.GetKeyDown(KeyCode.Escape) || Pad.Down(Pad.B) || !Allowed())
                {
                    SetOpen(false);
                    return;
                }
                Navigate();
            }
            else if (toggle && Allowed())
            {
                SetOpen(true);
            }
        }

        // Gamepad (D-pad / left stick + A) and keyboard (arrows + Enter/Space) control of the rows.
        private void Navigate()
        {
            if (selected >= Rows.Length)
            {
                selected = 0;
            }
            int move = Pad.Vertical();
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                move = -1;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                move = 1;
            }
            if (move != 0)
            {
                selected = (selected + move + Rows.Length) % Rows.Length;
                scrollToSelected = true;
                LazyAudio.PlayAndForget("gui_hover_light");
            }
            if (Pad.Down(Pad.A) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Toggle(Rows[selected]);
            }
        }

        private static bool IsEditable(Row r)
        {
            switch (r.level)
            {
                case 0: return true;
                case 1: return Config.Enabled;
                default: return Config.Enabled && Config.ShowOtherChests;
            }
        }

        private static void Toggle(Row r)
        {
            if (!IsEditable(r))
            {
                return;
            }
            LazyAudio.PlayAndForget("gui_click");
            r.set(!r.get());
            Config.Save();
        }

        private static bool Allowed()
        {
            try
            {
                MainGame.GameState state = MainGame.Instance != null ? MainGame.Instance.gameState : default;
                return MainGame.Instance != null && (state == MainGame.GameState.InGame || state == MainGame.GameState.MainMenu);
            }
            catch
            {
                return false;
            }
        }

        private void SetOpen(bool value)
        {
            if (value == open)
            {
                return;
            }
            open = value;
            if (open)
            {
                if (restoreInputAtFrame < 0)
                {
                    inputWasActive = LazyInput.IsInputActive();
                }
                restoreInputAtFrame = -1;
                LazyInput.SetInputActivity(false);
                blocker = CreateBlocker();
            }
            else
            {
                if (blocker != null)
                {
                    Destroy(blocker);
                }
                blocker = null;
                // Restore a little later so the closing key press does not reach the game.
                restoreInputAtFrame = Time.frameCount + 2;
                Config.Save();
            }
        }

        private void OnDisable()
        {
            SetOpen(false);
        }

        private void OnGUI()
        {
            if (!open)
            {
                return;
            }
            try
            {
                EnsureStyles();
                float width = Mathf.Min(720f, Screen.width - 40f);
                float height = rect.height;
                rect.width = width;
                rect.height = 0f;
                rect.x = (Screen.width - width) * 0.5f;
                rect.y = Mathf.Max(20f, (Screen.height - height) * 0.5f);
                GUI.depth = -1000;
                rect = GUILayout.Window(WindowId, rect, Draw, GUIContent.none, panel, GUILayout.Width(width));
                GUI.BringWindowToFront(WindowId);
            }
            catch (Exception ex)
            {
                SetOpen(false);
                Plugin.ReportOnce("Settings window", ex);
            }
        }

        private void Draw(int id)
        {
            GUILayout.Label("Global Storage — Settings", title);
            GUILayout.Label("v" + Plugin.Version + " (" + Plugin.Source + ")", hint);
            GUILayout.Space(10f);
            // Scrolls when the screen is short (e.g. Steam Deck); follows the gamepad/keyboard selection.
            float maxListHeight = Mathf.Max(200f, Screen.height - 260f);
            float listHeight = listContentHeight > 0f ? Mathf.Min(listContentHeight, maxListHeight) : maxListHeight;
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUILayout.Height(listHeight));
            GUILayout.BeginVertical();
            if (rowRects.Length != Rows.Length)
            {
                rowRects = new Rect[Rows.Length];
            }
            for (int i = 0; i < Rows.Length; i++)
            {
                Row r = Rows[i];
                bool value = r.get();
                GUI.enabled = IsEditable(r);
                GUILayout.BeginHorizontal(i == selected ? rowSelected : row);
                if (r.level > 0)
                {
                    GUILayout.Space(24f * r.level);
                }
                GUILayout.BeginVertical();
                GUILayout.Label(r.name, label);
                GUILayout.Label(r.hint, hint);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(value ? "ON" : "OFF", value ? on : off, GUILayout.Width(90f), GUILayout.Height(34f)))
                {
                    selected = i;
                    Toggle(r);
                }
                GUILayout.EndHorizontal();
                if (Event.current.type == EventType.Repaint)
                {
                    rowRects[i] = GUILayoutUtility.GetLastRect();
                }
                GUI.enabled = true;
                GUILayout.Space(2f);
            }
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                listContentHeight = GUILayoutUtility.GetLastRect().height + 4f;
                if (scrollToSelected && selected < rowRects.Length)
                {
                    scrollToSelected = false;
                    Rect target = rowRects[selected];
                    if (target.yMin < scroll.y)
                    {
                        scroll.y = target.yMin;
                    }
                    else if (target.yMax > scroll.y + listHeight)
                    {
                        scroll.y = target.yMax - listHeight;
                    }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Space(8f);
            GUILayout.Label("File: " + Config.FilePath, hint);
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Keyboard: Shift+F2, arrows, Enter, Esc\nGamepad / Steam Deck: L3+R3, D-pad, A toggle, B close", hint);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", button, GUILayout.Width(120f), GUILayout.Height(34f)))
            {
                SetOpen(false);
            }
            GUILayout.EndHorizontal();
        }

        private void EnsureStyles()
        {
            if (panel != null)
            {
                return;
            }
            Texture2D bg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            bg.SetPixel(0, 0, new Color(0.13f, 0.11f, 0.1f, 1f));
            bg.Apply();
            panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(24, 24, 20, 20) };
            panel.normal.background = bg;
            panel.onNormal.background = bg;
            title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
            title.normal.textColor = new Color(1f, 0.85f, 0.55f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            label.normal.textColor = new Color(0.92f, 0.9f, 0.86f);
            hint = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            hint.normal.textColor = new Color(0.65f, 0.62f, 0.58f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            on = new GUIStyle(button) { fontStyle = FontStyle.Bold };
            on.normal.textColor = on.hover.textColor = new Color(0.55f, 0.85f, 0.4f);
            off = new GUIStyle(button);
            off.normal.textColor = off.hover.textColor = new Color(0.9f, 0.4f, 0.35f);
            row = new GUIStyle { padding = new RectOffset(8, 8, 4, 4) };
            Texture2D highlight = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            highlight.SetPixel(0, 0, new Color(1f, 0.85f, 0.55f, 0.16f));
            highlight.Apply();
            rowSelected = new GUIStyle(row);
            rowSelected.normal.background = highlight;
        }

        // Full-screen dim overlay that also swallows clicks meant for the game UI underneath.
        private static GameObject CreateBlocker()
        {
            try
            {
                GameObject go = new GameObject("GK2GlobalStorage.SettingsBlocker");
                DontDestroyOnLoad(go);
                Canvas canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32767;
                go.AddComponent<GraphicRaycaster>();
                Image dim = new GameObject("Dim", typeof(RectTransform)).AddComponent<Image>();
                dim.transform.SetParent(go.transform, false);
                dim.rectTransform.anchorMin = Vector2.zero;
                dim.rectTransform.anchorMax = Vector2.one;
                dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
                dim.color = new Color(0f, 0f, 0f, 0.6f);
                dim.raycastTarget = true;
                return go;
            }
            catch (Exception ex)
            {
                Plugin.ReportOnce("Settings blocker", ex);
                return null;
            }
        }
    }
}
