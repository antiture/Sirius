// ==============================
// File: Program.cs
// 修改点 :
// - 注释中补充 AppConfig.Targets.Authentication
// - 其余逻辑不变
// ==============================

MsBuildBootstrapper.Register();

// 改这里:
// AppConfig.Targets.Api
// AppConfig.Targets.Tests
// AppConfig.Targets.Frontend
// AppConfig.Targets.Authentication
const string selectedTarget = AppConfig.Targets.Frontend;

// 改这里:
// true  = 导出后自动打开所有 txt
// false = 只导出, 不打开
const bool openWhenCompleted = false;

if (!AppConfig.Profiles.TryGetValue(selectedTarget, out var profile))
    throw new InvalidOperationException($"Unknown target: {selectedTarget}");

var exporter = new ProjectExporter();
var result = exporter.Export(profile, AppConfig.Options);

OutputLauncher.PrintSummary(profile, result);

if (openWhenCompleted)
    OutputLauncher.OpenAllFiles(result.PartPaths);