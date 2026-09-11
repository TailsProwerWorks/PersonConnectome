# Person Connectome contributor instructions

Use C# only. Preserve observe-only defaults and never introduce network, shell, remote download, unmanaged runtime, Rust, WASM, or irreversible game actions. Keep adapters optional, null-safe, bounded, and capability-based; prefer zero/telemetry for unknown APIs.

Verified intended commands (requires a local .NET 8 SDK):

```sh
dotnet format PersonConnectome.sln --verify-no-changes
dotnet build PersonConnectome.sln -c Release
dotnet run --project tests/PersonConnectome.Tests -c Release
git diff --check
```

Do not claim API compatibility without testing against the target People Playground build. Never add third-party data without an explicit compatible license and notice.
