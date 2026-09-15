using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mod.Adapters;
using Mod.Core;

namespace Mod.UI
{
    // Use only UI assemblies included in People Playground's mod compiler.
    // A screen-space overlay never enters the world's lighting or depth pass.
    internal sealed class PersonConnectomeStatusDisplay : IDisposable
    {
        private static readonly List<PersonConnectomeStatusDisplay> displays = [];
        private static readonly SharedDisplayState shared = new();
        private static readonly TelemetryIdentityPool identities = new();
        private int id;
        private readonly float[] history = new float[120];
        private int historyIndex, historyCount;
        private float refreshTimer, stepMilliseconds, sensorMilliseconds, brainMilliseconds, actuatorMilliseconds, uiMilliseconds, droppedSeconds;
        private float neuralEscapeFlash;
        private float lastSampleTime = -1f;
        private long capturedTick;
        private LifBrain brain = null!;
        private PeoplePlaygroundPersonAdapter adapter = null!;
        private GameObject canvasObject = null!;
        private Canvas canvas = null!;
        private RectTransform panel = null!, expanded = null!, viewport = null!, content = null!;
        private RectTransform titleDragArea = null!, resizeArea = null!;
        private TextMeshProUGUI interactionHint = null!;
        private ScrollRect scroll = null!;
        private Scrollbar scrollbar = null!;
        private TextMeshProUGUI title = null!, toggleLabel = null!, location = null!, state = null!, escapeState = null!, timing = null!, body = null!;
        private readonly List<TextMeshProUGUI> telemetryHeadings = [];
        private readonly List<TelemetryRow> telemetryRows = [];
        private readonly List<GameObject> telemetryElements = [];
        private readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[4];
        private readonly Button[] tabs = new Button[4];
        private Button toggle = null!, previous = null!, next = null!, smaller = null!, larger = null!;
        private TextMeshProUGUI motorHeading = null!, historyHeading = null!, historyScale = null!, mapHeading = null!, mapCaption = null!, populationHeading = null!, provenance = null!;
        private RectTransform mapViewport = null!;
        private Button mapZoomOut = null!, mapReset = null!, mapZoomIn = null!;
        private float mapZoom = 1f;
        private Vector2 mapPan;
        private readonly TextMeshProUGUI[] motorLabels = new TextMeshProUGUI[21];
        private readonly Image[] motorTracks = new Image[21], motorFills = new Image[21], motorCenters = new Image[21];
        private readonly TextMeshProUGUI[] populationLabels = new TextMeshProUGUI[8];
        private readonly Image[] populationTracks = new Image[8], populationFills = new Image[8];
        private readonly Image[] spikeBars = new Image[120];
        private readonly List<TextMeshProUGUI> legendLabels = [];
        private readonly List<Image> legendColors = [];
        private readonly float[] motorValues = new float[21];
        private readonly List<GameObject> motorElements = [];
        private readonly List<GameObject> brainElements = [];
        private readonly List<GameObject> stimulationElements = [];
        private readonly List<TextMeshProUGUI> stimulationSections = [];
        private readonly StimulationRow[] stimulationRows = new StimulationRow[(int)ManualInputChannel.Count];
        private readonly ManualInputState manualInput;
        private GameObject worldLabelObject = null!;
        private TextMeshProUGUI worldLabel = null!;
        private Vector3 lastWorldLabelPosition;
        private bool hasWorldLabelPosition;
        private bool lastWorldLabelManual;
        private Image historyBackground = null!;
        private RawImage mapImage = null!;
        private Texture2D mapTexture = null!;
        private Color32[] mapBackground = null!, mapPixels = null!;
        private Color32[] mapFlashColors = null!;
        private byte[] mapFlashAges = null!;
        private int[] mapIndexes = null!;
        private int[] mapPointByNeuron = null!;
        private BrainMapSample map = null!;
        private Color[] mapColors = null!;
        private const int MapWidth = 256, MapHeight = 320;
        private const byte MapFlashLifetime = 4;
        private const float NeuralEscapeFlashSeconds = .35f;
        private static readonly string[] Pages = ["Overview", "Senses", "Brain", "Stimulation"];
        private static readonly string[] MotorNames =
        [
            "Forward", "Yaw", "Backward", "Halt", "Brake", "Jump", "Takeoff", "Landing",
            "Flight power", "Flight yaw", "Wing motor", "Groom antenna", "Groom head", "Groom leg",
            "Groom abdomen", "Feed", "Courtship", "Song", "Song pulse", "Leg motor", "Leg asymmetry"
        ];
        private static readonly string[] PopulationNames = ["type:DNp09", "type:MDN", "type:DNp01", "type:MN9", "type:LC4", "type:LPLC2", "type:R1-R6", "motor"];
        private static readonly Color Background = new(.035f, .055f, .075f, 1f);
        private static readonly Color Track = new(.1f, .15f, .19f, 1f);
        private static readonly Color Foreground = new(.91f, .95f, .97f, 1f);
        private static readonly Color Accent = new(.4f, .86f, .95f, 1f);
        private static readonly Color TelemetryStripe = new(.065f, .105f, .135f, 1f);
        private static readonly Color LatestBar = new(.98f, 1f, 1f, 1f);
        private static readonly Color EmptyBar = new(.12f, .25f, .3f, 1f);
        private static readonly Color ActiveControl = new(.18f, .55f, .65f, 1f);
        private static readonly Color[] MapFamilyColors =
        [
            new Color(.18f, .65f, .66f), new Color(.52f, .6f, .82f),
            new Color(.78f, .59f, .28f), new Color(.7f, .38f, .67f),
            new Color(.35f, .78f, .5f), new Color(.86f, .4f, .32f), new Color(.42f, .48f, .54f)
        ];

        // These preferences intentionally remain shared while the selected
        // person's panel changes, but keeping them behind a state object avoids
        // mutating static scalar fields from instance event handlers.
        private sealed class SharedDisplayState
        {
            public PersonConnectomeStatusDisplay? Selected;
            public int Page;
            public bool Collapsed;
            public float TextScale = 1f;
            public float PanelX;
            public float PanelTop = 8f;
            public float PanelWidth = 510f;
            public float PanelHeight = 660f;
        }

        private TextMeshProUGUI stimulationHeading = null!, stimulationPerson = null!, stimulationStatus = null!, stimulationExplanation = null!, stimulationResponse = null!, stimulationModeHeading = null!;
        private Button stimulationMaster = null!;
        private Button mixedMode = null!, manualOnlyMode = null!, zeroManual = null!, returnToLive = null!;

        public PersonConnectomeStatusDisplay(Transform anchor, ManualInputState inputState)
        {
            manualInput = inputState ?? new ManualInputState();
        }

        public void SetActive(bool active)
        {
            if (active && !displays.Contains(this))
            {
                id = identities.Acquire(this);
                displays.Add(this);
                CreateWorldLabel();
            }
            if (!active)
            {
                displays.Remove(this);
                ReleaseWorldLabel();
                ReleaseUi();
                identities.Release(this);
                id = 0;
                Array.Clear(history, 0, history.Length);
                historyIndex = historyCount = 0;
                stepMilliseconds = droppedSeconds = 0f;
                sensorMilliseconds = brainMilliseconds = actuatorMilliseconds = uiMilliseconds = 0f;
                neuralEscapeFlash = 0f;
                lastSampleTime = -1f;
                capturedTick = 0;
                refreshTimer = 0f;
            }
            if (shared.Selected == null || !displays.Contains(shared.Selected))
            {
                shared.Selected = displays.Count == 0 ? null : displays[0];
                shared.Selected?.refreshTimer = 0f;
            }
        }

        public void RecordTick(float sensorMs, float brainMs, float actuatorMs, float skippedSeconds, LifBrain source)
        {
            sensorMilliseconds = sensorMs;
            brainMilliseconds = brainMs;
            actuatorMilliseconds = actuatorMs;
            stepMilliseconds = sensorMs + brainMs + actuatorMs;
            droppedSeconds += skippedSeconds;
            lastSampleTime = Time.unscaledTime;
            if (source != null && source.LastCommand.NeuralEscape > 0f)
            {
                neuralEscapeFlash = NeuralEscapeFlashSeconds;
            }
            history[historyIndex] = source!.FiredCount;
            historyIndex = (historyIndex + 1) % history.Length;
            historyCount = Math.Min(historyCount + 1, history.Length);
        }

