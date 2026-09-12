using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mod
{
    internal sealed class PersonConnectomeStatusDisplay
    {
        private const float LabelScale = .24f;
        private Transform anchor;
        private readonly GameObject labelObject;
        private readonly TextMeshPro label;
        private float refreshTimer;
        private string lastText;

        public PersonConnectomeStatusDisplay(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            this.anchor = anchor;
            labelObject = new GameObject("Person Connectome Live State");
            // Keep the label independent of limb rotation, mirroring and destruction.
            // The controller owns its lifetime; its world position follows the current head.
            PositionLabel();

            label = labelObject.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 2.8f;
            label.enableWordWrapping = false;
            label.autoSizeTextContainer = true;
            label.rectTransform.pivot = new Vector2(.5f, 0f);
            label.richText = false;
            label.outlineWidth = .2f;
            label.outlineColor = Color.black;
            label.sortingOrder = 1000;
            if (label.font == null && TMP_Settings.defaultFontAsset != null)
            {
                label.font = TMP_Settings.defaultFontAsset;
            }
            ConfigureForegroundRendering();
        }

        public void Update(float elapsedSeconds, ConnectomeBrain brain, PeoplePlaygroundPersonAdapter adapter)
        {
            if (label == null || labelObject == null)
            {
                return;
            }

            var currentAnchor = adapter?.StatusAnchor;
            if (currentAnchor != null) anchor = currentAnchor;
            PositionLabel();
            FaceCamera();
            refreshTimer -= Mathf.Max(0f, elapsedSeconds);
            if (refreshTimer > 0f)
            {
                return;
            }

            refreshTimer = .1f;
            var text = BuildText(brain, adapter);
            if (!String.Equals(text, lastText, StringComparison.Ordinal))
            {
                label.text = text;
                lastText = text;
            }

            label.color = GetStateColor(adapter);
        }

        public void Dispose()
        {
            if (labelObject != null)
            {
                UnityEngine.Object.Destroy(labelObject);
            }
        }

        private void FaceCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var direction = camera.transform.position - labelObject.transform.position;
            if (direction.sqrMagnitude > .001f)
            {
                labelObject.transform.rotation = Quaternion.LookRotation(-direction, camera.transform.up);
            }
        }

        private void ConfigureForegroundRendering()
        {
            var renderer = label == null ? null : label.GetComponent<Renderer>();
            if (renderer != null)
            {
                var frontLayer = renderer.sortingLayerID;
                var frontLayerValue = SortingLayer.GetLayerValueFromID(frontLayer);
                foreach (var layer in SortingLayer.layers)
                {
                    if (layer.value > frontLayerValue)
                    {
                        frontLayer = layer.id;
                        frontLayerValue = layer.value;
                    }
                }

                renderer.sortingLayerID = frontLayer;
                renderer.sortingOrder = short.MaxValue;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var material = label == null ? null : label.fontMaterial;
            if (material == null)
            {
                return;
            }

            // Use a private material so the display cannot change the game's
            // shared TMP font material or inherit scene-lighting state.
            var foregroundMaterial = new Material(material)
            {
                name = "Person Connectome Foreground Text",
                renderQueue = 4000
            };

            // TMP's distance-field shader is unlit. Prefer it explicitly so
            // scene lights and lightning flashes cannot tint the telemetry.
            var unlitShader = Shader.Find("TextMeshPro/Distance Field");
            if (unlitShader != null && foregroundMaterial.shader != unlitShader)
            {
                var unlitMaterial = new Material(unlitShader);
                unlitMaterial.CopyPropertiesFromMaterial(foregroundMaterial);
                unlitMaterial.name = foregroundMaterial.name;
                UnityEngine.Object.Destroy(foregroundMaterial);
                foregroundMaterial = unlitMaterial;
            }

            label.fontMaterial = foregroundMaterial;

            if (foregroundMaterial.HasProperty("_ZTest"))
            {
                foregroundMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
            }

            if (foregroundMaterial.HasProperty("_ZWrite"))
            {
                foregroundMaterial.SetFloat("_ZWrite", 0f);
            }
        }

        private static string BuildText(ConnectomeBrain brain, PeoplePlaygroundPersonAdapter adapter)
        {
            if (adapter == null)
            {
                return "STATE: OFFLINE\nSENSE: NO ADAPTER";
            }

            var neural = brain == null ? "NEURAL: OFFLINE" : brain.DisplaySummary;
            var motor = brain == null ? "REQUEST: OFFLINE" : brain.DisplayMotorSummary;
            var input = brain == null ? "INPUT: OFFLINE" : brain.DisplayInputSummary;
            return "PERSON CONNECTOME\nSTATE: " + adapter.LiveState + "\nSENSE: " + adapter.LiveSignal + " " + adapter.LiveSignalValue.ToString("0.00") + "\n" + adapter.LiveBodySummary + "\n" + adapter.LiveInjurySummary + "\n" + adapter.LiveEnvironmentSummary + "\n" + adapter.LiveAudioSummary + "\n" + adapter.LiveLimbSummary + "\n" + input + "\n" + motor + "\n" + neural;
        }

        private void PositionLabel()
        {
            if (anchor == null || labelObject == null) return;
            labelObject.transform.position = anchor.position + new Vector3(0f, 1.15f, 0f);
            labelObject.transform.localScale = new Vector3(LabelScale, LabelScale, LabelScale);
        }

        private static Color GetStateColor(PeoplePlaygroundPersonAdapter adapter)
        {
            if (adapter == null || !adapter.IsUsable)
            {
                return Color.gray;
            }

            if (adapter.IsTerminal)
            {
                return new Color(1f, .15f, .15f);
            }

            if (adapter.LiveThreat > .05f)
            {
                return new Color(1f, .3f, .2f);
            }

            if (adapter.LiveState == "ALERT")
            {
                return Color.yellow;
            }

            if (adapter.LiveState == "UNCONSCIOUS" || adapter.LiveState == "SEDATED")
            {
                return new Color(.7f, .7f, 1f);
            }

            if (adapter.LiveState == "BRAIN INJURED")
            {
                return new Color(1f, .65f, .2f);
            }

            return new Color(.4f, 1f, .6f);
        }
    }
}
