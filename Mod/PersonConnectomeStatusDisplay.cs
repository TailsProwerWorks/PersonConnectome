using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mod
{
    // Use only UI assemblies included in People Playground's mod compiler.
    // A screen-space overlay never enters the world's lighting or depth pass.
    internal sealed class PersonConnectomeStatusDisplay
    {
        private static readonly List<PersonConnectomeStatusDisplay> displays = new List<PersonConnectomeStatusDisplay>();
        private static PersonConnectomeStatusDisplay selected;
        private static int nextId, page;
        private static bool collapsed;
        private static float textScale = 1f;
        private static float panelX, panelTop = 8f, panelWidth = 510f, panelHeight = 660f;
        private readonly int id = ++nextId;
        private readonly float[] history = new float[120];
        private int historyIndex, historyCount;
        private float refreshTimer, stepMilliseconds, droppedSeconds;
        private float lastSampleTime = -1f;
        private long capturedTick;
        private ConnectomeBrain brain;
        private PeoplePlaygroundPersonAdapter adapter;
        private GameObject canvasObject;
        private Canvas canvas;
        private RectTransform panel, expanded, viewport, content;
        private RectTransform titleDragArea, resizeArea;
        private TextMeshProUGUI interactionHint;
        private ScrollRect scroll;
        private Scrollbar scrollbar;
        private TextMeshProUGUI title, toggleLabel, location, state, timing, body;
        private readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[3];
        private readonly Button[] tabs = new Button[3];
        private Button toggle, previous, next, smaller, larger;
        private TextMeshProUGUI motorHeading, historyHeading, historyScale, mapHeading, mapCaption, populationHeading, provenance;
        private readonly TextMeshProUGUI[] motorLabels = new TextMeshProUGUI[7];
        private readonly Image[] motorTracks = new Image[7], motorFills = new Image[7], motorCenters = new Image[7];
        private readonly TextMeshProUGUI[] populationLabels = new TextMeshProUGUI[8];
        private readonly Image[] populationTracks = new Image[8], populationFills = new Image[8];
        private readonly Image[] spikeBars = new Image[120];
        private readonly TextMeshProUGUI[] legendLabels = new TextMeshProUGUI[5];
        private readonly Image[] legendColors = new Image[5];
        private readonly float[] motorValues = new float[7];
        private readonly List<GameObject> motorElements = new List<GameObject>();
        private readonly List<GameObject> brainElements = new List<GameObject>();
        private Image historyBackground;
        private RawImage mapImage;
        private Texture2D mapTexture;
        private Color32[] mapBackground, mapPixels;
        private int[] mapIndexes;
        private BrainMapSample map;
        private const int MapWidth = 256, MapHeight = 320;
        private static readonly string[] Pages = { "Overview", "Senses", "Brain" };
        private static readonly string[] MotorNames = { "Walk", "Left arm", "Right arm", "Left leg", "Right leg", "Core", "Head" };
        private static readonly string[] PopulationNames = { "type:DNp09", "type:MDN", "type:DNp01", "type:MN9", "type:LC4", "type:LPLC2", "type:R1-R6", "motor" };
        private static readonly string[] Legend = { "Optic intrinsic / sensory", "Central brain intrinsic", "Ventral nerve cord intrinsic", "Motor / descending", "Other annotated classes" };
        private static readonly Color Background = new Color(.035f, .055f, .075f, 1f);
        private static readonly Color Track = new Color(.1f, .15f, .19f, 1f);
        private static readonly Color Foreground = new Color(.91f, .95f, .97f, 1f);
        private static readonly Color Accent = new Color(.4f, .86f, .95f, 1f);
        private static readonly Color[] MapColors =
        {
            new Color(.18f, .65f, .66f), new Color(.52f, .6f, .82f),
            new Color(.78f, .59f, .28f), new Color(.7f, .38f, .67f), new Color(.42f, .48f, .54f)
        };

        public PersonConnectomeStatusDisplay(Transform anchor) { }

        public void SetActive(bool active)
        {
            if (active && !displays.Contains(this)) displays.Add(this);
            if (!active)
            {
                displays.Remove(this);
                ReleaseUi();
                Array.Clear(history, 0, history.Length);
                historyIndex = historyCount = 0;
                stepMilliseconds = droppedSeconds = 0f;
                lastSampleTime = -1f;
                capturedTick = 0;
                refreshTimer = 0f;
            }
            if (selected == null || !displays.Contains(selected))
            {
                selected = displays.Count == 0 ? null : displays[0];
                if (selected != null) selected.refreshTimer = 0f;
            }
        }

        public void RecordTick(float milliseconds, float skippedSeconds, ConnectomeBrain source)
        {
            stepMilliseconds = milliseconds;
            droppedSeconds += skippedSeconds;
            lastSampleTime = Time.unscaledTime;
            history[historyIndex] = source.FiredCount;
            historyIndex = (historyIndex + 1) % history.Length;
            historyCount = Math.Min(historyCount + 1, history.Length);
        }

        public void Update(float elapsedSeconds, ConnectomeBrain source, PeoplePlaygroundPersonAdapter personAdapter)
        {
            brain = source;
            adapter = personAdapter;
            if (selected != this || Screen.width <= 0 || Screen.height <= 0) return;
            if (canvasObject == null) BuildUi();
            refreshTimer -= Mathf.Max(0f, elapsedSeconds);
            if (refreshTimer > 0f) return;
            refreshTimer = .1f;
            RefreshUi();
        }

        private void BuildUi()
        {
            canvasObject = new GameObject("Person Connectome Screen Telemetry", typeof(RectTransform));
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            canvasObject.AddComponent<GraphicRaycaster>();
            panel = CreateRect(canvasObject.transform, "Panel");
            var background = panel.gameObject.AddComponent<Image>();
            background.color = Background;
            // The panel is an ordinary UI raycast target, including its blank space.
            background.raycastTarget = true;
            title = CreateText(panel, "Person Connectome · #" + id, 18f, Accent);
            titleDragArea = CreateDragArea(panel, "Move panel", MovePanel);
            toggle = CreateButton(panel, "Collapse", ToggleCollapse, out toggleLabel);
            expanded = CreateRect(panel, "Expanded controls");
            previous = CreateButton(expanded, "Prev", () => Select(-1), out _);
            next = CreateButton(expanded, "Next", () => Select(1), out _);
            smaller = CreateButton(expanded, "A-", () => ResizeText(-.1f), out _);
            larger = CreateButton(expanded, "A+", () => ResizeText(.1f), out _);
            location = CreateText(expanded, "", 13f, Foreground);
            state = CreateText(expanded, "INITIALIZING", 14f, Accent);
            timing = CreateText(expanded, "Input: not sampled", 13f, Foreground);
            for (var i = 0; i < tabs.Length; i++)
            {
                var index = i;
                tabs[i] = CreateButton(expanded, Pages[i], () => ChangePage(index), out tabLabels[i]);
            }
            viewport = CreateRect(expanded, "Scrollable readings");
            viewport.gameObject.AddComponent<RectMask2D>();
            var hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Background;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = viewport;
            content = CreateRect(viewport, "Content");
            scroll.content = content;
            var scrollbarRect = CreateRect(expanded, "Reading position");
            scrollbarRect.gameObject.AddComponent<Image>().color = Track;
            scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            var handle = CreateRect(scrollbarRect, "Scroll handle");
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Accent;
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            body = CreateText(content, "Waiting for the first native sample.", 14f, Foreground);
            motorHeading = AddText(motorElements, "REQUESTED MOTOR OUTPUT · -1 TO +1", Accent);
            for (var i = 0; i < motorLabels.Length; i++)
            {
                motorLabels[i] = AddText(motorElements, "", Foreground);
                motorTracks[i] = AddImage(motorElements, Track);
                motorFills[i] = AddImage(motorElements, Accent);
                motorCenters[i] = AddImage(motorElements, Foreground);
            }
            historyHeading = AddText(brainElements, "SPIKES / NEURAL TICK", Accent);
            historyScale = AddText(brainElements, "", Foreground);
            historyBackground = AddImage(brainElements, Track);
            for (var i = 0; i < spikeBars.Length; i++) spikeBars[i] = AddImage(brainElements, Accent);
            mapHeading = AddText(brainElements, "SOMA MAP · RAW X/Z PROJECTION", Accent);
            mapImage = CreateRect(content, "Soma image").gameObject.AddComponent<RawImage>();
            mapImage.raycastTarget = false;
            brainElements.Add(mapImage.gameObject);
            mapCaption = AddText(brainElements, "", Foreground);
            for (var i = 0; i < Legend.Length; i++)
            {
                legendColors[i] = AddImage(brainElements, MapColors[i]);
                legendLabels[i] = AddText(brainElements, Legend[i], Foreground);
            }
            populationHeading = AddText(brainElements, "POPULATIONS · FIRED / MEMBERS\nCounts in the captured tick, not biological Hz.", Accent);
            for (var i = 0; i < populationLabels.Length; i++)
            {
                populationLabels[i] = AddText(brainElements, "", Foreground);
                populationTracks[i] = AddImage(brainElements, Track);
                populationFills[i] = AddImage(brainElements, Accent);
            }
            provenance = AddText(brainElements, "MaleCNS v1.0 · thresholded derivative\nNo anatomical human-brain or biological-realism claim.", Foreground);
            interactionHint = CreateText(expanded, "Drag title to move | Drag corner to resize", 11f, Foreground);
            resizeArea = CreateDragArea(expanded, "Resize panel", ResizePanel);
            var resizeLabel = CreateText(resizeArea, "//", 16f, Accent);
            resizeLabel.alignment = TextAlignmentOptions.Center;
            SetRect(resizeLabel.rectTransform, 0f, 0f, 28f, 24f);
            refreshTimer = 0f;
        }

        private void RefreshUi()
        {
            var hasSample = adapter != null && adapter.HasSample && adapter.IsUsable && lastSampleTime >= 0f;
            var layout = CurrentLayout();
            canvas.scaleFactor = layout.Scale;
            var width = layout.Width;
            var height = layout.Height;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f);
            panel.pivot = new Vector2(.5f, 1f);
            ApplyPanelPosition(layout);
            panel.sizeDelta = new Vector2(width, height);
            SetRect(title.rectTransform, 12f, 10f, width - 110f, 28f);
            SetRect(titleDragArea, 0f, 0f, width - 100f, 48f);
            SetRect((RectTransform)toggle.transform, width - 92f, 9f, 80f, 28f);
            toggleLabel.text = collapsed ? "Expand" : "Collapse";
            expanded.gameObject.SetActive(!collapsed);
            if (collapsed) return;
            SetRect(expanded, 0f, 48f, width, height - 48f);
            SetRect((RectTransform)previous.transform, 12f, 0f, 48f, 28f);
            SetRect((RectTransform)next.transform, 66f, 0f, 48f, 28f);
            SetRect((RectTransform)smaller.transform, width - 78f, 0f, 28f, 28f);
            SetRect((RectTransform)larger.transform, width - 44f, 0f, 28f, 28f);
            SetRect(location.rectTransform, 124f, 0f, width - 210f, 32f);
            var anchor = adapter?.StatusAnchor;
            location.text = displays.Count + " controlled" + (anchor == null ? "" : " · at " + anchor.position.x.ToString("0.0") + ", " + anchor.position.y.ToString("0.0"));
            var y = 36f;
            state.color = hasSample && (adapter.IsTerminal || adapter.LiveThreat > .05f) ? new Color(1f, .63f, .5f) : Accent;
            PlaceText(state, 12f, ref y, width - 24f, !hasSample ? "WAITING FOR NATIVE SAMPLE" : adapter.LiveState + " · " + adapter.LiveSignal + " " + adapter.LiveSignalValue.ToString("0.00"));
            var age = lastSampleTime < 0f ? "not sampled" : (Time.unscaledTime - lastSampleTime).ToString("0.00") + " s ago";
            PlaceText(timing, 12f, ref y, width - 24f, "Input: " + age + " · loop " + stepMilliseconds.ToString("0.0") + " ms\nSkipped game time: " + droppedSeconds.ToString("0.000") + " s · scroll below for more");
            for (var i = 0; i < tabs.Length; i++)
            {
                SetRect((RectTransform)tabs[i].transform, 12f + i * (width - 24f) / 3f, y, (width - 24f) / 3f - 4f, 30f);
                tabLabels[i].color = page == i ? Accent : Foreground;
            }
            y += 38f;
            var viewportHeight = Mathf.Max(1f, height - 48f - y - 30f);
            SetRect(viewport, 12f, y, width - 42f, viewportHeight);
            SetRect((RectTransform)scrollbar.transform, width - 24f, y, 12f, viewportHeight);
            var contentWidth = width - 50f;
            var contentY = 0f;
            SetVisible(motorElements, hasSample && brain != null && page == 0);
            SetVisible(brainElements, hasSample && brain != null && page == 2);
            string text;
            if (!hasSample)
            {
                text = "No native sample available.\n" + (brain == null ? "The connectome is unavailable; active control is disabled. Check the game mod log." : "Waiting for a usable person and its first physics sample.");
            }
            else if (page == 0)
            {
                text = "Body signals are normalized unless marked raw. Requests are not measured movement.\n\n" + adapter.LiveBodySummary + "\n\n" + adapter.LiveLimbSummary + "\n\n" + (brain == null ? "REQUEST: unavailable" : brain.DisplayMotorSummary);
            }
            else if (page == 1)
            {
                text = "NORMALIZED GAME READINGS\n0..1 signals unless signed or marked raw; not physical units. Unknown means unavailable.\n\n" + adapter.LiveBodySummary + "\n\n" + adapter.LiveInjurySummary + "\n\n" + adapter.LiveEnvironmentSummary + "\n\n" + adapter.LiveLiquidSummary + "\n\n" + adapter.LiveAudioSummary +
                    "\n\nDERIVED PROXIES\nDamage/blood loss, temperature bands, falling, vibration, proprioception and submerged hypoxia combine native readings.\nVision is nearest-collider line of sight × ambient light. Audio is external object playback. No semantic sight or smell.\nVitality uses valid health when its native baseline is unavailable.";
            }
            else text = brain == null ? "NEURAL: unavailable" : brain.DisplaySummary + "\n\nDERIVED INPUT DRIVES\n" + brain.DisplayInputSummary;
            PlaceText(body, 0f, ref contentY, contentWidth, text);
            if (hasSample && brain != null && page == 0) LayoutMotors(contentWidth, ref contentY);
            if (hasSample && brain != null && page == 2) LayoutBrain(contentWidth, ref contentY);
            // Preserve the current scroll position while changing the content extent.
            content.sizeDelta = new Vector2(contentWidth, contentY + 8f);
            SetRect(interactionHint.rectTransform, 12f, height - 48f - 22f, width - 54f, 18f);
            SetRect(resizeArea, width - 34f, height - 48f - 26f, 28f, 24f);
        }

        private static TelemetryLayout CurrentLayout() => TelemetryLayout.Create(Screen.width, Screen.height, textScale, collapsed, panelWidth, panelHeight);

        private void ApplyPanelPosition(TelemetryLayout layout)
        {
            panelX = layout.ClampHorizontal(Screen.width, panelX);
            panelTop = layout.ClampVertical(Screen.height, panelTop);
            panel.anchoredPosition = new Vector2(panelX / layout.Scale, -panelTop / layout.Scale);
        }

        private void MovePanel(BaseEventData data)
        {
            var pointer = data as PointerEventData;
            if (pointer == null || pointer.button != PointerEventData.InputButton.Left) return;
            panelX += pointer.delta.x;
            panelTop -= pointer.delta.y;
            ApplyPanelPosition(CurrentLayout());
        }

        private void ResizePanel(BaseEventData data)
        {
            var pointer = data as PointerEventData;
            if (pointer == null || pointer.button != PointerEventData.InputButton.Left) return;
            var before = CurrentLayout();
            panelWidth = Mathf.Clamp(before.Width + pointer.delta.x / before.Scale, 360f, Screen.width / before.Scale);
            panelHeight = Mathf.Clamp(before.Height - pointer.delta.y / before.Scale, 300f, Screen.height / before.Scale);
            var after = CurrentLayout();
            // Keep the left edge stable while dragging the lower-right corner.
            panelX += (after.Width * after.Scale - before.Width * before.Scale) * .5f;
            RefreshUi();
        }

        private static RectTransform CreateDragArea(Transform parent, string name, Action<BaseEventData> drag)
        {
            var rect = CreateRect(parent, name);
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            var trigger = rect.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            entry.callback.AddListener(data => drag(data));
            trigger.triggers.Add(entry);
            return rect;
        }

        private void LayoutMotors(float width, ref float y)
        {
            PlaceText(motorHeading, 0f, ref y, width, motorHeading.text);
            var command = brain.LastCommand;
            motorValues[0] = command.Walk; motorValues[1] = command.LeftArm; motorValues[2] = command.RightArm;
            motorValues[3] = command.LeftLeg; motorValues[4] = command.RightLeg; motorValues[5] = command.Core; motorValues[6] = command.Head;
            for (var i = 0; i < motorValues.Length; i++)
            {
                var rowY = y;
                PlaceText(motorLabels[i], 0f, ref y, width * .42f, MotorNames[i] + "  " + motorValues[i].ToString("0.00"));
                var x = width * .43f;
                var half = width * .57f * .5f;
                SetRect(motorTracks[i].rectTransform, x, rowY + 4f, half * 2f, 12f);
                SetRect(motorFills[i].rectTransform, x + half + Mathf.Min(0f, motorValues[i]) * half, rowY + 4f, Mathf.Abs(motorValues[i]) * half, 12f);
                SetRect(motorCenters[i].rectTransform, x + half, rowY + 2f, 1f, 16f);
            }
        }

        private void LayoutBrain(float width, ref float y)
        {
            RefreshMap();
            PlaceText(historyHeading, 0f, ref y, width, "SPIKES / NEURAL TICK\nWhole graph · latest " + historyCount + " ticks");
            var peak = 0f;
            for (var i = 0; i < historyCount; i++) peak = Mathf.Max(peak, history[i]);
            PlaceText(historyScale, 0f, ref y, width, "Scale peak " + peak.ToString("0") + " · one bar per processed tick");
            SetRect(historyBackground.rectTransform, 0f, y, width, 64f);
            for (var i = 0; i < spikeBars.Length; i++)
            {
                var value = i < historyCount ? history[(historyIndex - historyCount + i + history.Length) % history.Length] : 0f;
                var h = value / Mathf.Max(1f, peak) * 64f;
                SetRect(spikeBars[i].rectTransform, i * width / history.Length, y + 64f - h, Mathf.Max(1f, width / history.Length - 1f), h);
            }
            y += 76f;
            PlaceText(mapHeading, 0f, ref y, width, "SOMA MAP · RAW X/Z PROJECTION\n" + (map == null ? "No located soma data" : map.Points.Length + " sampled / " + map.LocatedCount + " located / " + map.NeuronCount + " neurons"));
            var imageWidth = Mathf.Min(width, MapWidth);
            var imageHeight = imageWidth * MapHeight / MapWidth;
            SetRect(mapImage.rectTransform, (width - imageWidth) * .5f, y, imageWidth, imageHeight);
            mapImage.texture = mapTexture;
            mapImage.enabled = mapTexture != null;
            y += imageHeight + 10f;
            PlaceText(mapCaption, 0f, ref y, width, "White = fired in captured tick " + capturedTick + ". Missing positions are omitted; dark points are not proof of inactivity between captures. Sampling changes only this view.");
            for (var i = 0; i < Legend.Length; i++)
            {
                SetRect(legendColors[i].rectTransform, 0f, y + 3f, 9f, 9f);
                PlaceText(legendLabels[i], 16f, ref y, width - 16f, Legend[i]);
            }
            PlaceText(populationHeading, 0f, ref y, width, populationHeading.text);
            for (var i = 0; i < PopulationNames.Length; i++)
            {
                var members = brain.PopulationCount(PopulationNames[i]);
                var fired = brain.PopulationFiredCount(PopulationNames[i]);
                var rowY = y;
                PlaceText(populationLabels[i], 0f, ref y, width * .6f, PopulationNames[i].Replace("type:", "") + "  " + (members == 0 ? "unavailable" : fired + " / " + members));
                SetRect(populationTracks[i].rectTransform, width * .61f, rowY + 4f, width * .39f, 10f);
                SetRect(populationFills[i].rectTransform, width * .61f, rowY + 4f, members == 0 ? 0f : width * .39f * fired / members, 10f);
            }
            PlaceText(provenance, 0f, ref y, width, provenance.text);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            return rect;
        }

        private static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string text, float size, Color color)
        {
            var label = CreateRect(parent, "Telemetry text").gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.enableWordWrapping = true;
            label.richText = false;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.text = text;
            return label;
        }

        private static void PlaceText(TextMeshProUGUI label, float x, ref float y, float width, string text)
        {
            label.text = text;
            var height = Mathf.Max(label.fontSize + 4f, label.GetPreferredValues(text, width, float.PositiveInfinity).y + 4f);
            SetRect(label.rectTransform, x, y, width, height);
            y += height + 8f;
        }

        private static Button CreateButton(Transform parent, string text, Action action, out TextMeshProUGUI label)
        {
            var rect = CreateRect(parent, text);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Track;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            label = CreateText(rect, text, 13f, Foreground);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(.5f, .5f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private TextMeshProUGUI AddText(List<GameObject> group, string text, Color color)
        {
            var label = CreateText(content, text, 14f, color);
            group.Add(label.gameObject);
            return label;
        }

        private Image AddImage(List<GameObject> group, Color color)
        {
            var image = CreateRect(content, "Telemetry bar").gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            group.Add(image.gameObject);
            return image;
        }

        private static void SetVisible(List<GameObject> elements, bool visible)
        {
            foreach (var element in elements) if (element.activeSelf != visible) element.SetActive(visible);
        }

        private void ToggleCollapse() { collapsed = !collapsed; refreshTimer = 0f; }
        private void ResizeText(float change) { textScale = Mathf.Clamp(textScale + change, .8f, 1.6f); refreshTimer = 0f; }
        private void ChangePage(int index)
        {
            page = index;
            if (page != 2) ReleaseMap();
            scroll.StopMovement();
            content.anchoredPosition = Vector2.zero;
            refreshTimer = 0f;
        }

        private static void Select(int direction)
        {
            if (displays.Count == 0) return;
            var nextIndex = (displays.IndexOf(selected) + direction + displays.Count) % displays.Count;
            selected?.ReleaseUi();
            selected = displays[nextIndex];
            selected.refreshTimer = 0f;
        }

        public void Dispose() { SetActive(false); }

        private void ReleaseUi()
        {
            if (canvasObject != null)
            {
                canvasObject.SetActive(false);
                UnityEngine.Object.Destroy(canvasObject);
            }
            canvasObject = null;
            motorElements.Clear();
            brainElements.Clear();
            ReleaseMap();
        }

        private void ReleaseMap()
        {
            if (mapImage != null) mapImage.texture = null;
            if (mapTexture != null) UnityEngine.Object.Destroy(mapTexture);
            mapTexture = null;
            map = null;
            mapBackground = mapPixels = null;
            mapIndexes = null;
        }

        private void RefreshMap()
        {
            if (brain.BrainMap == null) return;
            if (map == brain.BrainMap && capturedTick == brain.SimulationTick) return;
            if (map != brain.BrainMap)
            {
                map = brain.BrainMap;
                BuildMap();
            }
            Array.Copy(mapBackground, mapPixels, mapPixels.Length);
            for (var i = 0; i < map.Points.Length; i++)
            {
                if (brain.DidFire(map.Points[i].NeuronId)) mapPixels[mapIndexes[i]] = new Color32(255, 255, 255, 255);
            }
            capturedTick = brain.SimulationTick;
            mapTexture.SetPixels32(mapPixels);
            mapTexture.Apply(false, false);
        }

        private void BuildMap()
        {
            if (mapTexture != null) UnityEngine.Object.Destroy(mapTexture);
            mapTexture = new Texture2D(MapWidth, MapHeight, TextureFormat.RGBA32, false);
            mapTexture.filterMode = FilterMode.Point;
            mapBackground = new Color32[MapWidth * MapHeight];
            mapPixels = new Color32[mapBackground.Length];
            for (var i = 0; i < mapBackground.Length; i++) mapBackground[i] = new Color32(9, 14, 19, 255);
            mapIndexes = new int[map.Points.Length];
            if (map.Points.Length == 0) return;
            var minX = float.MaxValue; var maxX = float.MinValue;
            var minZ = float.MaxValue; var maxZ = float.MinValue;
            foreach (var point in map.Points)
            {
                minX = Mathf.Min(minX, point.X); maxX = Mathf.Max(maxX, point.X);
                minZ = Mathf.Min(minZ, point.Z); maxZ = Mathf.Max(maxZ, point.Z);
            }
            var scale = Mathf.Min((MapWidth - 12f) / Mathf.Max(1f, maxX - minX), (MapHeight - 12f) / Mathf.Max(1f, maxZ - minZ));
            var offsetX = (MapWidth - (maxX - minX) * scale) * .5f;
            var offsetZ = (MapHeight - (maxZ - minZ) * scale) * .5f;
            for (var i = 0; i < map.Points.Length; i++)
            {
                var point = map.Points[i];
                var x = (int)(offsetX + (point.X - minX) * scale);
                var z = MapHeight - 1 - (int)(offsetZ + (point.Z - minZ) * scale);
                var index = z * MapWidth + x;
                mapIndexes[i] = index;
                mapBackground[index] = MapColors[point.Category];
            }
        }
    }
}
