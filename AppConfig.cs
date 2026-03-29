// ==============================
// File: AppConfig.cs
// 修改点 :
// - 新增 Targets.Authentication
// - 新增 ZxAuthentication 导出配置
// ==============================

public static class AppConfig
{
    public static class Targets
    {
        public const string Api = "api";
        public const string Tests = "tests";
        public const string Frontend = "frontend";
        public const string Authentication = "authentication";
    }

    public const int DefaultMaxPartCharacters = 7793834;

    public static MergeOptions Options { get; } = new(
        OutputDirectory: @"C:\Users\antit\Desktop",
        IndexMode: MergeIndexMode.Both,
        IncludeGenerated: false,
        MaxPartCharacters: DefaultMaxPartCharacters);

    public static IReadOnlyDictionary<string, MergeProfile> Profiles { get; } =
        new Dictionary<string, MergeProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [Targets.Api] = new(
                Key: Targets.Api,
                DisplayName: "Andromeda.ApiService",
                RootPath: @"C:\Repos\Perso\Vitrism\Andromeda\backend\src\Andromeda.ApiService",
                ProjectFilePath: @"C:\Repos\Perso\Vitrism\Andromeda\backend\src\Andromeda.ApiService\Andromeda.ApiService.csproj",
                OutputFileName: "Andromeda.ApiService.merged.txt",
                Extensions:
                [
                    ".cs",
                    ".csproj",
                    ".cshtml",
                    ".razor",
                    ".json",
                    ".ts",
                    ".js"
                ],
                TargetFramework: "net10.0"),

            [Targets.Tests] = new(
                Key: Targets.Tests,
                DisplayName: "Andromeda.ApiService.Tests",
                RootPath: @"C:\Repos\Perso\Vitrism\Andromeda\backend\tests\Andromeda.ApiService.Tests",
                ProjectFilePath: @"C:\Repos\Perso\Vitrism\Andromeda\backend\tests\Andromeda.ApiService.Tests\Andromeda.ApiService.Tests.csproj",
                OutputFileName: "Andromeda.ApiService.Tests.merged.txt",
                Extensions:
                [
                    ".cs",
                    ".csproj",
                    ".json",
                    ".ts",
                    ".js"
                ],
                TargetFramework: "net10.0"),

            [Targets.Frontend] = new(
                Key: Targets.Frontend,
                DisplayName: "rw-ng-client",
                RootPath: @"C:\Repos\Perso\Vitrism\Andromeda\frontend\rw-ng-client",
                ProjectFilePath: null,
                OutputFileName: "rw-ng-client.merged.txt",
                Extensions:
                [
                    ".ts",
                    ".js",
                    ".json",
                    ".html",
                    ".css",
                    ".scss"
                ],
                TargetFramework: null),

            [Targets.Authentication] = new(
                Key: Targets.Authentication,
                DisplayName: "ZxAuthentication",
                RootPath: @"C:\Repos\Perso\Vitrism\Cassiopeia",
                ProjectFilePath: null,
                OutputFileName: "ZxAuthentication.merged.txt",
                Extensions:
                [
                    ".cs",
                    ".cshtml",
                    ".csproj",
                    ".razor",
                    ".json",
                    ".ts",
                    ".js",
                    ".html",
                    ".css",
                    ".scss"
                ],
                TargetFramework: null)
        };
}