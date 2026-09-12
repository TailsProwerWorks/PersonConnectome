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
This is a thresholded fly-connectome simulation mapped heuristically to a game Human, not a real fly mind or a human brain. Vision is a line-of-sight/light proxy; audio is external object playback. No semantic sight, smell or biological swimming is claimed. Motion requests come from neural motor activity and are rate limited. Native game movement APIs still supply their mechanics. The adapter converts normalized joint targets to degrees/second (30 full-scale by default) and doubles walking intent before clamping to -1..1. These adjustable engineering settings need in-game tuning; stronger requests do not guarantee walking. Raw adrenaline is shown separately from its clamped neural signal. Native injury does not necessarily create an adrenaline surge; pain/shock no longer request chemical calming. Existing restorative outputs are engineered gameplay adjustments, not neural healing.

Water alone does not disable healthy limbs. Brain injury or unconsciousness is not death. Terminal state and unavailable/low consciousness suppress active motor output.

<b><color=#FFD700>SOURCE & ATTRIBUTION</color></b>
Male CNS data: CC BY 4.0
https://male-cns.janelia.org/
https://male-cns.janelia.org/download/
Prepared derivative and visualizer inspiration:
https://github.com/blendi-remade/fly-brain-minecraft
Mod source, tests and provenance:
https://github.com/TailsProwerWorks/PersonConnectome

<align="center"><size=80%><color=#CCCCCC>Build {{GIT_COMMIT}} | Author: ShadowNineX</color></size></align>

Liquids and syringes: Senses lists all liquids detected in tracked circulation, including mixtures and the 41 stock IDs inspected in game version 1.27.17. Percentages show the highest concentration in any tracked limb, including detached limbs; they are not total-body dose or effect strength. All non-blood identities feed an engineered exposure input. Unknown/custom liquids get no guessed poison or healing effect. Native zombie state is separate from reanimation-agent exposure. Water Breathing Serum is not internal water, and strength/durability serums do not request adrenaline. Native effects can persist after a liquid disappears; pain, oxygen, health and zombie state are sampled independently. No liquid is injected or sensed remotely in a nearby syringe.