        public void Update(float elapsedSeconds, LifBrain source, PeoplePlaygroundPersonAdapter personAdapter)
        {
            brain = source;
            adapter = personAdapter;
            neuralEscapeFlash = Mathf.Max(0f, neuralEscapeFlash - Mathf.Max(0f, elapsedSeconds));
            UpdateWorldLabel();
            if (shared.Selected != this || Screen.width <= 0 || Screen.height <= 0) return;
            if (canvasObject == null) BuildUi();
            refreshTimer -= Mathf.Max(0f, elapsedSeconds);
            if (refreshTimer > 0f) return;
            refreshTimer = .1f;
            var uiStarted = Time.realtimeSinceStartup;
            RefreshUi();
            uiMilliseconds = (Time.realtimeSinceStartup - uiStarted) * 1000f;
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
            title = CreateText(panel, "Person Connectome | #" + id, 18f, Accent);
            titleDragArea = CreateDragArea(panel, "Move panel", MovePanel);
            toggle = CreateButton(panel, "Collapse", ToggleCollapse, out toggleLabel);
            expanded = CreateRect(panel, "Expanded controls");
            previous = CreateButton(expanded, "Prev", () => Select(-1), out _);
            next = CreateButton(expanded, "Next", () => Select(1), out _);
            smaller = CreateButton(expanded, "A-", () => ResizeText(-.1f), out _);
            larger = CreateButton(expanded, "A+", () => ResizeText(.1f), out _);
            location = CreateText(expanded, "", 13f, Foreground);
            state = CreateText(expanded, "BODY THREAT: waiting", 12f, Accent);
            escapeState = CreateText(expanded, "NEURAL ESCAPE: waiting", 12f, Accent);
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
            body = CreateText(content, "Waiting for the first native sample.", 13f, Foreground);
            body.richText = true;
            for (var i = 0; i < 8; i++)
            {
                var heading = CreateText(content, "", 13f, Accent);
                telemetryHeadings.Add(heading);
                telemetryElements.Add(heading.gameObject);
            }
            motorHeading = AddText(motorElements, "FLY REQUESTED OUTPUT | -1 TO +1", Accent);
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
            mapHeading = AddText(brainElements, "SOMA MAP | RAW X/Z PROJECTION", Accent);
            mapViewport = CreateRect(content, "Soma map preview");
            var mapBackgroundImage = mapViewport.gameObject.AddComponent<Image>();
            mapBackgroundImage.color = new Color(.025f, .04f, .055f, 1f);
            mapBackgroundImage.raycastTarget = true;
            mapViewport.gameObject.AddComponent<RectMask2D>();
            var mapEvents = mapViewport.gameObject.AddComponent<EventTrigger>();
            var mapDrag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            mapDrag.callback.AddListener(data => PanMap(data));
            mapEvents.triggers.Add(mapDrag);
            var mapScroll = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            mapScroll.callback.AddListener(data => ZoomMapFromScroll(data));
            mapEvents.triggers.Add(mapScroll);
            mapImage = CreateRect(mapViewport, "Soma image").gameObject.AddComponent<RawImage>();
            mapImage.raycastTarget = false;
            mapZoomOut = CreateButton(content, "-", () => SetMapZoom(mapZoom - .25f), out _);
            mapReset = CreateButton(content, "1:1", ResetMapView, out _);
            mapZoomIn = CreateButton(content, "+", () => SetMapZoom(mapZoom + .25f), out _);
            brainElements.Add(mapViewport.gameObject);
            brainElements.Add(mapZoomOut.gameObject);
            brainElements.Add(mapReset.gameObject);
            brainElements.Add(mapZoomIn.gameObject);
            mapCaption = AddText(brainElements, "", Foreground);
            populationHeading = AddText(brainElements, "POPULATIONS | FIRED / MEMBERS", Accent);
            for (var i = 0; i < populationLabels.Length; i++)
            {
                populationLabels[i] = AddText(brainElements, "", Foreground);
                populationTracks[i] = AddImage(brainElements, Track);
                populationFills[i] = AddImage(brainElements, Accent);
            }
            provenance = AddText(brainElements, "MaleCNS v1.0", Foreground);
            BuildStimulationUi();
            interactionHint = CreateText(expanded, "Drag title to move | Drag corner to resize", 11f, Foreground);
            resizeArea = CreateDragArea(expanded, "Resize panel", ResizePanel);
            var resizeLabel = CreateText(resizeArea, "//", 16f, Accent);
            resizeLabel.alignment = TextAlignmentOptions.Center;
            SetRect(resizeLabel.rectTransform, 0f, 0f, 28f, 24f);
            refreshTimer = 0f;
        }

        private void BuildStimulationUi()
        {
            stimulationHeading = AddText(stimulationElements, "MANUAL INPUT STIMULATION", Accent);
            stimulationPerson = AddText(stimulationElements, "", Foreground);
            stimulationStatus = AddText(stimulationElements, "", Accent);
            stimulationExplanation = AddText(stimulationElements, "Overrides neural input stimulation, not the body's physical state.", Foreground);
            stimulationMaster = CreateToggleButton(content, "Manual override", () => manualInput.OverrideEnabled, value =>
            {
                manualInput.SetOverrideEnabled(value);
                refreshTimer = 0f;
            }, out _);
            stimulationElements.Add(stimulationMaster.gameObject);
            stimulationModeHeading = AddText(stimulationElements, "Input mode | Mixed or Manual only", Accent);
            mixedMode = CreateButton(content, "Mixed", () =>
            {
                manualInput.SetMode(ManualInputMode.Mixed);
                refreshTimer = 0f;
            }, out _);
            manualOnlyMode = CreateButton(content, "Manual only", () =>
            {
                manualInput.SetMode(ManualInputMode.ManualOnly);
                refreshTimer = 0f;
            }, out _);
            stimulationElements.Add(mixedMode.gameObject);
            stimulationElements.Add(manualOnlyMode.gameObject);
            zeroManual = CreateButton(content, "Zero manual values", () =>
            {
                manualInput.ZeroManualValues();
                refreshTimer = 0f;
            }, out _);
            returnToLive = CreateButton(content, "Return to live", () =>
            {
                manualInput.ReturnToLive();
                refreshTimer = 0f;
            }, out _);
            stimulationElements.Add(zeroManual.gameObject);
            stimulationElements.Add(returnToLive.gameObject);
            stimulationResponse = AddText(stimulationElements, "", Foreground);

            BuildStimulationRows();
        }

        private void BuildStimulationRows()
        {
            for (var i = 0; i < ManualInputCatalog.All.Length; i++)
            {
                AddStimulationSectionIfNeeded(i);
                var descriptor = ManualInputCatalog.All[i];
                stimulationRows[i] = CreateStimulationRow(descriptor);
                stimulationElements.Add(stimulationRows[i].Root);
            }
        }

        private void AddStimulationSectionIfNeeded(int index)
        {
            var heading = index switch
            {
                0 => "LIGHT",
                3 => "AUDITORY",
                4 => "TACTILE",
                9 => "BODY / JOINT PROXIES",
                15 => "ENGINEERED VISUAL FEATURES",
                _ => null
            };
            if (heading != null) AddStimulationSection(heading);
        }

