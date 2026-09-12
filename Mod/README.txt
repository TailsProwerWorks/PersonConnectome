<align="center"><size=150%><b><color=#00E5FF>PERSON CONNECTOME</color></b></size>
<size=85%><color=#CCCCCC>MaleCNS-derived Human controller</color></size></align>

<b><color=#FFD700>PLAY</color></b>
Enable the mod with Shady Code Rejection enabled, then spawn Person Connectome (Active) from Entities. Stock Humans remain unchanged.

The top-center screen panel stays independent of world lighting and camera zoom. Prev / Next selects a person; the number and coordinates identify it. Use A- / A+ for text size and Collapse for a small header. Drag the title to move the panel and its bottom-right corner to resize it. Use the mouse wheel or scrollbar to see all readings. Position and size last for this session.

<b><color=#FFD700>READ THE PANEL</color></b>
• Overview: body state, local limb capability and requested output. Requests are not measured movement.
• Senses: normalized game values and derived proxies. Audio retains the last detected source with seconds since detection; latest-sample sound still clears when playback stops. Unknown data is not a positive hazard.
• Brain: workload, spike history and a sampled map of real soma positions. White points fired in the displayed capture. Dark/omitted points do not prove inactivity between captures.

The graph contains 176,422 neurons and 6,287,749 retained connections. At most 8,192 located neurons appear on the map; sampling does not change the simulated graph. The scheduler caps processed work and drops stale overload work. It does not guarantee a frame rate or zero latency.

<b><color=#FFD700>LIMITS</color></b>
This is a thresholded fly-connectome simulation mapped heuristically to a game Human, not a real fly mind or a human brain. Vision is a line-of-sight/light proxy; audio is external object playback. No semantic sight, smell or biological swimming is claimed. Motion requests come from neural motor activity, with short filters and walking-mode hysteresis to bridge brief activity gaps. Active walking modes have a 0.3 request floor before rate limiting; this is an engineered actuator setting, not measured movement. Native game movement APIs still supply their mechanics. The adapter converts normalized joint targets to degrees/second (30 full-scale by default) and doubles walking intent before clamping to -1..1. These adjustable engineering settings need in-game tuning; stronger requests do not guarantee walking. Raw adrenaline is shown separately from its normalized reading. Native injury does not necessarily create an adrenaline surge; pain/shock no longer request chemical calming. Existing restorative outputs are engineered gameplay adjustments, not neural healing.

Water alone does not disable healthy limbs. Brain injury or unconsciousness is not death. Terminal state and unavailable/low consciousness suppress active motor output.

<b><color=#FFD700>SOURCE & ATTRIBUTION</color></b>
Male CNS data: CC BY 4.0
https://male-cns.janelia.org/
https://male-cns.janelia.org/download/
Prepared derivative, sensory/motor mapping reference and visualizer inspiration:
https://github.com/blendi-remade/fly-brain-minecraft
Mod source, tests and provenance:
https://github.com/TailsProwerWorks/PersonConnectome

<align="center"><size=80%><color=#CCCCCC>Build {{GIT_COMMIT}} | Author: ShadowNineX</color></size></align>

Liquids and syringes: Senses lists all liquids detected in tracked circulation, including mixtures and the 41 stock IDs inspected in game version 1.27.17. Percentages show the highest concentration in any tracked limb, including detached limbs; they are not total-body dose or effect strength. Chemical identity remains exposure telemetry, not a fabricated fly taste/smell input. Unknown/custom liquids get no guessed poison or healing effect. Native zombie state is separate from reanimation-agent exposure. Water Breathing Serum is not internal water, and strength/durability serums do not request adrenaline. Native effects can persist after a liquid disappears; pain, oxygen, health and zombie state are sampled independently. No liquid is injected or sensed remotely in a nearby syringe.

Sensory mapping: external audio (world-horizontal direction and coarse frequency bands when available), regional contact, connected joint position/motion/load, hot/cold, geometric looming and small moving-object cues feed annotated populations. Native rotation supplies a light-gated visual-motion proxy. No object identity or sound meaning is understood. Human tilt is an engineered fly gravity-sense proxy. Walking/halting uses named locomotor populations; the fly MN9 feeding neuron no longer requests human grasping. A newly sampled connected-limb health drop now supplies one engineered injury event to touch populations, adapted from the Minecraft damage response. It is not felt pain: the panel pain value still comes from the game. First/invalid/recovery samples do not invent injuries. Other health state, oxygen, zombie state and internal chemistry remain telemetry/control constraints without invented receptors. The Minecraft project uses a different integrator; these mappings do not establish its biological results or reliable human walking.

Measured global light changes also drive ON/OFF pathways; this is not a spatial retina.

Reference mapping adaptation license (fly-brain-minecraft):
MIT License
Copyright (c) 2026 Blendi Remade / fal.ai

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
