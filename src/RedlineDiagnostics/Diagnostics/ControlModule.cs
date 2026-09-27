using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Obd;
using RedlineDiagnostics.Rendering3D;

namespace RedlineDiagnostics.Diagnostics
{
    public enum ModuleStatus { Pending = 0, Scanning = 1, Passed = 2, Warning = 3, Fault = 4, NoResponse = 5 }

    /// <summary>Runtime state of one control module during/after a scan.</summary>
    public sealed class ControlModule
    {
        public ModuleDefinition Definition;
        public ModuleStatus Status = ModuleStatus.Pending;
        public int Progress;
        public int ResponseTimeMs = -1;
        public string EcuId = "";
        public string ProtocolName = "";
        public readonly List<DtcRecord> Dtcs = new List<DtcRecord>();
        public readonly Dictionary<string, double> LiveValues = new Dictionary<string, double>();
        public DateTime? StartedAt;
        public TimeSpan Elapsed;
        public string Error = "";

        public ControlModule(ModuleDefinition def) { Definition = def; }

        public string Short => Definition.Short;
        public string Name => Definition.Name;
        public string Icon => Definition.Icon;
        public Vec3 Position => Definition.Position;
        public string Address => "0x" + Definition.RequestId.ToString("X3");
        public bool IsDone => Status >= ModuleStatus.Passed;
        public int FaultCount => Dtcs.Count(d => d.State != DtcState.Pending);
        public int PendingCount => Dtcs.Count(d => d.State == DtcState.Pending);

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case ModuleStatus.Scanning: return Loc.T("status.scanning");
                    case ModuleStatus.Passed: return Loc.T("status.completed");
                    case ModuleStatus.Warning: return Loc.T("status.warning");
                    case ModuleStatus.Fault: return Loc.T("status.fault");
                    case ModuleStatus.NoResponse: return Loc.T("status.noresponse");
                    default: return Loc.T("status.pending");
                }
            }
        }

        public void Reset()
        {
            Status = ModuleStatus.Pending;
            Progress = 0;
            ResponseTimeMs = -1;
            EcuId = "";
            ProtocolName = "";
            Dtcs.Clear();
            LiveValues.Clear();
            StartedAt = null;
            Elapsed = TimeSpan.Zero;
            Error = "";
        }
    }

    [DataContract]
    public sealed class DtcEntry
    {
        [DataMember] public string Code;
        [DataMember] public int State;
        [DataMember] public string Description;
        public DtcState DtcState => (DtcState)State;
    }

    [DataContract]
    public sealed class ModuleResult
    {
        [DataMember] public string Short;
        [DataMember] public string Name;
        [DataMember] public int Status;
        [DataMember] public int ResponseMs;
        [DataMember] public string EcuId;
        [DataMember] public string Address;
        [DataMember] public List<DtcEntry> Dtcs = new List<DtcEntry>();
        public ModuleStatus ModuleStatus => (ModuleStatus)Status;
    }

    /// <summary>Persisted outcome of a full system scan.</summary>
    [DataContract]
    public sealed class ScanResult
    {
        [DataMember] public DateTime Started;
        [DataMember] public DateTime Ended;
        [DataMember] public string Vin = "";
        [DataMember] public string VehicleName = "";
        [DataMember] public string Adapter = "";
        [DataMember] public string Protocol = "";
        [DataMember] public bool Cancelled;
        [DataMember] public int Scanned;
        [DataMember] public int Faults;
        [DataMember] public int Warnings;
        [DataMember] public int NoResponse;
        [DataMember] public string ReportPath = "";
        [DataMember] public List<ModuleResult> Modules = new List<ModuleResult>();

        public TimeSpan Duration => Ended - Started;
        public int TotalDtcs => Modules.Sum(m => m.Dtcs.Count);
    }

    [Serializable]
    public class VehicleProfile
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Vin = "";
        public string Make = "";
        public string Model = "";
        public int Year;
        public string Engine = "";
        public string Plate = "";
        public string Notes = "";
        public DateTime Created = DateTime.Now;

        public string DisplayName
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Make)) parts.Add(Make.Trim());
                if (!string.IsNullOrWhiteSpace(Model)) parts.Add(Model.Trim());
                if (Year > 0) parts.Add(Year.ToString());
                if (parts.Count == 0) parts.Add(string.IsNullOrWhiteSpace(Vin) ? "—" : Vin);
                return string.Join(" ", parts);
            }
        }
    }
}
