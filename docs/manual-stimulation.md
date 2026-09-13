# Manual brain-input stimulation

The telemetry panel's **Stimulation** tab lets you experiment with external neural input while leaving the connectome, motor decoder and native body state in control.

## Open the controls

Spawn one or more **Person Connectome (Active)** Humans. Open the screen panel and choose **Stimulation**. The panel and the floating `#ID` label above each controlled Human use the same session-local number. Prev / Next changes which person's settings are being edited; settings and active pulses remain owned by that person.

The tab reports one of these states:

- **INACTIVE · live input**: the master override is off, so normal native sensing is used.
- **ACTIVE · Mixed**: checked channels replace their live encoded values; unchecked channels remain live.
- **ACTIVE · Manual only**: checked channels use their manual values and all unselected supported external input routes contribute zero new stimulation, including when no channels are selected.
- **SUSPENDED**: the existing native lifecycle has stopped or suspended input, such as terminal state or unavailable health data.

The header also shows **MANUAL INPUT** while the selected person's override is active, including when the panel is collapsed and while viewing another tab.

## Configure a channel

Check **Manual override**, select **Mixed** or **Manual only**, then use a channel row:

- **Override** selects that channel for manual resolution.
- The strength slider and numeric field use finite normalized amplitude from `0.00` to `1.00`. They edit the same value.
- **LIVE → EFFECTIVE** shows the live encoded value and the value resolved at the latest processed neural tick. An unavailable route is labeled unavailable; no replacement neurons are invented.
- Direction controls appear only for routes with existing lateral weighting. They use `-1.00` for left-biased, `0.00` for bilateral and `+1.00` for right-biased world-horizontal weighting. This is an engineering projection, not verified anatomical localization.
- **Continuous** offers the selected value on every valid neural tick. **Pulse** keeps the selected value at zero between triggers. Set a pulse length from 1 to 200 **neural ticks**, then press **Fire pulse**. A trigger arms the next valid tick; another trigger replaces the current pulse rather than queuing more work.

The supported rows map to the existing annotated routes: light and light ON/OFF entries, auditory, regional tactile, gravity/tilt, joint position/motion/load, warm/cool, engineered expanding/looming/small-moving visual features, and optic roll. The details text on each row names the target population(s). A manual feature route can therefore be stimulated even when its corresponding object, sound, rotation or temperature condition is absent.

**Zero manual values** clears stored amplitudes and pulses but keeps channel selections and mode. **Return to live** unchecks the master override and cancels pulses while retaining draft values for later experiments.

## What the controls do not do

Manual values are resolved at the neural input boundary. They do not change native temperature, health, damage, consciousness, oxygen, audio history, liquids or visual observations. They do not directly fire neurons, set membrane potentials, set walking or limb commands, heal, inject chemistry or bypass the scheduler. The graph still propagates activity and the existing decoder still produces any motor request.

The Senses page continues to show actual native observations. The Stimulation response section shows the latest neural tick, actual fired-neuron count, queued/processed/dropped work and current motor request. A configured or effective input is not proof that a neuron fired. The Brain page remains available for the sampled activity map and per-tick history. Zero external input also does not instantly silence recurrent activity already present in the graph.

Manual state is session-local and per person. Collapsing, changing tabs, resizing, changing text scale or selecting another person does not reset an active experiment. Disabling, destroying or reaching a terminal/invalid-health state disarms manual input and cancels pulses; re-enable does not replay an old pulse. Native movement-permission, consciousness, freeze, limb eligibility and stop/cleanup rules remain authoritative.

Strong sustained input can fill the bounded neural work budget and show an overload warning, or produce little movement. Amplitudes are normalized engineering controls, not decibels, degrees, biological firing rates or guaranteed behavior commands. Native-game behavior still needs the [manual checklist](manual-game-test.md).
