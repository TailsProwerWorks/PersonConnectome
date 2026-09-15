# Fly adapter follow-up

The modular boundary now reserves `src/Adapters/PeoplePlaygroundFlyAdapter.cs`, but it is deliberately disabled. A real fly-body implementation is a separate follow-up after the boundaries are stable.

Before enabling it, that follow-up must:

1. Verify the installed People Playground fly body types, lifecycle, joints, sensors, and safe reflection surface against the exact game build.
2. Implement `IBodySensor` and `IBodyActuator` using native fly capabilities, reporting unsupported channels instead of fabricating values.
3. Define and document biological calibration for wings, legs, proboscis, antennae, optic flow, and escape behavior; do not reinterpret human joint mappings.
4. Add narrow adapter doubles and integration coverage for sensing, actuation, terminal cleanup, invalid data, and capability failures.
5. Add a native-game checklist with screenshots/logs for loading, physics, UI, performance, and lifecycle before enabling selection in composition.

The fly adapter must reuse `Core/LifBrain.cs`, `Core/ConnectomeAsset.cs`, `SensoryFrame`, and `MotorCommand`; it must not introduce a second simulator or change the FLYB asset format.