        private StimulationRow CreateStimulationRow(ManualInputChannelDescriptor descriptor)
        {
            var channel = descriptor.Channel;
            var row = new StimulationRow(content, descriptor);
            row.Name = CreateText(row.Root.transform, descriptor.Label, 13f, Accent);
            row.Details = CreateText(row.Root.transform, descriptor.Details, 11f, Foreground);
            row.ManualLabel = CreateText(row.Root.transform, "MANUAL", 10f, Accent);
            row.Override = CreateToggleButton(row.Root.transform, "Override", () => manualInput.IsSelected(channel), value =>
            {
                manualInput.SetSelected(channel, value);
                refreshTimer = 0f;
            }, out _);
            row.Strength = CreateSlider(row.Root.transform, 0f, 1f, value =>
            {
                manualInput.SetValue(channel, value);
                refreshTimer = 0f;
            });
            row.Value = CreateNumericField(row.Root.transform, value =>
            {
                if (TryParseUnit(value, out var parsed)) manualInput.SetValue(channel, parsed);
                refreshTimer = 0f;
            });
            row.LiveEffective = CreateText(row.Root.transform, "", 11f, Foreground);
            if (descriptor.Directional) AddDirectionalControls(row, channel);
            row.Continuous = CreateButton(row.Root.transform, "Continuous", () =>
            {
                manualInput.SetWaveform(channel, ManualInputWaveform.Continuous);
                refreshTimer = 0f;
            }, out _);
            row.Pulse = CreateButton(row.Root.transform, "Pulse", () =>
            {
                manualInput.SetWaveform(channel, ManualInputWaveform.Pulse);
                refreshTimer = 0f;
            }, out _);
            row.PulseLength = CreateNumericField(row.Root.transform, value =>
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed)) manualInput.SetPulseLength(channel, parsed);
                refreshTimer = 0f;
            });
            row.PulseHint = CreateText(row.Root.transform, "ticks", 11f, Foreground);
            row.Trigger = CreateButton(row.Root.transform, "Fire pulse", () =>
            {
                manualInput.TriggerPulse(channel);
                refreshTimer = 0f;
            }, out _);
            return row;
        }

        private void AddDirectionalControls(StimulationRow row, ManualInputChannel channel)
        {
            row.DirectionLabel = CreateText(row.Root.transform, "DIRECTION", 10f, Accent);
            row.Direction = CreateSlider(row.Root.transform, -1f, 1f, value =>
            {
                manualInput.SetDirection(channel, value);
                refreshTimer = 0f;
            });
            row.DirectionValue = CreateText(row.Root.transform, "", 11f, Foreground);
        }

        private void AddStimulationSection(string text)
        {
            var heading = AddText(stimulationElements, text, Accent);
            heading.fontSize = 12f;
            stimulationSections.Add(heading);
        }

        private string StimulationStatusText()
        {
            if (!adapter.HasSample) return "INACTIVE | waiting for native sample";
            if (adapter.IsTerminal) return "SUSPENDED | native terminal state";
            if (adapter.LiveState.IndexOf("INVALID", StringComparison.OrdinalIgnoreCase) >= 0) return "SUSPENDED | native health data unavailable";
            if (!manualInput.OverrideEnabled) return "INACTIVE | live input";
            return "ACTIVE | " + (manualInput.Mode == ManualInputMode.ManualOnly ? "Manual only" : "Mixed");
        }

        private static string BodyThreatStatus(bool hasSample, bool terminal, float threat)
        {
            if (!hasSample) return "waiting";
            if (terminal) return "stopped";
            return threat > .05f ? threat.ToString("0.00") : "clear";
        }

        private static string NeuralEscapeStatus(bool hasSample, bool terminal, bool escapeVisible)
        {
            if (!hasSample) return "waiting";
            if (terminal) return "stopped";
            return escapeVisible ? "requested" : "idle";
        }

        private static Color SpikeBarColor(float value, bool latest)
        {
            if (value <= 0f) return EmptyBar;
            return latest ? LatestBar : Accent;
        }

        private void LayoutStimulation(float width, ref float y)
        {
            LayoutStimulationHeader(width, ref y);
            LayoutStimulationRows(width, ref y);
        }

        private void LayoutStimulationHeader(float width, ref float y)
        {
            PlaceText(stimulationHeading, 0f, ref y, width, stimulationHeading.text);
            PlaceText(stimulationPerson, 0f, ref y, width, "Editing person #" + id + " | settings are session-local and independent");
            var statusText = StimulationStatusText();
            PlaceText(stimulationStatus, 0f, ref y, width, statusText);
            PlaceText(stimulationExplanation, 0f, ref y, width, stimulationExplanation.text);
            SetRect((RectTransform)stimulationMaster.transform, 0f, y, width, 26f);
            SetButtonColor(stimulationMaster, manualInput.OverrideEnabled ? ActiveControl : Track);
            y += 34f;
            PlaceText(stimulationModeHeading, 0f, ref y, width, stimulationModeHeading.text);
            SetRect((RectTransform)mixedMode.transform, 0f, y, width * .28f, 26f);
            SetRect((RectTransform)manualOnlyMode.transform, width * .3f, y, width * .34f, 26f);
            SetButtonColor(mixedMode, manualInput.Mode == ManualInputMode.Mixed ? ActiveControl : Track);
            SetButtonColor(manualOnlyMode, manualInput.Mode == ManualInputMode.ManualOnly ? ActiveControl : Track);
            y += 34f;
            SetRect((RectTransform)zeroManual.transform, 0f, y, width * .48f, 26f);
            SetRect((RectTransform)returnToLive.transform, width * .52f, y, width * .48f, 26f);
            y += 34f;
            PlaceText(stimulationResponse, 0f, ref y, width, FormatStimulationResponse());
        }

        private void LayoutStimulationRows(float width, ref float y)
        {
            var sectionIndex = 0;
            for (var i = 0; i < stimulationRows.Length; i++)
            {
                if (i == 0 || i == 3 || i == 4 || i == 9 || i == 15)
                {
                    var section = stimulationSections[sectionIndex++];
                    PlaceText(section, 0f, ref y, width, section.text);
                }
                var row = stimulationRows[i];
                if (row == null) continue;
                LayoutStimulationRow(row, width, ref y);
            }
        }

        private void LayoutStimulationRow(StimulationRow row, float width, ref float y)
        {
            var detailsHeight = Mathf.Max(18f, row.Details.GetPreferredValues(row.Details.text, width, float.PositiveInfinity).y + 2f);
            var manualY = 28f + detailsHeight;
            var liveY = manualY + 24f;
            var directionY = liveY + 24f;
            var lowerY = row.Descriptor.Directional ? directionY + 24f : liveY + 24f;
            var height = lowerY + 26f + 4f;
            SetRect((RectTransform)row.Root.transform, 0f, y, width, height);
            SetRect(row.Name.rectTransform, 0f, 0f, width * .7f, 22f);
            SetRect((RectTransform)row.Override.transform, width * .76f, 0f, width * .24f, 22f);
            SetRect(row.Details.rectTransform, 0f, 25f, width, detailsHeight);
            SetRect(row.ManualLabel.rectTransform, 0f, manualY + 2f, width * .18f, 18f);
            SetRect((RectTransform)row.Strength.transform, width * .2f, manualY, width * .55f, 18f);
            SetRect((RectTransform)row.Value.transform, width * .78f, manualY - 2f, width * .14f, 22f);
            SetRect(row.LiveEffective.rectTransform, 0f, liveY, width, 18f);
            if (row.Descriptor.Directional)
            {
                SetRect(row.DirectionLabel.rectTransform, 0f, directionY + 2f, width * .18f, 18f);
                SetRect((RectTransform)row.Direction.transform, width * .2f, directionY, width * .55f, 18f);
                SetRect(row.DirectionValue.rectTransform, width * .78f, directionY, width * .18f, 18f);
            }
            SetRect((RectTransform)row.Continuous.transform, 0f, lowerY, width * .27f, 26f);
            SetRect((RectTransform)row.Pulse.transform, width * .29f, lowerY, width * .22f, 26f);
            SetRect((RectTransform)row.PulseLength.transform, width * .53f, lowerY, width * .12f, 26f);
            SetRect(row.PulseHint.rectTransform, width * .66f, lowerY + 2f, width * .08f, 22f);
            SetRect((RectTransform)row.Trigger.transform, width * .76f, lowerY, width * .24f, 26f);
            var reading = manualInput.GetReading(row.Descriptor.Channel);
            var availability = reading.Available ? "" : " | UNAVAILABLE";
            var pulseStatus = manualInput.GetPulseRemaining(row.Descriptor.Channel) > 0 ? " | pulse " + manualInput.GetPulseRemaining(row.Descriptor.Channel) + " ticks" : "";
            row.LiveEffective.text = "LIVE " + Format(reading.Live) + "  ->  EFFECTIVE " + Format(reading.Effective) + pulseStatus + availability;
            if (row.Descriptor.Directional) row.DirectionValue.text = "dir " + FormatSigned(reading.EffectiveDirection);
            SetButtonColor(row.Override, manualInput.OverrideEnabled && manualInput.IsSelected(row.Descriptor.Channel) ? ActiveControl : Track);
            row.Strength.SetValueWithoutNotify(manualInput.GetValue(row.Descriptor.Channel));
            row.Strength.interactable = manualInput.OverrideEnabled;
            row.Value.interactable = manualInput.OverrideEnabled;
            row.Override.interactable = manualInput.OverrideEnabled;
            row.Continuous.interactable = manualInput.OverrideEnabled;
            row.Pulse.interactable = manualInput.OverrideEnabled;
            row.PulseLength.interactable = manualInput.OverrideEnabled;
            row.Trigger.interactable = manualInput.OverrideEnabled && manualInput.IsSelected(row.Descriptor.Channel) && manualInput.GetWaveform(row.Descriptor.Channel) == ManualInputWaveform.Pulse;
            if (!row.Value.isFocused) row.Value.text = Format(manualInput.GetValue(row.Descriptor.Channel));
            if (!row.PulseLength.isFocused) row.PulseLength.text = manualInput.GetPulseLength(row.Descriptor.Channel).ToString(CultureInfo.CurrentCulture);
            if (row.Descriptor.Directional)
            {
                row.Direction.SetValueWithoutNotify(manualInput.GetDirection(row.Descriptor.Channel));
                row.Direction.interactable = manualInput.OverrideEnabled;
            }
            SetButtonColor(row.Continuous, manualInput.GetWaveform(row.Descriptor.Channel) == ManualInputWaveform.Continuous ? ActiveControl : Track);
            SetButtonColor(row.Pulse, manualInput.GetWaveform(row.Descriptor.Channel) == ManualInputWaveform.Pulse ? ActiveControl : Track);
            row.Name.color = manualInput.OverrideEnabled ? Accent : Foreground;
            y += height + 4f;
        }

        private string FormatStimulationResponse()
        {
            var command = brain.LastCommand;
            return "LATEST NEURAL TICK\n" +
                "tick " + brain.SimulationTick + " | fired " + brain.FiredCount + " | integrated " + brain.ProcessedCount + " | decay-only " + brain.DecayedCount +
                " | queued " + brain.PendingCount + " | dropped-spikes " + brain.DroppedCount +
                "\nFLY REQUEST | forward " + FormatSigned(command.FlyForward) + " | yaw " + FormatSigned(command.FlyYaw) +
                " | backward " + FormatSigned(command.FlyBackward) + " | halt " + FormatSigned(command.FlyHalt) +
                " | brake " + FormatSigned(command.FlyBrake) + " | flight-power " + FormatSigned(command.FlyFlightPower) +
                " | feed " + FormatSigned(command.FlyFeed);
        }

        private void RefreshUi()
        {
            var hasSample = adapter != null && adapter.HasSample && adapter.IsUsable && lastSampleTime >= 0f;
            var layout = CurrentLayout();
            canvas.scaleFactor = layout.Scale;
            var width = layout.Width;
            var height = layout.Height;
            var y = 36f;
            LayoutPanelChrome(layout, width, height);
            if (shared.Collapsed) return;
            y = LayoutHeader(hasSample, width, y);
            var viewportHeight = Mathf.Max(1f, height - 48f - y - 30f);
            SetRect(viewport, 12f, y, width - 42f, viewportHeight);
            SetRect((RectTransform)scrollbar.transform, width - 24f, y, 12f, viewportHeight);
            var contentY = LayoutPageContent(hasSample, width - 50f);
            // Preserve the current scroll position while changing the content extent.
            content.sizeDelta = new Vector2(width - 50f, contentY + 8f);
            SetRect(interactionHint.rectTransform, 12f, height - 48f - 22f, width - 54f, 18f);
            SetRect(resizeArea, width - 34f, height - 48f - 26f, 28f, 24f);
        }

        private void LayoutPanelChrome(TelemetryLayout layout, float width, float height)
        {
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f);
            panel.pivot = new Vector2(.5f, 1f);
            ApplyPanelPosition(layout);
            panel.sizeDelta = new Vector2(width, height);
            SetRect(title.rectTransform, 12f, 10f, width - 110f, 28f);
            title.text = "Person Connectome | #" + id + (manualInput.OverrideEnabled ? " | MANUAL INPUT" : "");
            SetRect(titleDragArea, 0f, 0f, width - 100f, 48f);
            SetRect((RectTransform)toggle.transform, width - 92f, 9f, 80f, 28f);
            toggleLabel.text = shared.Collapsed ? "Expand" : "Collapse";
            expanded.gameObject.SetActive(!shared.Collapsed);
            if (!shared.Collapsed)
            {
                SetRect(expanded, 0f, 48f, width, height - 48f);
                SetRect((RectTransform)previous.transform, 12f, 0f, 48f, 28f);
                SetRect((RectTransform)next.transform, 66f, 0f, 48f, 28f);
                SetRect((RectTransform)smaller.transform, width - 78f, 0f, 28f, 28f);
                SetRect((RectTransform)larger.transform, width - 44f, 0f, 28f, 28f);
                SetRect(location.rectTransform, 124f, 0f, width - 210f, 32f);
                var anchor = adapter?.StatusAnchor;
                location.text = displays.Count + " controlled" + (anchor == null ? "" : " | at " + anchor.position.x.ToString("0.0") + ", " + anchor.position.y.ToString("0.0"));
            }
        }

        private float LayoutHeader(bool hasSample, float width, float y)
        {
            var neuralEscapeVisible = hasSample && !adapter.IsTerminal && neuralEscapeFlash > 0f;
            var bodyThreatVisible = hasSample && adapter.LiveThreat > .05f;
            var terminal = hasSample && adapter.IsTerminal;
            state.color = bodyThreatVisible || terminal ? new Color(1f, .63f, .5f) : Accent;
            escapeState.color = neuralEscapeVisible ? new Color(1f, .35f, .78f) : Accent;
            var statusWidth = (width - 36f) * .5f;
            var bodyY = y;
            var escapeY = y;
            PlaceText(state, 12f, ref bodyY, statusWidth, "BODY THREAT: " + BodyThreatStatus(hasSample, terminal, bodyThreatVisible ? adapter.LiveThreat : 0f));
            PlaceText(escapeState, 24f + statusWidth, ref escapeY, statusWidth, "NEURAL ESCAPE: " + NeuralEscapeStatus(hasSample, terminal, neuralEscapeVisible));
            y = Mathf.Max(bodyY, escapeY);
            var nativeSignal = !hasSample ? "WAITING FOR NATIVE SAMPLE" : adapter.LiveState + " | " + adapter.LiveSignal + " " + adapter.LiveSignalValue.ToString("0.00");
            var manualIndicator = manualInput.OverrideEnabled ? " | MANUAL INPUT ACTIVE" : "";
            var age = lastSampleTime < 0f ? "not sampled" : (Time.unscaledTime - lastSampleTime).ToString("0.00") + " s ago";
            PlaceText(timing, 12f, ref y, width - 24f, nativeSignal + manualIndicator + "\nInput: " + age + " | loop " + stepMilliseconds.ToString("0.0") + " ms (sensor " + sensorMilliseconds.ToString("0.0") + " / brain " + brainMilliseconds.ToString("0.0") + " / actuator " + actuatorMilliseconds.ToString("0.0") + ") | UI " + uiMilliseconds.ToString("0.0") + " ms\nSkipped game time: " + droppedSeconds.ToString("0.000") + " s | scroll below for more");
            for (var i = 0; i < tabs.Length; i++)
            {
                SetRect((RectTransform)tabs[i].transform, 12f + i * (width - 24f) / Pages.Length, y, (width - 24f) / Pages.Length - 4f, 30f);
                tabLabels[i].color = shared.Page == i ? Accent : Foreground;
            }
            return y + 38f;
        }

        private float LayoutPageContent(bool hasSample, float contentWidth)
        {
            var contentY = 0f;
            var showStimulation = shared.Page == 3 && hasSample && brain != null;
            SetVisible(motorElements, hasSample && brain != null && shared.Page == 0);
            SetVisible(brainElements, hasSample && brain != null && shared.Page == 2);
            SetVisible(stimulationElements, showStimulation);
            SetVisible(telemetryElements, !showStimulation);
            body.gameObject.SetActive(!showStimulation);
            BuildTelemetrySections(hasSample, out var telemetryIntro, out var telemetrySections);
            if (!showStimulation)
            {
                LayoutTelemetry(telemetryIntro, telemetrySections, contentWidth, ref contentY);
            }
            else
            {
                HideTelemetryRows();
            }
            if (hasSample && brain != null && shared.Page == 0) LayoutMotors(contentWidth, ref contentY);
            if (hasSample && brain != null && shared.Page == 2) LayoutBrain(contentWidth, ref contentY);
            if (showStimulation) LayoutStimulation(contentWidth, ref contentY);
            return contentY;
        }

        private void BuildTelemetrySections(bool hasSample, out string intro, out List<string> sections)
        {
            intro = "";
            sections = new List<string>();
            if (!hasSample)
            {
                intro = "No native sample available.\n" + (brain == null ? "The connectome is unavailable; active control is disabled. Check the game mod log." : "Waiting for a usable person and its first physics sample.");
                return;
            }
            if (shared.Page == 0)
            {
                sections.Add(adapter.LiveBodySummary);
                sections.Add(adapter.LiveLimbSummary);
                sections.Add(brain == null ? "REQUEST: unavailable" : brain.DisplayMotorSummary);
                return;
            }
            if (shared.Page == 1)
            {
                sections.Add("NORMALIZED GAME READINGS\n" + adapter.LiveBodySummary);
                sections.Add(adapter.LiveInjurySummary);
                sections.Add(adapter.LiveEnvironmentSummary);
                sections.Add(adapter.LiveLiquidSummary);
                sections.Add(adapter.LiveAudioSummary);
                sections.Add("DERIVED PROXIES\nDamage/blood loss, temperature bands, falling, vibration, proprioception and submerged hypoxia combine native readings.\nVision is nearest-collider line of sight combined with ambient + nearby light proxy. Local light uses enabled native light sprites and an approximate footprint; no texture/shadow sampling. Audio is external object playback. No semantic sight or smell.\nMin-limb-health is the lowest valid normalized health among connected limbs; it is not native Vitality.");
                return;
            }
            sections.Add(brain == null ? "NEURAL: unavailable" : brain.DisplaySummary);
            if (brain != null) sections.Add("DERIVED INPUT DRIVES\n" + brain.DisplayInputSummary);
        }

        private static TelemetryLayout CurrentLayout() => TelemetryLayout.Create(Screen.width, Screen.height, shared.TextScale, shared.Collapsed, shared.PanelWidth, shared.PanelHeight);

        private static string FormatTelemetryText(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                var escaped = EscapeTelemetryText(lines[i]);
                if (trimmed.EndsWith(":", StringComparison.Ordinal) ||
                    trimmed.StartsWith("NORMALIZED ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("DERIVED ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("NEURAL:", StringComparison.Ordinal) ||
                    trimmed.StartsWith("REQUEST", StringComparison.Ordinal) ||
                    trimmed.StartsWith("FILTERED ", StringComparison.Ordinal))
                {
                    lines[i] = "<color=#63D9F0><b>" + escaped + "</b></color>";
                }
                else
                {
                    lines[i] = escaped;
                }
            }
            return String.Join("\n", lines);
        }

        private static string EscapeTelemetryText(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static List<string> SplitTelemetryFields(string line)
        {
            var fields = new List<string>();
            var start = 0;
            var i = 0;
            while (i <= line.Length)
            {
                var delimiter = i == line.Length || (line[i] == ' ' && i + 1 < line.Length && line[i + 1] == ' ');
                if (!delimiter)
                {
                    // Every parser branch must advance i; this runs on the
                    // main UI thread and a non-advancing branch freezes the game.
                    i++;
                    continue;
                }
                var field = line.Substring(start, i - start).Trim();
                if (field.Length > 0) fields.Add(field);
                if (i < line.Length)
                {
                    var next = i + 1;
                    while (next < line.Length && line[next] == ' ') next++;
                    start = next;
                    i = next;
                    continue;
                }
                i++;
            }
            return fields;
        }

        private static bool TrySplitTelemetryField(string field, out string key, out string value)
        {
            var equals = field.IndexOf('=');
            if (equals <= 0)
            {
                key = value = "";
                return false;
            }
            key = field.Substring(0, equals).Trim();
            value = field.Substring(equals + 1).Trim();
            return key.Length > 0;
        }

        private static bool IsUnavailableValue(string value)
        {
            return value.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("unavailable", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("invalid", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("unknown ", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("unavailable ", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("invalid ", StringComparison.OrdinalIgnoreCase);
        }

        private void LayoutTelemetry(string intro, List<string> sections, float width, ref float y)
        {
            body.gameObject.SetActive(!String.IsNullOrEmpty(intro));
            if (!String.IsNullOrEmpty(intro)) PlaceText(body, 0f, ref y, width, FormatTelemetryText(intro));
            var rowIndex = 0;
            for (var i = 0; i < telemetryHeadings.Count; i++)
            {
                var heading = telemetryHeadings[i];
                if (i >= sections.Count)
                {
                    heading.gameObject.SetActive(false);
                    continue;
                }

                var section = sections[i] ?? "";
                var lineBreak = section.IndexOf('\n');
                var headingText = lineBreak < 0 ? section : section.Substring(0, lineBreak);
                var detailText = lineBreak < 0 ? "" : section.Substring(lineBreak + 1);
                var color = TelemetrySectionColor(headingText);
                heading.gameObject.SetActive(true);
                heading.color = color;
                PlaceText(heading, 0f, ref y, width, headingText);
                if (!String.IsNullOrEmpty(detailText))
                {
                    var lines = detailText.Split('\n');
                    var stripeIndex = 0;
                    foreach (var line in lines) LayoutTelemetryLine(line, width, color, ref rowIndex, ref stripeIndex, ref y);
                }
                y += 4f;
            }
            for (var i = rowIndex; i < telemetryRows.Count; i++) telemetryRows[i].Root.SetActive(false);
        }

        private void LayoutTelemetryLine(string line, float width, Color sectionColor, ref int rowIndex, ref int stripeIndex, ref float y)
        {
            var fields = SplitTelemetryFields(line);
            var fieldCount = 0;
            foreach (var field in fields)
            {
                if (!TrySplitTelemetryField(field, out string key, out string value)) continue;
                fieldCount++;
                var row = TakeTelemetryRow(ref rowIndex);
                row.Key.gameObject.SetActive(true);
                row.Value.gameObject.SetActive(true);
                row.Detail.gameObject.SetActive(false);
                row.Key.text = "<color=#00E5FF><b>" + EscapeTelemetryText(key) + "</b></color>";
                var valueColor = IsUnavailableValue(value) ? "#FFB285" : "#F2F7FA";
                row.Value.text = "<color=#AFC7CE>=</color> <color=" + valueColor + ">" + EscapeTelemetryText(value) + "</color>";
                var valueX = Mathf.Clamp(width * .56f, 184f, width - 108f);
                var keyWidth = Mathf.Max(70f, valueX - 8f);
                var valueWidth = Mathf.Max(80f, width - valueX);
                var keyHeight = row.Key.GetPreferredValues(row.Key.text, keyWidth, float.PositiveInfinity).y;
                var valueHeight = row.Value.GetPreferredValues(row.Value.text, valueWidth, float.PositiveInfinity).y;
                var height = Mathf.Max(row.Key.fontSize + 4f, Mathf.Max(keyHeight, valueHeight) + 3f);
                SetRect((RectTransform)row.Root.transform, 0f, y, width, height);
                SetRect(row.Key.rectTransform, 0f, 0f, keyWidth, height);
                SetRect(row.Value.rectTransform, valueX, 0f, valueWidth, height);
                row.BackgroundImage.color = stripeIndex++ % 2 == 1 ? TelemetryStripe : Color.clear;
                y += height + 2f;
            }
            if (fieldCount != 0) return;
            var detailRow = TakeTelemetryRow(ref rowIndex);
            detailRow.Key.gameObject.SetActive(false);
            detailRow.Value.gameObject.SetActive(false);
            detailRow.Detail.gameObject.SetActive(true);
            detailRow.BackgroundImage.color = Color.clear;
            detailRow.Detail.color = Color.Lerp(Foreground, sectionColor, .18f);
            var detailText = FormatTelemetryText(line);
            var detailHeight = Mathf.Max(detailRow.Detail.fontSize + 4f, detailRow.Detail.GetPreferredValues(detailText, width, float.PositiveInfinity).y + 4f);
            SetRect((RectTransform)detailRow.Root.transform, 0f, y, width, detailHeight);
            SetRect(detailRow.Detail.rectTransform, 0f, 0f, width, detailHeight);
            detailRow.Detail.text = detailText;
            y += detailHeight + 8f;
        }

        private TelemetryRow TakeTelemetryRow(ref int rowIndex)
        {
            if (rowIndex >= telemetryRows.Count) telemetryRows.Add(new TelemetryRow(content));
            var row = telemetryRows[rowIndex++];
            row.Root.SetActive(true);
            return row;
        }

        private void HideTelemetryRows()
        {
            foreach (var row in telemetryRows) row.Root.SetActive(false);
        }

        private static Color TelemetrySectionColor(string heading)
        {
            if (heading.StartsWith("INJURY", StringComparison.Ordinal) || heading.StartsWith("REQUEST", StringComparison.Ordinal))
            {
                return new Color(1f, .63f, .5f, 1f);
            }
            if (heading.StartsWith("ENVIRONMENT", StringComparison.Ordinal) || heading.StartsWith("CIRCULATING", StringComparison.Ordinal))
            {
                return new Color(1f, .8f, .42f, 1f);
            }
            if (heading.StartsWith("AUDIO", StringComparison.Ordinal))
            {
                return new Color(.55f, .78f, 1f, 1f);
            }
            if (heading.StartsWith("NEURAL", StringComparison.Ordinal) || heading.StartsWith("DERIVED", StringComparison.Ordinal))
            {
                return new Color(.75f, .65f, 1f, 1f);
            }
            return Accent;
        }

        private void ApplyPanelPosition(TelemetryLayout layout)
        {
            shared.PanelX = layout.ClampHorizontal(Screen.width, shared.PanelX);
            shared.PanelTop = layout.ClampVertical(Screen.height, shared.PanelTop);
            panel.anchoredPosition = new Vector2(shared.PanelX / layout.Scale, -shared.PanelTop / layout.Scale);
        }

        private void PanMap(BaseEventData data)
        {
            var pointer = data as PointerEventData;
            if (pointer == null || pointer.button != PointerEventData.InputButton.Left || mapViewport == null || mapImage == null || !mapImage.enabled) return;
            var scale = canvas == null ? 1f : Mathf.Max(.0001f, canvas.scaleFactor);
            mapPan += new Vector2(pointer.delta.x, -pointer.delta.y) / scale;
            var view = mapViewport.rect;
            var image = mapImage.rectTransform.rect;
            mapPan = ClampMapPan(mapPan, view.width, view.height, image.width, image.height);
            SetRect(mapImage.rectTransform, mapPan.x, mapPan.y, image.width, image.height);
        }

        private void SetMapZoom(float zoom)
        {
            if (mapViewport == null || mapImage == null) return;
            var view = mapViewport.rect;
            ApplyMapZoom(zoom, new Vector2(view.width, view.height) * .5f);
        }

        private void ZoomMapFromScroll(BaseEventData data)
        {
            var pointer = data as PointerEventData;
            if (pointer == null || mapViewport == null || mapImage == null || !mapImage.enabled) return;
            var amount = pointer.scrollDelta.y;
            if (Mathf.Abs(amount) < .01f) amount = pointer.scrollDelta.x;
            if (Mathf.Abs(amount) < .01f) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapViewport, pointer.position, pointer.pressEventCamera, out Vector2 localPoint)) return;
            var focus = new Vector2(localPoint.x, -localPoint.y);
            ApplyMapZoom(mapZoom + Mathf.Sign(amount) * .25f, focus);
        }

        private void ApplyMapZoom(float zoom, Vector2 focus)
        {
            var view = mapViewport.rect;
            var oldZoom = Mathf.Max(.01f, mapZoom);
            var imagePoint = (focus - mapPan) / oldZoom;
            mapZoom = Mathf.Clamp(zoom, 1f, 4f);
            mapPan = focus - imagePoint * mapZoom;
            var imageWidth = view.width;
            var imageHeight = imageWidth * MapHeight / MapWidth;
            mapPan = ClampMapPan(mapPan, view.width, view.height, imageWidth * mapZoom, imageHeight * mapZoom);
            RefreshUi();
        }

        private void ResetMapView()
        {
            mapZoom = 1f;
            mapPan = Vector2.zero;
            RefreshUi();
        }

        private static Vector2 ClampMapPan(Vector2 pan, float viewportWidth, float viewportHeight, float imageWidth, float imageHeight)
        {
            var minX = Mathf.Min(0f, viewportWidth - imageWidth);
            var minY = Mathf.Min(0f, viewportHeight - imageHeight);
            return new Vector2(Mathf.Clamp(pan.x, minX, 0f), Mathf.Clamp(pan.y, minY, 0f));
        }

        private void MovePanel(BaseEventData data)
        {
            var pointer = data as PointerEventData;
            if (pointer == null || pointer.button != PointerEventData.InputButton.Left) return;
            shared.PanelX += pointer.delta.x;
            shared.PanelTop -= pointer.delta.y;
            ApplyPanelPosition(CurrentLayout());
        }

        private void ResizePanel(BaseEventData data)
        {
            var pointer = data as PointerEventData;
            if (pointer == null || pointer.button != PointerEventData.InputButton.Left) return;
            var before = CurrentLayout();
            shared.PanelWidth = Mathf.Clamp(before.Width + pointer.delta.x / before.Scale, 360f, Screen.width / before.Scale);
            shared.PanelHeight = Mathf.Clamp(before.Height - pointer.delta.y / before.Scale, 300f, Screen.height / before.Scale);
            var after = CurrentLayout();
            // Keep the left edge stable while dragging the lower-right corner.
            shared.PanelX += (after.Width * after.Scale - before.Width * before.Scale) * .5f;
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
            motorValues[0] = command.FlyForward; motorValues[1] = command.FlyYaw; motorValues[2] = command.FlyBackward;
            motorValues[3] = command.FlyHalt; motorValues[4] = command.FlyBrake; motorValues[5] = command.FlyJump;
            motorValues[6] = command.FlyTakeoff; motorValues[7] = command.FlyLanding; motorValues[8] = command.FlyFlightPower;
            motorValues[9] = command.FlyFlightYaw; motorValues[10] = command.FlyWingMotor; motorValues[11] = command.FlyGroomAntenna;
            motorValues[12] = command.FlyGroomHead; motorValues[13] = command.FlyGroomLeg; motorValues[14] = command.FlyGroomAbdomen;
            motorValues[15] = command.FlyFeed; motorValues[16] = command.FlyCourtship; motorValues[17] = command.FlySong;
            motorValues[18] = command.FlySongPulse; motorValues[19] = command.FlyLegMotor; motorValues[20] = command.FlyLegMotorAsym;
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
            EnsureLegend();
            PlaceText(historyHeading, 0f, ref y, width, "SPIKES / NEURAL TICK");
            var peak = 0f;
            for (var i = 0; i < historyCount; i++) peak = Mathf.Max(peak, history[i]);
            var latest = historyCount == 0 ? 0f : history[(historyIndex - 1 + history.Length) % history.Length];
            PlaceText(historyScale, 0f, ref y, width, "LATEST " + latest.ToString("0") + " SPIKES | scale peak " + peak.ToString("0") + " | one bar per processed tick");
            SetRect(historyBackground.rectTransform, 0f, y, width, 64f);
            for (var i = 0; i < spikeBars.Length; i++)
            {
                var value = i < historyCount ? history[(historyIndex - historyCount + i + history.Length) % history.Length] : 0f;
                var h = value / Mathf.Max(1f, peak) * 64f;
                spikeBars[i].color = SpikeBarColor(value, i == historyCount - 1);
                SetRect(spikeBars[i].rectTransform, i * width / history.Length, y + 64f - h, Mathf.Max(1f, width / history.Length - 1f), h);
            }
            y += 76f;
            PlaceText(mapHeading, 0f, ref y, width, "SOMA ACTIVITY | RAW X/Z PROJECTION\n" + (map == null ? "No located soma data" : map.Points.Length + " displayed / " + map.LocatedCount + " located / " + map.NeuronCount + " neurons"));
            var imageWidth = Mathf.Min(width, MapWidth);
            var imageHeight = imageWidth * MapHeight / MapWidth;
            var previewHeight = Mathf.Min(imageHeight, Mathf.Max(220f, width * .78f));
            var imageX = (width - imageWidth) * .5f;
            SetRect((RectTransform)mapZoomOut.transform, imageX + imageWidth - 132f, y, 38f, 24f);
            SetRect((RectTransform)mapReset.transform, imageX + imageWidth - 90f, y, 48f, 24f);
            SetRect((RectTransform)mapZoomIn.transform, imageX + imageWidth - 38f, y, 30f, 24f);
            y += 32f;
            SetRect(mapViewport, (width - imageWidth) * .5f, y, imageWidth, previewHeight);
            var scaledWidth = imageWidth * mapZoom;
            var scaledHeight = imageHeight * mapZoom;
            mapPan = ClampMapPan(mapPan, imageWidth, previewHeight, scaledWidth, scaledHeight);
            SetRect(mapImage.rectTransform, mapPan.x, mapPan.y, scaledWidth, scaledHeight);
            mapImage.texture = mapTexture;
            mapImage.enabled = mapTexture != null;
            y += previewHeight + 10f;
            PlaceText(mapCaption, 0f, ref y, width, "TICK " + capturedTick + " | " + latest.ToString("0") + " graph spikes\nDrag to pan | scroll or use - / + to zoom | 1:1 resets.");
            if (map != null)
            {
                for (var i = 0; i < legendLabels.Count; i++)
                {
                    SetRect(legendColors[i].rectTransform, 0f, y + 3f, 9f, 9f);
                    PlaceText(legendLabels[i], 16f, ref y, width - 16f, BrainMapSample.FriendlyClassName(map.Classes[i]));
                }
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

        private static Button CreateToggleButton(Transform parent, string text, Func<bool> isOn, Action<bool> changed, out TextMeshProUGUI label)
        {
            var rect = CreateRect(parent, text + " toggle");
            var background = rect.gameObject.AddComponent<Image>();
            background.color = Track;
            var toggle = rect.gameObject.AddComponent<Button>();
            toggle.targetGraphic = background;
            label = CreateText(rect, text, 12f, Foreground);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(.5f, .5f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            toggle.onClick.AddListener(() => changed(!isOn()));
            return toggle;
        }

        private static Slider CreateSlider(Transform parent, float minimum, float maximum, Action<float> changed)
        {
            var rect = CreateRect(parent, "Stimulation slider");
            var background = rect.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            var trackRect = CreateRect(rect, "Slider track");
            trackRect.anchorMin = new Vector2(0f, .5f);
            trackRect.anchorMax = new Vector2(1f, .5f);
            trackRect.pivot = new Vector2(.5f, .5f);
            trackRect.sizeDelta = new Vector2(0f, 8f);
            var track = trackRect.gameObject.AddComponent<Image>();
            track.color = Track;
            track.raycastTarget = false;
            var fillRect = CreateRect(trackRect, "Slider fill");
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = Accent;
            fill.raycastTarget = false;
            fillRect.anchorMin = new Vector2(0f, .5f);
            fillRect.anchorMax = new Vector2(0f, .5f);
            fillRect.pivot = new Vector2(0f, .5f);
            fillRect.sizeDelta = new Vector2(0f, 8f);
            var handleArea = CreateRect(rect, "Slider handle area");
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(0f, 1f);
            handleArea.pivot = new Vector2(.5f, .5f);
            handleArea.sizeDelta = Vector2.zero;
            var handleRect = CreateRect(handleArea, "Slider thumb");
            handleRect.anchorMin = new Vector2(.5f, .5f);
            handleRect.anchorMax = new Vector2(.5f, .5f);
            handleRect.pivot = new Vector2(.5f, .5f);
            var handle = handleRect.gameObject.AddComponent<Image>();
            handle.color = Foreground;
            handleRect.sizeDelta = new Vector2(10f, 22f);
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.value = minimum;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fillRect;
            slider.handleRect = handleArea;
            slider.targetGraphic = handle;
            slider.onValueChanged.AddListener(value => changed(value));
            return slider;
        }

        private static TMP_InputField CreateNumericField(Transform parent, Action<string> endEdit)
        {
            var rect = CreateRect(parent, "Numeric stimulation value");
            var background = rect.gameObject.AddComponent<Image>();
            background.color = Track;
            var field = rect.gameObject.AddComponent<TMP_InputField>();
            var viewport = CreateRect(rect, "Numeric text viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(.5f, .5f);
            viewport.offsetMin = new Vector2(4f, 2f);
            viewport.offsetMax = new Vector2(-4f, -2f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = CreateText(viewport, "", 12f, Foreground);
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            field.textViewport = viewport;
            field.textComponent = text;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = TMP_InputField.ContentType.DecimalNumber;
            field.onEndEdit.AddListener(value => endEdit(value));
            return field;
        }

        private static void SetButtonColor(Button button, Color color)
        {
            var image = button?.GetComponent<Image>();
            image?.color = color;
        }

        private static bool TryParseUnit(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && IsFinite(value);
        }

        private static string Format(float value) => value.ToString("0.00", CultureInfo.CurrentCulture);
        private static string FormatSigned(float value) => value.ToString("+0.00;-0.00;0.00", CultureInfo.CurrentCulture);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class StimulationRow
        {
            public readonly ManualInputChannelDescriptor Descriptor;
            public readonly GameObject Root;
            public TextMeshProUGUI Name = null!, Details = null!, ManualLabel = null!, LiveEffective = null!, DirectionLabel = null!, DirectionValue = null!;
            public Button Override = null!;
            public Slider Strength = null!, Direction = null!;
            public TMP_InputField Value = null!, PulseLength = null!;
            public Button Continuous = null!, Pulse = null!, Trigger = null!;
            public TextMeshProUGUI PulseHint = null!;

            public StimulationRow(Transform parent, ManualInputChannelDescriptor descriptor)
            {
                Descriptor = descriptor;
                Root = CreateRect(parent, "Stimulation row " + descriptor.Label).gameObject;
            }
        }

        private sealed class TelemetryRow
        {
            public readonly GameObject Root;
            public readonly Image BackgroundImage;
            public readonly TextMeshProUGUI Key, Value, Detail;

            public TelemetryRow(Transform parent)
            {
                Root = CreateRect(parent, "Telemetry field row").gameObject;
                BackgroundImage = Root.AddComponent<Image>();
                BackgroundImage.raycastTarget = false;
                BackgroundImage.color = Color.clear;
                Key = CreateText(Root.transform, "", 12.5f, Accent);
                Value = CreateText(Root.transform, "", 12.5f, Foreground);
                Detail = CreateText(Root.transform, "", 12.5f, Foreground);
                Key.richText = Value.richText = Detail.richText = true;
                Root.SetActive(false);
            }
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

        private void ToggleCollapse() { shared.Collapsed = !shared.Collapsed; refreshTimer = 0f; }
        private void ResizeText(float change) { shared.TextScale = Mathf.Clamp(shared.TextScale + change, .8f, 1.6f); refreshTimer = 0f; }
        private void ChangePage(int index)
        {
            shared.Page = index;
            if (shared.Page != 2) ReleaseMap();
            scroll.StopMovement();
            content.anchoredPosition = Vector2.zero;
            refreshTimer = 0f;
        }

        private static void Select(int direction)
        {
            if (displays.Count == 0) return;
            var currentIndex = shared.Selected == null ? 0 : displays.IndexOf(shared.Selected);
            var nextIndex = (currentIndex + direction + displays.Count) % displays.Count;
            shared.Selected?.ReleaseUi();
            shared.Selected = displays[nextIndex];
            shared.Selected.refreshTimer = 0f;
        }

        public void Dispose() { SetActive(false); }

        private void CreateWorldLabel()
        {
            if (worldLabelObject != null) return;
            worldLabelObject = new GameObject("Person Connectome Hover Label #" + id, typeof(RectTransform));
            var labelCanvas = worldLabelObject.AddComponent<Canvas>();
            labelCanvas.renderMode = RenderMode.WorldSpace;
            labelCanvas.sortingOrder = short.MaxValue;
            worldLabel = CreateText(worldLabelObject.transform, "#" + id, 24f, Accent);
            worldLabel.alignment = TextAlignmentOptions.Center;
            worldLabel.rectTransform.anchorMin = Vector2.zero;
            worldLabel.rectTransform.anchorMax = Vector2.one;
            worldLabel.rectTransform.pivot = new Vector2(.5f, .5f);
            worldLabel.rectTransform.offsetMin = worldLabel.rectTransform.offsetMax = Vector2.zero;
            var rect = (RectTransform)worldLabelObject.transform;
            rect.sizeDelta = new Vector2(180f, 30f);
            worldLabelObject.transform.localScale = Vector3.one * .01f;
            UpdateWorldLabel();
        }

        private void UpdateWorldLabel()
        {
            if (worldLabelObject == null) return;
            var currentAnchor = adapter?.StatusAnchor;
            if (currentAnchor == null)
            {
                if (worldLabelObject.activeSelf) worldLabelObject.SetActive(false);
                hasWorldLabelPosition = false;
                return;
            }

            if (!worldLabelObject.activeSelf) worldLabelObject.SetActive(true);
            var position = currentAnchor.position + new Vector3(0f, 1.35f, 0f);
            if (!hasWorldLabelPosition || (lastWorldLabelPosition - position).sqrMagnitude > .000001f)
            {
                worldLabelObject.transform.position = position;
                lastWorldLabelPosition = position;
                hasWorldLabelPosition = true;
            }

            var manual = manualInput.OverrideEnabled;
            if (manual != lastWorldLabelManual)
            {
                worldLabel.text = "#" + id + (manual ? "  MANUAL" : "");
                worldLabel.color = manual ? LatestBar : Accent;
                lastWorldLabelManual = manual;
            }
        }

        private void ReleaseWorldLabel()
        {
            if (worldLabelObject != null) UnityEngine.Object.Destroy(worldLabelObject);
            worldLabelObject = null!;
            worldLabel = null!;
            hasWorldLabelPosition = false;
            lastWorldLabelPosition = default;
            lastWorldLabelManual = false;
        }

        private void ReleaseUi()
        {
            if (canvasObject != null)
            {
                canvasObject.SetActive(false);
                UnityEngine.Object.Destroy(canvasObject);
            }
            canvasObject = null!;
            motorElements.Clear();
            brainElements.Clear();
            telemetryHeadings.Clear();
            telemetryRows.Clear();
            telemetryElements.Clear();
            stimulationElements.Clear();
            stimulationSections.Clear();
            legendLabels.Clear();
            legendColors.Clear();
            Array.Clear(stimulationRows, 0, stimulationRows.Length);
            stimulationHeading = stimulationPerson = stimulationStatus = stimulationExplanation = stimulationResponse = stimulationModeHeading = null!;
            stimulationMaster = mixedMode = manualOnlyMode = zeroManual = returnToLive = null!;
            ReleaseMap();
        }

        private void ReleaseMap()
        {
            mapImage?.texture = null;
            if (mapTexture != null) UnityEngine.Object.Destroy(mapTexture);
            mapTexture = null!;
            map = null!;
            mapColors = null!;
            mapBackground = mapPixels = null!;
            mapFlashColors = null!;
            mapFlashAges = null!;
            mapIndexes = null!;
            mapPointByNeuron = null!;
        }

        private void RefreshMap()
        {
            var currentMap = brain.BrainMap;
            if (currentMap == null) return;
            var currentTick = brain.SimulationTick;
            var reset = map == currentMap && currentTick < capturedTick;
            if (reset) ResetMapFlashes();
            if (!reset && map == currentMap && capturedTick == currentTick) return;
            EnsureMap(currentMap);
            AgeMapFlashes();
            Array.Copy(mapBackground, mapPixels, mapPixels.Length);
            MarkFiredNeurons();
            ApplyMapFlashColors();
            capturedTick = currentTick;
            mapTexture.SetPixels32(mapPixels);
            mapTexture.Apply(false, false);
        }

        private void ResetMapFlashes()
        {
            Array.Clear(mapFlashAges, 0, mapFlashAges.Length);
            Array.Clear(mapFlashColors, 0, mapFlashColors.Length);
        }

        private void EnsureMap(BrainMapSample currentMap)
        {
            if (map == currentMap) return;
            map = currentMap;
            mapColors = CreateMapColors(map.Classes);
            BuildMap();
        }

        private void AgeMapFlashes()
        {
            for (var i = 0; i < mapFlashAges.Length; i++)
            {
                if (mapFlashAges[i] > 0) mapFlashAges[i]--;
            }
        }

        private void MarkFiredNeurons()
        {
            var firedNeurons = brain.FiredNeurons;
            for (var i = 0; i < firedNeurons.Count; i++)
            {
                var neuronId = firedNeurons[i];
                if (neuronId < 0 || neuronId >= mapPointByNeuron.Length) continue;
                var pointIndex = mapPointByNeuron[neuronId];
                if (pointIndex >= 0) MarkMapFlash(mapIndexes[pointIndex], mapColors[map.Points[pointIndex].Category]);
            }
        }

        private void ApplyMapFlashColors()
        {
            for (var i = 0; i < mapPixels.Length; i++)
            {
                if (mapFlashAges[i] > 0) mapPixels[i] = MapFlashColor(mapFlashAges[i], mapFlashColors[i]);
            }
        }

        private void BuildMap()
        {
            if (mapTexture != null) UnityEngine.Object.Destroy(mapTexture);
            mapTexture = new Texture2D(MapWidth, MapHeight, TextureFormat.RGBA32, false);
            mapTexture.filterMode = FilterMode.Point;
            mapBackground = new Color32[MapWidth * MapHeight];
            mapPixels = new Color32[mapBackground.Length];
            mapFlashColors = new Color32[mapBackground.Length];
            mapFlashAges = new byte[mapBackground.Length];
            for (var i = 0; i < mapBackground.Length; i++) mapBackground[i] = new Color32(9, 14, 19, 255);
            mapIndexes = new int[map.Points.Length];
            mapPointByNeuron = new int[map.NeuronCount];
            for (var i = 0; i < mapPointByNeuron.Length; i++) mapPointByNeuron[i] = -1;
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
                mapPointByNeuron[point.NeuronId] = i;
                mapBackground[index] = mapColors[point.Category];
            }
        }

        private void EnsureLegend()
        {
            if (map == null || legendLabels.Count == map.Classes.Length) return;
            for (var i = 0; i < legendLabels.Count; i++)
            {
                UnityEngine.Object.Destroy(legendLabels[i].gameObject);
                UnityEngine.Object.Destroy(legendColors[i].gameObject);
            }
            legendLabels.Clear();
            legendColors.Clear();
            for (var i = 0; i < map.Classes.Length; i++)
            {
                legendColors.Add(AddImage(brainElements, mapColors[i]));
                legendLabels.Add(AddText(brainElements, BrainMapSample.FriendlyClassName(map.Classes[i]), Foreground));
            }
        }

        private static Color[] CreateMapColors(string[] classes)
        {
            var colors = new Color[classes.Length];
            var familyCounts = new int[MapFamilyColors.Length];
            for (var i = 0; i < classes.Length; i++)
            {
                var family = ClassColorFamily(classes[i]);
                var variant = familyCounts[family]++;
                var shade = 1f - Mathf.Min(.18f, variant * .035f);
                colors[i] = new Color(
                    MapFamilyColors[family].r * shade,
                    MapFamilyColors[family].g * shade,
                    MapFamilyColors[family].b * shade,
                    1f);
            }
            return colors;
        }

        private static int ClassColorFamily(string superclass)
        {
            if (String.IsNullOrEmpty(superclass) || superclass == "unannotated") return 6;
            if (superclass.StartsWith("visual_", StringComparison.Ordinal) || superclass.StartsWith("ol_", StringComparison.Ordinal)) return 0;
            if (superclass.Contains("endocrine")) return 5;
            if (superclass.StartsWith("sensory_", StringComparison.Ordinal)) return 4;
            if (IsMotorSuperclass(superclass)) return 3;
            if (superclass.StartsWith("cb_", StringComparison.Ordinal)) return 1;
            if (superclass.StartsWith("vnc_", StringComparison.Ordinal) || superclass == "ENS") return 2;
            return 6;
        }

        private static bool IsMotorSuperclass(string superclass)
        {
            return superclass.Contains("motor") ||
                superclass.Contains("descending") ||
                superclass.Contains("ascending") ||
                superclass.Contains("efferent");
        }

        private void MarkMapFlash(int index, Color color)
        {
            var centerX = index % MapWidth;
            var centerY = index / MapWidth;
            for (var y = centerY - 1; y <= centerY + 1; y++)
            {
                for (var x = centerX - 1; x <= centerX + 1; x++)
                {
                    MarkMapFlashPixel(centerX, centerY, x, y, color);
                }
            }
        }

        private void MarkMapFlashPixel(int centerX, int centerY, int x, int y, Color color)
        {
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) return;
            var distance = Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY);
            if (distance > 1) return;
            var age = distance == 0 ? MapFlashLifetime : (byte)(MapFlashLifetime - 2);
            var target = y * MapWidth + x;
            if (mapFlashAges[target] >= age) return;
            mapFlashAges[target] = age;
            mapFlashColors[target] = (Color32)color;
        }

        private static Color32 MapFlashColor(byte age, Color32 categoryColor)
        {
            var category = (Color)categoryColor;
            var brightness = Mathf.Clamp01(age / (float)MapFlashLifetime);
            var flash = Color.Lerp(category, Color.white, .25f + brightness * .4f);
            flash.a = 1f;
            return (Color32)flash;
        }
    }
}
