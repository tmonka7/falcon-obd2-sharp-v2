using System.Collections.Generic;

namespace RedlineDiagnostics.Localization
{
    /// <summary>All UI strings: key -> { English, Japanese, Chinese }.</summary>
    internal static class Strings
    {
        public static Dictionary<string, string[]> Build()
        {
            var d = new Dictionary<string, string[]>();
            void A(string key, string en, string ja, string zh) => d[key] = new[] { en, ja, zh };

            // Brand / top bar
            A("app.title", "Redline Diagnostics", "Redline Diagnostics", "Redline Diagnostics");
            A("app.brand", "REDLINE", "REDLINE", "REDLINE");
            A("app.brandSub", "DIAGNOSTICS", "DIAGNOSTICS", "DIAGNOSTICS");
            A("status.connected", "Connected", "接続済み", "已连接");
            A("status.disconnected", "Disconnected", "未接続", "未连接");
            A("status.connecting", "Connecting...", "接続中...", "连接中...");
            A("status.error", "Connection error", "接続エラー", "连接错误");
            A("status.noAdapter", "No adapter", "アダプターなし", "无适配器");

            // Navigation
            A("nav.home", "Home", "ホーム", "首页");
            A("nav.diagnose", "Diagnose", "診断", "诊断");
            A("nav.livedata", "Live Data", "ライブデータ", "实时数据");
            A("nav.vehicle", "Vehicle", "車両", "车辆");
            A("nav.reports", "Reports", "レポート", "报告");
            A("nav.history", "History", "履歴", "历史");
            A("nav.garage", "Garage", "ガレージ", "车库");
            A("nav.settings", "Settings", "設定", "设置");

            // Scan screen
            A("scan.fullSystem", "Full System Scan", "フルシステムスキャン", "全系统扫描");
            A("scan.modules", "Modules", "モジュール", "模块");
            A("scan.elapsed", "Elapsed Time", "経過時間", "已用时间");
            A("scan.start", "Start Scan", "スキャン開始", "开始扫描");
            A("scan.stop", "Stop Scan", "スキャン停止", "停止扫描");
            A("scan.rescan", "Scan Again", "再スキャン", "重新扫描");
            A("scan.complete", "Scan Complete", "スキャン完了", "扫描完成");
            A("scan.cancelled", "Scan Cancelled", "スキャン中止", "扫描已取消");
            A("scan.ready", "Ready to scan", "スキャン準備完了", "准备扫描");
            A("scan.notConnected", "Connect an OBD2 adapter to start a scan.", "スキャンを開始するには OBD2 アダプターを接続してください。", "请先连接 OBD2 适配器再开始扫描。");
            A("scan.summary", "{0} modules scanned · {1} faults · {2} warnings", "{0} モジュール完了 · 故障 {1} · 警告 {2}", "已扫描 {0} 个模块 · {1} 个故障 · {2} 个警告");
            A("scan.controlModules", "Control Modules", "コントロールモジュール", "控制模块");
            A("scan.viewReport", "View Report", "レポート表示", "查看报告");

            // Module statuses
            A("status.pending", "Pending", "待機中", "待处理");
            A("status.scanning", "Scanning...", "スキャン中...", "扫描中...");
            A("status.completed", "Completed", "完了", "已完成");
            A("status.passed", "Passed", "正常", "正常");
            A("status.warning", "Warning", "警告", "警告");
            A("status.fault", "Fault", "故障", "故障");
            A("status.noresponse", "No Response", "応答なし", "无响应");

            // Legend
            A("legend.scanning", "Scanning", "スキャン中", "扫描中");
            A("legend.passed", "Passed", "正常", "通过");
            A("legend.warning", "Warning", "警告", "警告");
            A("legend.fault", "Fault", "故障", "故障");
            A("legend.pending", "Pending", "待機", "待处理");

            // 3D view
            A("view.3d", "3D", "3D", "3D");
            A("view.2d", "2D", "2D", "2D");
            A("view.xray", "X-Ray", "X線", "X光");
            A("view.autoRotate", "Auto rotate", "自動回転", "自动旋转");

            // Right panel
            A("panel.currentArea", "Current Diagnostic Area", "現在の診断エリア", "当前诊断区域");
            A("panel.protocol", "Protocol", "プロトコル", "协议");
            A("panel.moduleAddress", "Module Address", "モジュールアドレス", "模块地址");
            A("panel.ecuId", "ECU ID", "ECU ID", "ECU ID");
            A("panel.timeElapsed", "Time Elapsed", "経過時間", "已用时间");
            A("panel.liveData", "Live Data", "ライブデータ", "实时数据");
            A("panel.communication", "Communication", "通信", "通信");
            A("panel.stable", "Stable", "安定", "稳定");
            A("panel.unstable", "Unstable", "不安定", "不稳定");
            A("panel.idle", "Idle", "待機", "空闲");
            A("panel.dtcs", "Trouble Codes", "故障コード", "故障码");
            A("panel.noDtcs", "No trouble codes stored", "故障コードはありません", "未存储故障码");
            A("panel.noModule", "Select a module or start a scan", "モジュールを選択するかスキャンを開始してください", "请选择模块或开始扫描");
            A("panel.responseTime", "Response", "応答時間", "响应时间");

            // DTC status
            A("dtc.stored", "Stored", "確定", "已存储");
            A("dtc.pending", "Pending", "保留", "待定");
            A("dtc.permanent", "Permanent", "永久", "永久");
            A("dtc.system.P", "Powertrain", "パワートレイン", "动力系统");
            A("dtc.system.B", "Body", "ボディ", "车身");
            A("dtc.system.C", "Chassis", "シャシー", "底盘");
            A("dtc.system.U", "Network", "ネットワーク", "网络");
            A("dtc.unknown", "Manufacturer specific code — consult service documentation", "メーカー固有コード — 整備資料を参照してください", "制造商专用代码 — 请参阅维修资料");

            // Home
            A("home.welcome", "Welcome back", "おかえりなさい", "欢迎回来");
            A("home.subtitle", "Vehicle diagnostics workstation", "車両診断ワークステーション", "车辆诊断工作站");
            A("home.quickActions", "Quick Actions", "クイック操作", "快捷操作");
            A("home.connect", "Connect Adapter", "アダプター接続", "连接适配器");
            A("home.disconnect", "Disconnect", "切断", "断开连接");
            A("home.fullScan", "Full System Scan", "フルシステムスキャン", "全系统扫描");
            A("home.readDtcs", "Read Trouble Codes", "故障コード読取", "读取故障码");
            A("home.clearDtcs", "Clear Trouble Codes", "故障コード消去", "清除故障码");
            A("home.liveData", "Live Data Monitor", "ライブデータ監視", "实时数据监控");
            A("home.recentScans", "Recent Scans", "最近のスキャン", "最近扫描");
            A("home.activeVehicle", "Active Vehicle", "選択中の車両", "当前车辆");
            A("home.adapter", "Adapter", "アダプター", "适配器");
            A("home.systemStatus", "System Status", "システム状態", "系统状态");
            A("home.noVehicle", "No vehicle selected", "車両が選択されていません", "未选择车辆");
            A("home.lastScan", "Last scan", "前回のスキャン", "上次扫描");
            A("home.never", "Never", "なし", "无");
            A("home.dtcsFound", "{0} trouble codes found", "故障コード {0} 件", "发现 {0} 个故障码");
            A("home.clearConfirm", "Clear all stored trouble codes in the engine control module?", "エンジン制御モジュールの故障コードをすべて消去しますか？", "确定要清除发动机控制模块中的所有故障码吗？");
            A("home.cleared", "Trouble codes cleared.", "故障コードを消去しました。", "故障码已清除。");
            A("home.database", "Database", "データベース", "数据库");
            A("home.dbCodes", "{0} DTC definitions · {1} PIDs · {2} modules", "DTC 定義 {0} 件 · PID {1} 件 · モジュール {2} 件", "{0} 条故障码定义 · {1} 个 PID · {2} 个模块");
            A("splash.tagline", "PROFESSIONAL OBD2 VEHICLE DIAGNOSTIC TOOL", "プロフェッショナル OBD2 車両診断ツール", "专业 OBD2 汽车诊断工具");
            A("splash.words", "SCAN   /   ANALYZE   /   REPAIR   /   DRIVE BETTER", "スキャン   /   解析   /   修理   /   快適なドライブ", "扫描   /   分析   /   维修   /   安心驾驶");
            A("splash.f1", "FULL SYSTEM\nSCAN", "フルシステム\nスキャン", "全系统\n扫描");
            A("splash.f2", "LIVE DATA", "ライブデータ", "实时数据");
            A("splash.f3", "SERVICE\nFUNCTIONS", "サービス\n機能", "服务\n功能");
            A("splash.f4", "DIAGNOSTIC\nREPORTS", "診断\nレポート", "诊断\n报告");
            A("splash.init", "Initializing application...", "アプリケーションを初期化しています...", "正在初始化应用程序...");
            A("splash.db", "Loading diagnostic database...", "診断データベースを読み込んでいます...", "正在加载诊断数据库...");
            A("splash.dbDone", "Database: {0} DTC definitions, {1} PIDs, {2} modules", "データベース: DTC {0} 件、PID {1} 件、モジュール {2} 件", "数据库：{0} 条故障码、{1} 个 PID、{2} 个模块");
            A("splash.model", "Loading 3D vehicle model...", "3D 車両モデルを読み込んでいます...", "正在加载 3D 车辆模型...");
            A("splash.iface", "Checking OBD2 interface...", "OBD2 インターフェースを確認しています...", "正在检查 OBD2 接口...");
            A("splash.ifaceSim", "Simulator adapter ready", "シミュレーター準備完了", "模拟器已就绪");
            A("splash.ifacePort", "{0} found on {1}", "{0}: {1} を検出", "在 {1} 上找到 {0}");
            A("splash.ifaceMissing", "{0}: port {1} not found (check Settings)", "{0}: ポート {1} が見つかりません (設定を確認)", "{0}：未找到端口 {1}（请检查设置）");
            A("splash.ifaceWifi", "{0} at {1}", "{0}: {1}", "{0}：{1}");
            A("splash.ready", "Ready", "準備完了", "准备就绪");
            A("svc.mil", "Reset MIL / clear emission data (Mode 04)", "MIL リセット / 排出ガスデータ消去 (モード 04)", "复位故障灯 / 清除排放数据（模式 04）");
            A("svc.readiness", "Readiness monitors", "レディネスモニター", "就绪状态监测");
            A("svc.battery", "Battery voltage", "バッテリー電圧", "蓄电池电压");
            A("svc.batteryValue", "Battery voltage: {0}", "バッテリー電圧: {0}", "蓄电池电压：{0}");
            A("svc.resetAdapter", "Re-initialise adapter", "アダプター再初期化", "重新初始化适配器");
            A("app.tagline", "Smarter Diagnostics  •  Healthier Journeys", "より賢い診断  •  より安心なドライブ", "更智能的诊断  •  更安心的旅程");
            A("home.ready", "Your vehicle is ready for diagnostics", "車両は診断の準備ができています", "您的车辆已准备好进行诊断");
            A("home.notReady", "Connect an adapter to start diagnostics", "診断を開始するにはアダプターを接続してください", "连接适配器以开始诊断");
            A("home.changeVehicle", "Change Vehicle", "車両を変更", "更换车辆");
            A("home.connAdapter", "Connection & Adapter", "接続とアダプター", "连接与适配器");
            A("home.deviceDetails", "Device Details", "デバイス詳細", "设备详情");
            A("home.more", "More", "その他", "更多");
            A("home.healthScore", "Health Score", "健康スコア", "健康评分");
            A("home.viewDetails", "View Details", "詳細を見る", "查看详情");
            A("home.viewAll", "View All", "すべて表示", "查看全部");
            A("home.popular", "Popular DTCs", "よくある DTC", "常见故障码");
            A("home.results", "Search Results", "検索結果", "搜索结果");
            A("home.noResults", "No matching codes", "該当するコードがありません", "没有匹配的故障码");
            A("home.serviceFunctions", "Service Functions", "サービス機能", "服务功能");
            A("home.scanAll", "Scan all modules", "全モジュールをスキャン", "扫描所有模块");
            A("home.viewDtcs", "View DTCs", "DTC を表示", "查看故障码");
            A("home.eraseDtcs", "Erase DTCs", "DTC を消去", "清除故障码");
            A("home.realtime", "Real-time data", "リアルタイムデータ", "实时数据");
            A("home.resetAdapt", "Reset & Adaptation", "リセットと学習", "复位与匹配");
            A("home.issues", "Issues", "要確認", "有问题");
            A("home.ok", "OK", "OK", "正常");
            A("home.healthy", "Healthy", "正常", "健康");
            A("home.issue1", "1 Issue", "問題 1 件", "1 个问题");
            A("home.issueN", "{0} Issues", "問題 {0} 件", "{0} 个问题");
            A("home.promo1", "Professional Diagnostics", "あらゆる車両に", "专业诊断");
            A("home.promo2", "For Every Vehicle", "プロの診断を", "适用于每一辆车");
            A("home.accurate", "Accurate", "正確", "精准");
            A("home.fast", "Fast", "高速", "快速");
            A("home.reliable", "Reliable", "高信頼", "可靠");
            A("home.dtcPlaceholder", "Enter DTC code (e.g. P0301)", "DTC コードを入力 (例: P0301)", "输入故障码（例如 P0301）");
            A("home.faultsModules", "{0} Faults  •  {1} Modules", "故障 {0} 件  •  {1} モジュール", "{0} 个故障  •  {1} 个模块");
            A("home.noScan", "No scan yet", "スキャン未実施", "尚未扫描");
            A("home.sideSlogan", "Drive Better\nwith Confidence", "安心して\nもっと快適に", "自信驾驶\n一路安心");
            A("home.m.ECM", "Engine", "エンジン", "发动机");
            A("home.m.TCM", "Transmission", "トランスミッション", "变速箱");
            A("home.m.ABS", "ABS", "ABS", "ABS");
            A("home.m.SRS", "SRS", "SRS", "SRS");
            A("home.m.BCM", "Body", "ボディ", "车身");
            A("home.m.TPMS", "TPMS", "TPMS", "TPMS");
            A("home.link.usb", "USB", "USB", "USB");
            A("home.link.bt", "Bluetooth", "Bluetooth", "蓝牙");
            A("home.link.wifi", "WiFi", "WiFi", "WiFi");
            A("home.link.sim", "Virtual vehicle", "仮想車両", "虚拟车辆");

            // Live data
            A("live.title", "Live Data", "ライブデータ", "实时数据");
            A("live.subtitle", "Real-time engine control module parameters", "エンジン制御モジュールのリアルタイムパラメータ", "发动机控制模块实时参数");
            A("live.startStop", "Start / Stop", "開始 / 停止", "开始 / 停止");
            A("live.start", "Start Monitoring", "監視開始", "开始监控");
            A("live.stop", "Stop Monitoring", "監視停止", "停止监控");
            A("live.rate", "Sample rate", "サンプルレート", "采样率");
            A("live.notConnected", "Connect an adapter to view live data.", "ライブデータを表示するにはアダプターを接続してください。", "请连接适配器以查看实时数据。");

            // Vehicle page
            A("vehicle.title", "Vehicle Information", "車両情報", "车辆信息");
            A("vehicle.vin", "VIN", "車台番号 (VIN)", "车辆识别码 (VIN)");
            A("vehicle.make", "Make", "メーカー", "品牌");
            A("vehicle.model", "Model", "モデル", "车型");
            A("vehicle.year", "Year", "年式", "年份");
            A("vehicle.engine", "Engine", "エンジン", "发动机");
            A("vehicle.country", "Country", "製造国", "生产国");
            A("vehicle.plate", "License Plate", "ナンバー", "车牌号");
            A("vehicle.odometer", "Odometer", "走行距離", "里程");
            A("vehicle.mil", "MIL Status", "警告灯 (MIL)", "故障指示灯");
            A("vehicle.milOn", "ON", "点灯", "亮");
            A("vehicle.milOff", "OFF", "消灯", "灭");
            A("vehicle.dtcCount", "Stored DTC count", "確定 DTC 数", "已存储故障码数");
            A("vehicle.readiness", "I/M Readiness Monitors", "I/M レディネスモニター", "I/M 就绪状态监测");
            A("vehicle.readFromEcu", "Read from ECU", "ECU から読み取り", "从 ECU 读取");
            A("vehicle.supportedPids", "Supported PIDs", "対応 PID", "支持的 PID");
            A("vehicle.protocol", "Protocol", "プロトコル", "协议");
            A("vehicle.battery", "Battery Voltage", "バッテリー電圧", "电池电压");
            A("vehicle.ready", "Ready", "完了", "就绪");
            A("vehicle.notReady", "Not Ready", "未完了", "未就绪");
            A("vehicle.na", "N/A", "対象外", "不适用");
            A("vehicle.save", "Save Vehicle", "車両を保存", "保存车辆");
            A("mon.misfire", "Misfire", "失火", "失火");
            A("mon.fuel", "Fuel System", "燃料系統", "燃油系统");
            A("mon.comp", "Components", "総合部品", "综合部件");
            A("mon.catalyst", "Catalyst", "触媒", "催化器");
            A("mon.heatedCat", "Heated Catalyst", "加熱触媒", "加热催化器");
            A("mon.evap", "Evaporative System", "蒸発ガス", "蒸发系统");
            A("mon.secAir", "Secondary Air", "二次空気", "二次空气");
            A("mon.o2", "O2 Sensor", "O2 センサー", "氧传感器");
            A("mon.o2Heater", "O2 Sensor Heater", "O2 ヒーター", "氧传感器加热器");
            A("mon.egr", "EGR / VVT", "EGR / VVT", "EGR / VVT");

            // Reports
            A("report.title", "Diagnostic Reports", "診断レポート", "诊断报告");
            A("report.generate", "Generate Report", "レポート生成", "生成报告");
            A("report.open", "Open", "開く", "打开");
            A("report.openFolder", "Open Folder", "フォルダーを開く", "打开文件夹");
            A("report.none", "No reports yet. Run a full system scan and generate a report.", "レポートはまだありません。フルシステムスキャンを実行してレポートを生成してください。", "暂无报告。请运行全系统扫描并生成报告。");
            A("report.generated", "Report saved: {0}", "レポートを保存しました: {0}", "报告已保存：{0}");
            A("report.noScan", "No completed scan to report.", "レポートできるスキャンがありません。", "没有可生成报告的扫描。");
            A("report.heading", "Vehicle Diagnostic Report", "車両診断レポート", "车辆诊断报告");
            A("report.summary", "Summary", "概要", "摘要");
            A("report.moduleResults", "Module Results", "モジュール結果", "模块结果");
            A("report.generatedAt", "Generated", "作成日時", "生成时间");
            A("report.tool", "Tool", "ツール", "工具");
            A("report.delete", "Delete", "削除", "删除");

            // History
            A("history.title", "Scan History", "スキャン履歴", "扫描历史");
            A("history.none", "No scan history yet.", "スキャン履歴はありません。", "暂无扫描历史。");
            A("history.details", "Scan Details", "スキャン詳細", "扫描详情");
            A("history.clear", "Clear History", "履歴を消去", "清除历史");
            A("history.clearConfirm", "Delete all scan history?", "すべてのスキャン履歴を削除しますか？", "确定删除所有扫描历史？");
            A("history.duration", "Duration", "所要時間", "耗时");

            // Garage
            A("garage.title", "Garage", "ガレージ", "车库");
            A("garage.subtitle", "Saved vehicles", "保存済み車両", "已保存车辆");
            A("garage.add", "Add Vehicle", "車両追加", "添加车辆");
            A("garage.remove", "Remove", "削除", "删除");
            A("garage.select", "Set Active", "選択", "设为当前");
            A("garage.active", "Active", "選択中", "当前");
            A("garage.none", "No vehicles saved. Add one or connect to read the VIN.", "車両が保存されていません。追加するか接続して VIN を読み取ってください。", "尚未保存车辆。请添加或连接以读取 VIN。");
            A("garage.removeConfirm", "Remove {0} from the garage?", "{0} をガレージから削除しますか？", "确定从车库移除 {0}？");
            A("garage.editTitle", "Vehicle", "車両", "车辆");

            // Settings
            A("settings.title", "Settings", "設定", "设置");
            A("settings.language", "Language", "言語", "语言");
            A("settings.connection", "Connection", "接続", "连接");
            A("settings.adapterType", "Adapter Type", "アダプター種別", "适配器类型");
            A("settings.port", "Serial Port", "シリアルポート", "串口");
            A("settings.baud", "Baud Rate", "ボーレート", "波特率");
            A("settings.host", "WiFi Host", "WiFi ホスト", "WiFi 主机");
            A("settings.tcpPort", "TCP Port", "TCP ポート", "TCP 端口");
            A("settings.protocol", "OBD Protocol", "OBD プロトコル", "OBD 协议");
            A("settings.protocolAuto", "Automatic", "自動", "自动");
            A("settings.units", "Units", "単位", "单位");
            A("settings.metric", "Metric", "メートル法", "公制");
            A("settings.imperial", "Imperial", "ヤード・ポンド法", "英制");
            A("settings.display", "Display", "表示", "显示");
            A("settings.model", "3D Vehicle Model", "3D 車両モデル", "3D 车辆模型");
            A("settings.modelBuiltIn", "Built-in procedural sedan", "内蔵プロシージャルセダン", "内置程序生成轿车");
            A("settings.modelBundled", "Bundled model", "同梱モデル", "内置模型");
            A("settings.modelCustom", "Custom file...", "カスタムファイル...", "自定义文件...");
            A("settings.modelFlip", "Flip front / back", "前後を反転", "前后翻转");
            A("settings.modelInfo", "Loaded", "読み込み済み", "已加载");
            A("settings.browse", "Browse...", "参照...", "浏览...");
            A("settings.autoRotate", "Auto-rotate 3D model", "3D モデルを自動回転", "自动旋转 3D 模型");
            A("settings.showHarness", "Show data-bus animation", "データバスのアニメーションを表示", "显示数据总线动画");
            A("settings.fullscreen", "Full screen (1366×768)", "フルスクリーン (1366×768)", "全屏 (1366×768)");
            A("settings.save", "Save Settings", "設定を保存", "保存设置");
            A("settings.saved", "Settings saved.", "設定を保存しました。", "设置已保存。");
            A("settings.refreshPorts", "Refresh", "更新", "刷新");
            A("settings.testConnection", "Test Connection", "接続テスト", "测试连接");
            A("settings.simulatorHint", "Simulator: built-in virtual vehicle, no hardware required.", "シミュレーター: 内蔵仮想車両 (ハードウェア不要)", "模拟器：内置虚拟车辆，无需硬件。");
            A("settings.about", "About", "情報", "关于");
            A("settings.aboutText", "Redline Diagnostics v{0} · .NET Framework 4.7 · Software 3D renderer", "Redline Diagnostics v{0} · .NET Framework 4.7 · ソフトウェア 3D レンダラー", "Redline Diagnostics v{0} · .NET Framework 4.7 · 软件 3D 渲染器");
            A("settings.exit", "Exit Application", "アプリケーション終了", "退出应用");

            // Adapter types
            A("adapter.simulator", "Simulator (Demo)", "シミュレーター (デモ)", "模拟器（演示）");
            A("adapter.elm327usb", "ELM327 USB", "ELM327 USB", "ELM327 USB");
            A("adapter.elm327bt", "ELM327 Bluetooth", "ELM327 Bluetooth", "ELM327 蓝牙");
            A("adapter.elm327wifi", "ELM327 WiFi", "ELM327 WiFi", "ELM327 WiFi");
            A("adapter.obdlink", "OBDLink MX+ / SX", "OBDLink MX+ / SX", "OBDLink MX+ / SX");

            // Common
            A("common.ok", "OK", "OK", "确定");
            A("common.cancel", "Cancel", "キャンセル", "取消");
            A("common.yes", "Yes", "はい", "是");
            A("common.no", "No", "いいえ", "否");
            A("common.name", "Name", "名前", "名称");
            A("common.value", "Value", "値", "数值");
            A("common.unit", "Unit", "単位", "单位");
            A("common.code", "Code", "コード", "代码");
            A("common.description", "Description", "説明", "描述");
            A("common.status", "Status", "状態", "状态");
            A("common.date", "Date", "日付", "日期");
            A("common.vehicle", "Vehicle", "車両", "车辆");
            A("common.faults", "Faults", "故障", "故障");
            A("common.warnings", "Warnings", "警告", "警告");
            A("common.modules", "Modules", "モジュール", "模块");
            A("common.error", "Error", "エラー", "错误");
            A("common.info", "Information", "情報", "信息");
            A("common.confirm", "Confirm", "確認", "确认");
            A("common.none", "None", "なし", "无");
            A("common.close", "Close", "閉じる", "关闭");
            A("common.search", "Search", "検索", "搜索");
            A("common.dtcLookup", "DTC Lookup", "DTC 検索", "故障码查询");
            A("common.dtcLookupHint", "Enter a code such as P0420", "P0420 などのコードを入力", "输入代码，例如 P0420");

            // Connection messages
            A("conn.failed", "Could not connect to the adapter: {0}", "アダプターに接続できませんでした: {0}", "无法连接到适配器：{0}");
            A("conn.success", "Connected. Protocol: {0}", "接続しました。プロトコル: {0}", "已连接。协议：{0}");
            A("conn.noPort", "No serial port selected. Check Settings.", "シリアルポートが選択されていません。設定を確認してください。", "未选择串口，请检查设置。");

            // Live data labels (module specific)
            A("live.wheelFL", "Wheel Speed (FL)", "車輪速 (左前)", "轮速 (左前)");
            A("live.wheelFR", "Wheel Speed (FR)", "車輪速 (右前)", "轮速 (右前)");
            A("live.wheelRL", "Wheel Speed (RL)", "車輪速 (左後)", "轮速 (左后)");
            A("live.wheelRR", "Wheel Speed (RR)", "車輪速 (右後)", "轮速 (右后)");
            A("live.voltage", "Voltage", "電圧", "电压");
            A("live.brakePressure", "Brake Pressure", "ブレーキ圧", "制动压力");
            A("live.yawRate", "Yaw Rate", "ヨーレート", "横摆率");
            A("live.steerAngle", "Steering Angle", "ステアリング角", "转向角");
            A("live.gear", "Current Gear", "現在のギア", "当前档位");
            A("live.atfTemp", "ATF Temperature", "ATF 温度", "变速箱油温");
            A("live.tirePressureFL", "Tire Pressure (FL)", "タイヤ空気圧 (左前)", "胎压 (左前)");
            A("live.tirePressureFR", "Tire Pressure (FR)", "タイヤ空気圧 (右前)", "胎压 (右前)");
            A("live.tirePressureRL", "Tire Pressure (RL)", "タイヤ空気圧 (左後)", "胎压 (左后)");
            A("live.tirePressureRR", "Tire Pressure (RR)", "タイヤ空気圧 (右後)", "胎压 (右后)");
            A("live.cabinTemp", "Cabin Temperature", "車内温度", "车内温度");
            A("live.setTemp", "Set Temperature", "設定温度", "设定温度");
            A("live.blower", "Blower Level", "風量", "风量等级");
            A("live.hvBattSoc", "HV Battery SOC", "HV バッテリー SOC", "高压电池 SOC");
            A("live.hvBattVolt", "HV Battery Voltage", "HV バッテリー電圧", "高压电池电压");
            A("live.hvBattTemp", "HV Battery Temp", "HV バッテリー温度", "高压电池温度");
            A("live.motorRpm", "Motor Speed", "モーター回転数", "电机转速");
            A("live.steerTorque", "Steering Torque", "ステアリングトルク", "转向扭矩");
            A("live.assistCurrent", "Assist Current", "アシスト電流", "助力电流");
            A("live.busLoad", "Bus Load", "バス負荷", "总线负载");
            A("live.msgCount", "Message Count", "メッセージ数", "报文数");
            A("live.moduleTemp", "Module Temperature", "モジュール温度", "模块温度");
            A("live.supplyVoltage", "Supply Voltage", "供給電圧", "供电电压");
            A("live.status", "Status", "状態", "状态");
            A("live.ok", "OK", "OK", "正常");
            A("live.inputSpeed", "Input Shaft Speed", "入力軸回転数", "输入轴转速");
            A("live.outputSpeed", "Output Shaft Speed", "出力軸回転数", "输出轴转速");
            A("live.linePressure", "Line Pressure", "ライン圧", "主油路压力");
            A("live.squibDriver", "Driver Squib Resistance", "運転席スクイブ抵抗", "驾驶员气囊点火器电阻");
            A("live.squibPassenger", "Passenger Squib Resistance", "助手席スクイブ抵抗", "乘客气囊点火器电阻");
            A("live.doorStatus", "Door Status", "ドア状態", "车门状态");
            A("live.tireTemp", "Tire Temperature", "タイヤ温度", "轮胎温度");
            A("live.evapTemp", "Evaporator Temp", "エバポレーター温度", "蒸发器温度");
            A("live.motorTemp", "Motor Temperature", "モーター温度", "电机温度");
            A("live.odometer", "Odometer", "走行距離", "里程");
            A("live.fuelGauge", "Fuel Gauge", "燃料計", "燃油表");
            A("live.inverterTemp", "Inverter Temp", "インバーター温度", "逆变器温度");
            A("live.packVoltage", "Pack Voltage", "パック電圧", "电池组电压");
            A("live.packTemp", "Pack Temperature", "パック温度", "电池组温度");
            A("live.cellDelta", "Cell Voltage Delta", "セル電圧差", "单体电压差");
            A("live.radarStatus", "Radar Status", "レーダー状態", "雷达状态");
            A("live.targetDistance", "Target Distance", "対象距離", "目标距离");
            A("live.cameraTemp", "Camera Temp", "カメラ温度", "摄像头温度");
            A("live.motorCurrent", "Motor Current", "モーター電流", "电机电流");
            A("live.keyStatus", "Key Status", "キー状態", "钥匙状态");
            A("live.lfSignal", "LF Signal", "LF 信号", "LF 信号");
            A("live.signalStrength", "Signal Strength", "信号強度", "信号强度");
            A("live.seatPosition", "Seat Position", "シート位置", "座椅位置");
            A("live.headlampCurrent", "Headlamp Current", "ヘッドランプ電流", "前照灯电流");
            A("live.windowPosition", "Window Position", "ウィンドウ位置", "车窗位置");
            A("live.lockStatus", "Lock Status", "ロック状態", "锁止状态");
            A("live.swivelAngle", "Swivel Angle", "スイベル角", "转向角度");
            A("live.levelingAngle", "Leveling Angle", "レベリング角", "调平角度");

            // ECM PID names
            A("pid.rpm", "Engine RPM", "エンジン回転数", "发动机转速");
            A("pid.speed", "Vehicle Speed", "車速", "车速");
            A("pid.coolant", "Coolant Temp", "冷却水温", "冷却液温度");
            A("pid.load", "Engine Load", "エンジン負荷", "发动机负荷");
            A("pid.throttle", "Throttle Position", "スロットル開度", "节气门位置");
            A("pid.iat", "Intake Air Temp", "吸気温度", "进气温度");
            A("pid.maf", "MAF Air Flow", "MAF 吸入空気量", "空气流量");
            A("pid.stft", "Short Term Fuel Trim", "短期燃料補正", "短期燃油修正");
            A("pid.ltft", "Long Term Fuel Trim", "長期燃料補正", "长期燃油修正");
            A("pid.voltage", "Control Module Voltage", "制御モジュール電圧", "控制模块电压");
            A("pid.timing", "Timing Advance", "点火進角", "点火提前角");
            A("pid.fuelLevel", "Fuel Level", "燃料残量", "燃油液位");

            return d;
        }
    }
}
