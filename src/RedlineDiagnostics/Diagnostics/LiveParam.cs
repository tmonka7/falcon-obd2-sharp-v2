using System;
using System.Collections.Generic;
using System.Globalization;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.Diagnostics
{
    /// <summary>
    /// One live-data channel. Mode 0x01 channels use the standard J1979 PID formulas; mode 0x22
    /// channels are manufacturer data identifiers decoded as unsigned 16-bit * Scale + Offset.
    /// The simulator uses the same definitions to encode its values, so both sides always agree.
    /// </summary>
    public sealed class LiveParam
    {
        public string Key;
        public string LabelKey;
        public int Mode;
        public int Id;
        public string Unit;
        public double Scale = 1;
        public double Offset;
        public int Decimals;
        public double Min;
        public double Max = 100;
        public bool Chart = true;

        public string Request => Mode == 0x01 ? "01" + Id.ToString("X2") : "22" + Id.ToString("X4");

        public byte ResponseService => (byte)(Mode + 0x40);
        public int HeaderBytes => Mode == 0x01 ? 1 : 2;

        public double? Decode(byte[] payload)
        {
            if (payload == null) return null;
            if (Mode == 0x01) return PidDecoder.Decode(Id, payload);
            if (payload.Length < 2) return payload.Length == 1 ? payload[0] * Scale + Offset : (double?)null;
            int raw = (payload[0] << 8) | payload[1];
            return raw * Scale + Offset;
        }

        /// <summary>Inverse of <see cref="Decode"/> for mode 0x22 (simulator use).</summary>
        public byte[] Encode(double value)
        {
            int raw = (int)Math.Round((value - Offset) / Scale);
            if (raw < 0) raw = 0;
            if (raw > 65535) raw = 65535;
            return new[] { (byte)(raw >> 8), (byte)(raw & 0xFF) };
        }

        public string Format(double value)
        {
            var s = value.ToString("F" + Decimals, CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(Unit) ? s : s + " " + Unit;
        }

        public string Label => Localization.Loc.T(LabelKey);

        private static LiveParam P1(string key, string label, int pid, string unit, int dec, double min, double max)
            => new LiveParam { Key = key, LabelKey = label, Mode = 0x01, Id = pid, Unit = unit, Decimals = dec, Min = min, Max = max };

        private static LiveParam P22(string key, string label, int did, string unit, double scale, double offset, int dec, double min, double max, bool chart = true)
            => new LiveParam { Key = key, LabelKey = label, Mode = 0x22, Id = did, Unit = unit, Scale = scale, Offset = offset, Decimals = dec, Min = min, Max = max, Chart = chart };

        public static readonly LiveParam[] Ecm =
        {
            P1("ecm.rpm", "pid.rpm", 0x0C, "rpm", 0, 0, 8000),
            P1("ecm.speed", "pid.speed", 0x0D, "km/h", 0, 0, 240),
            P1("ecm.coolant", "pid.coolant", 0x05, "°C", 0, -40, 130),
            P1("ecm.load", "pid.load", 0x04, "%", 1, 0, 100),
            P1("ecm.throttle", "pid.throttle", 0x11, "%", 1, 0, 100),
            P1("ecm.iat", "pid.iat", 0x0F, "°C", 0, -40, 80),
            P1("ecm.maf", "pid.maf", 0x10, "g/s", 2, 0, 200),
            P1("ecm.stft", "pid.stft", 0x06, "%", 1, -25, 25),
            P1("ecm.ltft", "pid.ltft", 0x07, "%", 1, -25, 25),
            P1("ecm.voltage", "pid.voltage", 0x42, "V", 2, 8, 16),
            P1("ecm.timing", "pid.timing", 0x0E, "°", 1, -30, 60),
            P1("ecm.fuel", "pid.fuelLevel", 0x2F, "%", 1, 0, 100),
        };

        private static readonly Dictionary<string, LiveParam[]> _byModule = new Dictionary<string, LiveParam[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "ECM", Ecm },
            { "TCM", new[] {
                P22("tcm.gear", "live.gear", 0x0001, "", 1, 0, 0, 0, 8, false),
                P22("tcm.atf", "live.atfTemp", 0x0002, "°C", 0.1, -40, 1, -40, 150),
                P22("tcm.inSpeed", "live.inputSpeed", 0x0003, "rpm", 1, 0, 0, 0, 8000),
                P22("tcm.outSpeed", "live.outputSpeed", 0x0004, "rpm", 1, 0, 0, 0, 8000),
                P22("tcm.linePressure", "live.linePressure", 0x0005, "kPa", 1, 0, 0, 0, 2000) } },
            { "ABS", new[] {
                P22("abs.wheelFL", "live.wheelFL", 0x0001, "km/h", 0.01, 0, 1, 0, 240),
                P22("abs.wheelFR", "live.wheelFR", 0x0002, "km/h", 0.01, 0, 1, 0, 240),
                P22("abs.wheelRL", "live.wheelRL", 0x0003, "km/h", 0.01, 0, 1, 0, 240),
                P22("abs.wheelRR", "live.wheelRR", 0x0004, "km/h", 0.01, 0, 1, 0, 240),
                P22("abs.voltage", "live.voltage", 0x0005, "V", 0.01, 0, 1, 8, 16),
                P22("abs.brake", "live.brakePressure", 0x0006, "bar", 0.01, 0, 2, 0, 200),
                P22("abs.yaw", "live.yawRate", 0x0007, "°/s", 0.01, -327.68, 2, -50, 50) } },
            { "SRS", new[] {
                P22("srs.voltage", "live.supplyVoltage", 0x0001, "V", 0.01, 0, 2, 8, 16),
                P22("srs.squibD", "live.squibDriver", 0x0002, "Ω", 0.01, 0, 2, 0, 10),
                P22("srs.squibP", "live.squibPassenger", 0x0003, "Ω", 0.01, 0, 2, 0, 10),
                P22("srs.status", "live.status", 0x0004, "", 1, 0, 0, 0, 1, false) } },
            { "BCM", new[] {
                P22("bcm.voltage", "live.supplyVoltage", 0x0001, "V", 0.01, 0, 2, 8, 16),
                P22("bcm.temp", "live.moduleTemp", 0x0002, "°C", 0.1, -40, 1, -40, 100),
                P22("bcm.msg", "live.msgCount", 0x0003, "", 1, 0, 0, 0, 65535, false),
                P22("bcm.doors", "live.doorStatus", 0x0004, "", 1, 0, 0, 0, 15, false) } },
            { "TPMS", new[] {
                P22("tpms.fl", "live.tirePressureFL", 0x0001, "bar", 0.01, 0, 2, 0, 4),
                P22("tpms.fr", "live.tirePressureFR", 0x0002, "bar", 0.01, 0, 2, 0, 4),
                P22("tpms.rl", "live.tirePressureRL", 0x0003, "bar", 0.01, 0, 2, 0, 4),
                P22("tpms.rr", "live.tirePressureRR", 0x0004, "bar", 0.01, 0, 2, 0, 4),
                P22("tpms.temp", "live.tireTemp", 0x0005, "°C", 0.1, -40, 1, -40, 100) } },
            { "HVAC", new[] {
                P22("hvac.cabin", "live.cabinTemp", 0x0001, "°C", 0.1, -40, 1, -20, 60),
                P22("hvac.set", "live.setTemp", 0x0002, "°C", 0.1, -40, 1, 16, 30),
                P22("hvac.blower", "live.blower", 0x0003, "", 1, 0, 0, 0, 7),
                P22("hvac.evap", "live.evapTemp", 0x0004, "°C", 0.1, -40, 1, -10, 40) } },
            { "EPS", new[] {
                P22("eps.angle", "live.steerAngle", 0x0001, "°", 0.1, -3276.8, 1, -540, 540),
                P22("eps.torque", "live.steerTorque", 0x0002, "Nm", 0.01, -327.68, 2, -10, 10),
                P22("eps.current", "live.assistCurrent", 0x0003, "A", 0.1, 0, 1, 0, 80),
                P22("eps.motorTemp", "live.motorTemp", 0x0004, "°C", 0.1, -40, 1, -40, 120) } },
            { "IPC", new[] {
                P22("ipc.odo", "live.odometer", 0x0001, "km", 10, 0, 0, 0, 655350, false),
                P22("ipc.fuel", "live.fuelGauge", 0x0002, "%", 0.1, 0, 1, 0, 100),
                P22("ipc.voltage", "live.supplyVoltage", 0x0003, "V", 0.01, 0, 2, 8, 16) } },
            { "GTW", new[] {
                P22("gtw.load", "live.busLoad", 0x0001, "%", 0.1, 0, 1, 0, 100),
                P22("gtw.msg", "live.msgCount", 0x0002, "", 1, 0, 0, 0, 65535, false),
                P22("gtw.voltage", "live.supplyVoltage", 0x0003, "V", 0.01, 0, 2, 8, 16) } },
            { "HV", new[] {
                P22("hv.soc", "live.hvBattSoc", 0x0001, "%", 0.1, 0, 1, 0, 100),
                P22("hv.volt", "live.hvBattVolt", 0x0002, "V", 0.1, 0, 1, 0, 400),
                P22("hv.motor", "live.motorRpm", 0x0003, "rpm", 1, 0, 0, 0, 12000),
                P22("hv.invTemp", "live.inverterTemp", 0x0004, "°C", 0.1, -40, 1, -40, 120) } },
            { "BMS", new[] {
                P22("bms.soc", "live.hvBattSoc", 0x0001, "%", 0.1, 0, 1, 0, 100),
                P22("bms.pack", "live.packVoltage", 0x0002, "V", 0.1, 0, 1, 0, 400),
                P22("bms.temp", "live.packTemp", 0x0003, "°C", 0.1, -40, 1, -40, 80),
                P22("bms.delta", "live.cellDelta", 0x0004, "mV", 1, 0, 0, 0, 500) } },
            { "PCS", new[] {
                P22("pcs.status", "live.radarStatus", 0x0001, "", 1, 0, 0, 0, 1, false),
                P22("pcs.dist", "live.targetDistance", 0x0002, "m", 0.1, 0, 1, 0, 200),
                P22("pcs.camTemp", "live.cameraTemp", 0x0003, "°C", 0.1, -40, 1, -40, 100) } },
            { "PKB", new[] {
                P22("pkb.current", "live.motorCurrent", 0x0001, "A", 0.1, 0, 1, 0, 30),
                P22("pkb.status", "live.status", 0x0002, "", 1, 0, 0, 0, 1, false),
                P22("pkb.voltage", "live.supplyVoltage", 0x0003, "V", 0.01, 0, 2, 8, 16) } },
            { "SMK", new[] {
                P22("smk.key", "live.keyStatus", 0x0001, "", 1, 0, 0, 0, 1, false),
                P22("smk.voltage", "live.supplyVoltage", 0x0002, "V", 0.01, 0, 2, 8, 16),
                P22("smk.lf", "live.lfSignal", 0x0003, "dBm", 0.1, -100, 1, -100, 0) } },
            { "MID", new[] {
                P22("mid.voltage", "live.supplyVoltage", 0x0001, "V", 0.01, 0, 2, 8, 16),
                P22("mid.temp", "live.moduleTemp", 0x0002, "°C", 0.1, -40, 1, -40, 100),
                P22("mid.status", "live.status", 0x0003, "", 1, 0, 0, 0, 1, false) } },
            { "TEL", new[] {
                P22("tel.signal", "live.signalStrength", 0x0001, "dBm", 0.1, -120, 1, -120, -40),
                P22("tel.status", "live.status", 0x0002, "", 1, 0, 0, 0, 1, false),
                P22("tel.voltage", "live.supplyVoltage", 0x0003, "V", 0.01, 0, 2, 8, 16) } },
            { "SCU", new[] {
                P22("scu.pos", "live.seatPosition", 0x0001, "mm", 0.1, 0, 1, 0, 300),
                P22("scu.current", "live.motorCurrent", 0x0002, "A", 0.1, 0, 1, 0, 30),
                P22("scu.voltage", "live.supplyVoltage", 0x0003, "V", 0.01, 0, 2, 8, 16) } },
            { "LCM", new[] {
                P22("lcm.voltage", "live.supplyVoltage", 0x0001, "V", 0.01, 0, 2, 8, 16),
                P22("lcm.current", "live.headlampCurrent", 0x0002, "A", 0.1, 0, 1, 0, 20),
                P22("lcm.status", "live.status", 0x0003, "", 1, 0, 0, 0, 1, false) } },
            { "PDM", new[] {
                P22("pdm.window", "live.windowPosition", 0x0001, "%", 0.1, 0, 1, 0, 100),
                P22("pdm.lock", "live.lockStatus", 0x0002, "", 1, 0, 0, 0, 1, false),
                P22("pdm.voltage", "live.supplyVoltage", 0x0003, "V", 0.01, 0, 2, 8, 16) } },
            { "AFS", new[] {
                P22("afs.swivel", "live.swivelAngle", 0x0001, "°", 0.1, -327.68, 1, -20, 20),
                P22("afs.level", "live.levelingAngle", 0x0002, "°", 0.1, -327.68, 1, -5, 5),
                P22("afs.voltage", "live.supplyVoltage", 0x0003, "V", 0.01, 0, 2, 8, 16) } },
        };

        public static LiveParam[] For(string moduleShort)
        {
            LiveParam[] p;
            return moduleShort != null && _byModule.TryGetValue(moduleShort, out p) ? p : new LiveParam[0];
        }

        public static LiveParam Find(string key)
        {
            foreach (var kv in _byModule)
                foreach (var p in kv.Value)
                    if (p.Key == key) return p;
            return null;
        }
    }
}
