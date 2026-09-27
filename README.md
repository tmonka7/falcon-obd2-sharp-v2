# Redline Diagnostics

Full-screen (1366 × 768) OBD-II vehicle diagnostics workstation for Windows, built on
**.NET Framework 4.7 / Windows Forms** with a self-contained software 3D renderer.
The UI is available in **English, Japanese and Chinese** and can be switched live.

![Full System Scan](docs/screenshots/scan.png)

## Features

| Area | What it does |
| --- | --- |
| **Full System Scan** | Walks every control module (21 modules in the bundled catalog), probes it, identifies the ECU, samples its live channels and reads stored / pending / permanent trouble codes (SAE J1979 and UDS 19 02). Progress is visualised in 3D: glowing module markers, an animated CAN harness, a scanning sweep plane and floating callouts. |
| **3D vehicle view** | Real car meshes (glTF/GLB or OBJ) rendered with GDI+: painter's algorithm, holographic fills, crease + silhouette outlines. 3D / 2D (top-down blueprint) / X-Ray modes, mouse orbit and zoom, auto-rotate, mini navigator. Dense models are simplified at load time with quadric edge collapse (213k → 7k triangles in about 3 s). A procedural sedan is the fallback when no model is available. |
| **Adapters** | ELM327 over USB or Bluetooth SPP (COM port), ELM327 WiFi (TCP), OBDLink MX+/SX (STN), and a built-in **Simulator** with a virtual hybrid sedan so the whole application can be exercised without hardware. |
| **Protocols** | Auto-detect or force: SAE J1850 PWM/VPW, ISO 9141-2, KWP2000 (slow / fast), CAN 11/29-bit at 250/500 kbps. ISO-TP multi-frame reassembly, per-module physical addressing (`AT SH` / `AT CRA` / flow control). |
| **Live Data** | Real-time ECM parameters (RPM, speed, coolant, load, throttle, IAT, MAF, fuel trims, voltage, timing, fuel level) with sparklines; per-module channels (wheel speeds, tyre pressures, HV battery, EPS…) while a module is selected. |
| **Database** | Embedded CSV tables: 320+ DTC definitions in EN/JA/ZH with family fallbacks for unknown codes, 75 mode-01 PIDs, 21 control modules with CAN addresses and 3D positions, VIN WMI manufacturer table. Drop extra CSVs into a `Data` folder next to the executable to extend it. |
| **Vehicle** | VIN decoding, MIL status, DTC count, I/M readiness monitors, supported-PID discovery. |
| **Reports / History / Garage** | HTML reports in the current language with in-app preview, JSON scan history, saved vehicle profiles. |

## Building

Requirements: Visual Studio 2022 **or** the .NET SDK (the project is SDK-style and targets `net47`).

```powershell
dotnet build RedlineDiagnostics.sln -c Release
# output: src\RedlineDiagnostics\bin\Release\net47\RedlineDiagnostics.exe
```

The executable has no runtime dependencies beyond .NET Framework 4.7.

## Running

* The window is borderless 1366 × 768 (true full screen on a 1366 × 768 display, centred otherwise).
  `F11` toggles the full-screen flag, `Esc` returns Home / exits, `F1`–`F8` jump to pages.
* On first start the **Simulator** adapter is selected and connects automatically; press **Full System Scan**.
* Real hardware: open **Settings**, pick the adapter type, COM port / baud (Bluetooth SPP adapters
  appear as COM ports) or WiFi host, optionally force a protocol, then **Test Connection**.
* Language: click **EN / 日本語 / 中文** in the top bar.

### Automated UI check

```powershell
RedlineDiagnostics.exe --autotest C:\temp\shots
```

Runs an unattended session off-screen (simulator scan, language switch, view modes, report, live data,
all pages) and writes a PNG of every step. Useful for visual regression checks.

## Project layout

```
src/RedlineDiagnostics
├─ App/            Theme, vector icons, settings, AppState (connection, scan, vehicles, history)
├─ Localization/   Loc (runtime language switch) and the EN/JA/ZH string table
├─ Rendering3D/    Vec3/Mat4 math, Mesh, procedural CarMeshFactory, ObjLoader, GDI+ Renderer
├─ Obd/            Transports (Serial/TCP/Simulator), Elm327Adapter, ISO-TP response parser,
│                  PID formulas, DTC parsers, ELM327 + virtual vehicle simulator
├─ Database/       Embedded CSV loaders (DTC, PID, module catalog, VIN WMI) + Data/*.csv
├─ Diagnostics/    ScanEngine, LiveDataMonitor, LiveParam channel definitions, history, HTML reports
├─ Controls/       Custom-painted widgets: TopBar, SideNav, Vehicle3DView, module cards,
│                  diagnostic area panel, buttons, lists, sparklines
└─ Forms/          MainForm shell and the eight pages
```

## 3D models

Models are read from the `Models` folder next to the executable (copied from `assets/models` at build time)
and can be switched in **Settings → 3D Vehicle Model**, or any `.glb` / `.gltf` / `.obj` file can be chosen
with **Custom file…**. Loading runs on a background thread; the procedural car is shown until the file is ready.

| File | Source | Licence |
| --- | --- | --- |
| `CarConcept.glb` (default) | [Khronos glTF Sample Assets – Car Concept](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/CarConcept), based on a CC0 model by Unity Fan; geometry-only copy (textures stripped) | CC-BY 4.0 (Khronos logos excluded) |
| `sedan.obj`, `sedan-sports.obj`, `suv.obj`, `hatchback-sports.obj` | [Kenney Car Kit](https://kenney.nl/assets/car-kit) | CC0 |

Import pipeline: node hierarchy and transforms → triangles → part classification from node / mesh / material
names (`glass`, `wheel`, `interior`, …) → normalisation to a 4.85 m wheelbase footprint resting on the ground →
quadric-error-metric simplification with boundary preservation → crease detection. Small trim parts (wipers,
gaskets, pedals, badges) are skipped because they only add noise at this scale.

## Extending

* **More modules**: add rows to `Database/Data/modules.csv` (request/response CAN id, 3D position, icon)
  and, if the module has manufacturer live data, its `LiveParam` channels in `Diagnostics/LiveParam.cs`.
* **More DTCs**: append to `dtc_codes.csv` (`code,en,ja,zh`) or ship a `Data\dtc_codes.csv` beside the exe.
* **Another car model**: drop a `.glb`, `.gltf` or `.obj` into `assets/models` (it becomes a bundled choice) or pick
  it as a custom file in Settings. Use **Flip front / back** if the model was authored facing the other way.
