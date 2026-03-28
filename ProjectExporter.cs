// ==============================
// File: ProjectExporter.cs
// 修改点 :
// - 不再创建时间戳文件夹
// - 直接输出到 options.OutputDirectory
// - 分片文件名改为:
//   {baseName}_{timestamp}_part01.txt
// - 保持:
//   分片输出 / Migrations 排除 / prompt 尾部追加 / 目录树索引 / namespace 索引
// ==============================

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;

public sealed class ProjectExporter
{
    private static readonly Regex NamespaceRegex =
        new(
            @"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*(?:;|\{)",
            RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly HashSet<string> IgnoredDirectoryNames =
    [
        "bin",
        "obj",
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        "node_modules",
        "dist",
        "coverage",
        "publish",
        "out",
        ".angular",
        ".cache",
        "Migrations"
    ];

    public MergeResult Export(MergeProfile profile, MergeOptions options)
    {
        ValidateProfile(profile, options);

        var files = CollectFiles(profile, options);

        Directory.CreateDirectory(options.OutputDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var baseName = Path.GetFileNameWithoutExtension(profile.OutputFileName);
        var partBaseName = $"{baseName}_{timestamp}";

        var resultSeed = new MergeResult(
            OutputDirectory: options.OutputDirectory,
            PartPaths: [],
            FileCount: files.Count);

        var partPaths = WriteMergedTexts(
            outputDirectory: options.OutputDirectory,
            partBaseName: partBaseName,
            profile: profile,
            options: options,
            files: files,
            result: resultSeed);

        return resultSeed with
        {
            PartPaths = partPaths
        };
    }

    private static void ValidateProfile(MergeProfile profile, MergeOptions options)
    {
        if (!Directory.Exists(profile.RootPath))
            throw new DirectoryNotFoundException($"RootPath not found: {profile.RootPath}");

        if (!string.IsNullOrWhiteSpace(profile.ProjectFilePath) && !File.Exists(profile.ProjectFilePath))
            throw new FileNotFoundException($"ProjectFilePath not found: {profile.ProjectFilePath}");

        if (options.MaxPartCharacters < 4096)
            throw new InvalidOperationException($"MaxPartCharacters too small: {options.MaxPartCharacters}");
    }

    private static List<FileEntry> CollectFiles(MergeProfile profile, MergeOptions options)
    {
        var map = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
        var allowedExtensions = profile.Extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasProjectFile = !string.IsNullOrWhiteSpace(profile.ProjectFilePath) && File.Exists(profile.ProjectFilePath);

        if (hasProjectFile)
        {
            AddCSharpFilesFromProject(profile, map, options);
            allowedExtensions.Remove(".cs");
        }

        AddScannedFiles(profile, map, options, allowedExtensions);

        return map.Values
            .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddCSharpFilesFromProject(
        MergeProfile profile,
        Dictionary<string, FileEntry> map,
        MergeOptions options)
    {
        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Configuration"] = "Debug",
            ["DesignTimeBuild"] = "true",
            ["BuildProjectReferences"] = "false",
            ["SkipCompilerExecution"] = "true",
            ["ProvideCommandLineArgs"] = "true"
        };

        if (!string.IsNullOrWhiteSpace(profile.TargetFramework))
            globalProperties["TargetFramework"] = profile.TargetFramework!;

        using var projectCollection = new ProjectCollection(globalProperties);
        var project = projectCollection.LoadProject(profile.ProjectFilePath!);

        foreach (var item in project.GetItems("Compile"))
        {
            var fullPath = item.GetMetadataValue("FullPath");

            if (string.IsNullOrWhiteSpace(fullPath))
                continue;

            fullPath = Path.GetFullPath(fullPath);

            if (!File.Exists(fullPath))
                continue;

            if (!fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsExcludedPath(fullPath))
                continue;

            if (!options.IncludeGenerated && IsGeneratedFile(fullPath))
                continue;

            AddEntry(profile.RootPath, fullPath, map);
        }
    }

    private static void AddScannedFiles(
        MergeProfile profile,
        Dictionary<string, FileEntry> map,
        MergeOptions options,
        HashSet<string> allowedExtensions)
    {
        foreach (var file in EnumerateFilesSafe(profile.RootPath))
        {
            var extension = Path.GetExtension(file);

            if (!allowedExtensions.Contains(extension))
                continue;

            if (IsExcludedPath(file))
                continue;

            if (!options.IncludeGenerated && IsGeneratedFile(file))
                continue;

            AddEntry(profile.RootPath, file, map);
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootPath)
    {
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                var name = Path.GetFileName(directory);

                if (IgnoredDirectoryNames.Contains(name))
                    continue;

                stack.Push(directory);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
                yield return Path.GetFullPath(file);
        }
    }

    private static void AddEntry(
        string rootPath,
        string fullPath,
        Dictionary<string, FileEntry> map)
    {
        var relativePath = NormalizePath(Path.GetRelativePath(rootPath, fullPath));
        var extension = Path.GetExtension(fullPath);
        var namespaceName = extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            ? TryReadNamespace(fullPath)
            : null;

        map[fullPath] = new FileEntry(
            FullPath: fullPath,
            RelativePath: relativePath,
            Extension: extension,
            NamespaceName: namespaceName);
    }

    private static string? TryReadNamespace(string filePath)
    {
        try
        {
            var text = File.ReadAllText(filePath);
            var match = NamespaceRegex.Match(text);

            if (match.Success)
                return match.Groups[1].Value.Trim();

            return "(global)";
        }
        catch
        {
            return "(unknown)";
        }
    }

    private static bool IsExcludedPath(string path)
    {
        var normalized = NormalizePath(path);
        return normalized.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedFile(string path)
    {
        var fileName = Path.GetFileName(path);
        var normalized = NormalizePath(path);

        if (normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fileName.EndsWith(".AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fileName.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(fileName, "GlobalUsings.g.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static IReadOnlyList<string> WriteMergedTexts(
        string outputDirectory,
        string partBaseName,
        MergeProfile profile,
        MergeOptions options,
        List<FileEntry> files,
        MergeResult result)
    {
        var writer = new PartFileWriter(
            outputDirectory: outputDirectory,
            baseFileName: partBaseName,
            maxPartCharacters: options.MaxPartCharacters);

        writer.AppendText(BuildHeaderText(profile, options, files));
        writer.AppendText(BuildDirectoryTreeIndexTextIfNeeded(options, files));
        writer.AppendText(BuildNamespaceIndexTextIfNeeded(options, files));
        writer.AppendText(BuildFilesSectionHeaderText());

        foreach (var file in files)
            WriteFile(writer, file);

        writer.AppendText(BuildTailPromptText(profile, result));

        return writer.Complete();
    }

    private static void WriteFile(PartFileWriter writer, FileEntry file)
    {
        string content;
        try
        {
            content = File.ReadAllText(file.FullPath);
        }
        catch (Exception ex)
        {
            content =
                $"/* READ ERROR */{Environment.NewLine}" +
                $"/* {ex.GetType().Name}: {ex.Message} */";
        }

        var singleHeader = BuildFileHeaderText(file, segmentNumber: null);
        var singleBlock = singleHeader + content + Environment.NewLine + Environment.NewLine;

        if (singleBlock.Length <= writer.MaxPartCharacters)
        {
            writer.AppendBlock(singleBlock);
            return;
        }

        var segmentNumber = 1;
        var position = 0;

        while (position < content.Length)
        {
            var segmentHeader = BuildFileHeaderText(file, segmentNumber);
            var suffix = Environment.NewLine + Environment.NewLine;
            var availableContentLength = writer.MaxPartCharacters - segmentHeader.Length - suffix.Length;

            if (availableContentLength <= 0)
                throw new InvalidOperationException($"MaxPartCharacters too small for file header: {file.RelativePath}");

            var takeLength = FindBestSplitLength(content, position, availableContentLength);
            var chunk = content.Substring(position, takeLength);
            var block = segmentHeader + chunk + suffix;

            writer.AppendBlock(block);

            position += takeLength;
            segmentNumber++;
        }
    }

    private static int FindBestSplitLength(string text, int startIndex, int maxLength)
    {
        var remaining = text.Length - startIndex;
        if (remaining <= maxLength)
            return remaining;

        var hardEnd = startIndex + maxLength;
        var searchStart = Math.Max(startIndex, hardEnd - 2048);

        for (var i = hardEnd - 1; i >= searchStart; i--)
        {
            if (text[i] == '\n')
                return (i - startIndex) + 1;
        }

        return maxLength;
    }

    private static string BuildHeaderText(
        MergeProfile profile,
        MergeOptions options,
        List<FileEntry> files)
    {
        var sb = new StringBuilder();

        sb.AppendLine("================================================================================");
        sb.AppendLine("MERGED PROJECT EXPORT");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Target Key        : {profile.Key}");
        sb.AppendLine($"Display Name      : {profile.DisplayName}");
        sb.AppendLine($"Root Path         : {profile.RootPath}");
        sb.AppendLine($"Project File      : {profile.ProjectFilePath ?? "(folder scan only)"}");
        sb.AppendLine($"Created At        : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"TargetFramework   : {profile.TargetFramework ?? "(none)"}");
        sb.AppendLine($"Index Mode        : {options.IndexMode}");
        sb.AppendLine($"Max Part Chars    : {options.MaxPartCharacters}");
        sb.AppendLine($"Files Count       : {files.Count}");
        sb.AppendLine();
        sb.AppendLine("FILE TYPE SUMMARY");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (var group in files.GroupBy(x => x.Extension).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"{group.Key,-10} {group.Count(),5}");

        sb.AppendLine();

        return sb.ToString();
    }

    private static string BuildDirectoryTreeIndexTextIfNeeded(
        MergeOptions options,
        List<FileEntry> files)
    {
        if (options.IndexMode is not (MergeIndexMode.Tree or MergeIndexMode.Both))
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("DIRECTORY TREE INDEX");
        sb.AppendLine("================================================================================");

        var root = new TreeNode("(root)");

        foreach (var file in files)
        {
            var parts = file.RelativePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            root.Add(parts);
        }

        root.Write(sb, "", true);
        sb.AppendLine();

        return sb.ToString();
    }

    private static string BuildNamespaceIndexTextIfNeeded(
        MergeOptions options,
        List<FileEntry> files)
    {
        if (options.IndexMode is not (MergeIndexMode.Namespace or MergeIndexMode.Both))
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("C# NAMESPACE INDEX");
        sb.AppendLine("================================================================================");

        var groups = files
            .Where(x => x.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.NamespaceName ?? "(global)", StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groups)
        {
            sb.AppendLine(group.Key);

            foreach (var file in group.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"  - {file.RelativePath}");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildFilesSectionHeaderText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("FILES");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string BuildFileHeaderText(FileEntry file, int? segmentNumber)
    {
        var sb = new StringBuilder();

        sb.AppendLine("================================================================================");
        sb.AppendLine($"FILE: {file.RelativePath}");
        sb.AppendLine($"PATH: {file.FullPath}");
        sb.AppendLine($"TYPE: {file.Extension}");

        if (!string.IsNullOrWhiteSpace(file.NamespaceName))
            sb.AppendLine($"NAMESPACE: {file.NamespaceName}");

        if (segmentNumber is not null)
            sb.AppendLine($"FILE_SEGMENT: {segmentNumber.Value}");

        sb.AppendLine("================================================================================");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string BuildTailPromptText(MergeProfile profile, MergeResult result)
    {
        var prompt = PromptProvider.Build(profile, result);

        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("CUSTOM PROMPT");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        sb.AppendLine(prompt);

        if (!prompt.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            sb.AppendLine();

        sb.AppendLine();

        return sb.ToString();
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');

    private sealed class TreeNode(string name)
    {
        public string Name { get; } = name;
        public SortedDictionary<string, TreeNode> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public SortedSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string[] parts)
        {
            Add(parts, 0);
        }

        private void Add(string[] parts, int index)
        {
            if (index >= parts.Length)
                return;

            if (index == parts.Length - 1)
            {
                Files.Add(parts[index]);
                return;
            }

            var dir = parts[index];

            if (!Directories.TryGetValue(dir, out var child))
            {
                child = new TreeNode(dir);
                Directories[dir] = child;
            }

            child.Add(parts, index + 1);
        }

        public void Write(StringBuilder sb, string prefix, bool isLast)
        {
            if (!string.Equals(Name, "(root)", StringComparison.Ordinal))
            {
                sb.Append(prefix);
                sb.Append(isLast ? "└── " : "├── ");
                sb.AppendLine(Name);

                prefix += isLast ? "    " : "│   ";
            }

            var directories = Directories.Values.ToList();
            var fileNames = Files.ToList();

            for (var i = 0; i < directories.Count; i++)
            {
                var isDirectoryLast = i == directories.Count - 1 && fileNames.Count == 0;
                directories[i].Write(sb, prefix, isDirectoryLast);
            }

            for (var i = 0; i < fileNames.Count; i++)
            {
                var isFileLast = i == fileNames.Count - 1;
                sb.Append(prefix);
                sb.Append(isFileLast ? "└── " : "├── ");
                sb.AppendLine(fileNames[i]);
            }
        }
    }

    private sealed class PartFileWriter(
        string outputDirectory,
        string baseFileName,
        int maxPartCharacters)
    {
        private readonly string _outputDirectory = outputDirectory;
        private readonly string _baseFileName = baseFileName;
        private readonly int _maxPartCharacters = maxPartCharacters;
        private readonly List<string> _partPaths = [];
        private readonly StringBuilder _builder = new();
        private int _partNumber = 1;

        public int MaxPartCharacters => _maxPartCharacters;

        public void AppendText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var position = 0;

            while (position < text.Length)
            {
                var remainingPartCapacity = _maxPartCharacters - _builder.Length;

                if (remainingPartCapacity <= 0)
                {
                    FlushPart();
                    remainingPartCapacity = _maxPartCharacters;
                }

                var remainingText = text.Length - position;
                if (remainingText <= remainingPartCapacity)
                {
                    _builder.Append(text, position, remainingText);
                    position += remainingText;
                    continue;
                }

                var takeLength = FindBestSplitLength(text, position, remainingPartCapacity);
                _builder.Append(text, position, takeLength);
                position += takeLength;
                FlushPart();
            }
        }

        public void AppendBlock(string block)
        {
            if (string.IsNullOrEmpty(block))
                return;

            if (block.Length > _maxPartCharacters)
                throw new InvalidOperationException($"Block exceeds MaxPartCharacters: {block.Length} > {_maxPartCharacters}");

            if (_builder.Length > 0 && _builder.Length + block.Length > _maxPartCharacters)
                FlushPart();

            _builder.Append(block);
        }

        public IReadOnlyList<string> Complete()
        {
            if (_builder.Length > 0)
                FlushPart();

            return _partPaths;
        }

        private void FlushPart()
        {
            if (_builder.Length == 0)
                return;

            var partPath = Path.Combine(
                _outputDirectory,
                $"{_baseFileName}_part{_partNumber:00}.txt");

            File.WriteAllText(partPath, _builder.ToString(), new UTF8Encoding(false));
            _partPaths.Add(partPath);

            _builder.Clear();
            _partNumber++;
        }
    }
}

public static class MsBuildBootstrapper
{
    public static void Register()
    {
        if (MSBuildLocator.IsRegistered)
            return;

        var instances = MSBuildLocator
            .QueryVisualStudioInstances()
            .OrderByDescending(x => x.Version)
            .ToArray();

        if (instances.Length > 0)
        {
            MSBuildLocator.RegisterInstance(instances[0]);
            return;
        }

        MSBuildLocator.RegisterDefaults();
    }
}