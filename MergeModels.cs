// ==============================
// File: MergeModels.cs
// 修改点 :
// - MergeOptions 删除 OpenWhenCompleted
// ==============================

public enum MergeIndexMode
{
    None,
    Tree,
    Namespace,
    Both
}

public sealed record MergeOptions(
    string OutputDirectory,
    MergeIndexMode IndexMode,
    bool IncludeGenerated,
    int MaxPartCharacters);

public sealed record MergeProfile(
    string Key,
    string DisplayName,
    string RootPath,
    string? ProjectFilePath,
    string OutputFileName,
    string[] Extensions,
    string? TargetFramework);

public sealed record MergeResult(
    string OutputDirectory,
    IReadOnlyList<string> PartPaths,
    int FileCount);

internal sealed record FileEntry(
    string FullPath,
    string RelativePath,
    string Extension,
    string? NamespaceName);