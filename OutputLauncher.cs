// ==============================
// File: OutputLauncher.cs
// 修改点 :
// - summary 收口为“直接输出文件”语义
// - 保留自动打开所有生成文件的方法
// ==============================

using System.Diagnostics;

public static class OutputLauncher
{
    public static void PrintSummary(MergeProfile profile, MergeResult result)
    {
        Console.WriteLine("DONE");
        Console.WriteLine($"Target       : {profile.Key}");
        Console.WriteLine($"Root         : {profile.RootPath}");
        Console.WriteLine($"Files        : {result.FileCount}");
        Console.WriteLine($"Parts        : {result.PartPaths.Count}");
        Console.WriteLine($"Output Dir   : {result.OutputDirectory}");
        Console.WriteLine();

        for (var i = 0; i < result.PartPaths.Count; i++)
        {
            var partPath = result.PartPaths[i];
            Console.WriteLine($"Part {(i + 1).ToString().PadLeft(2, '0')} Path : {partPath}");
            Console.WriteLine($"Part {(i + 1).ToString().PadLeft(2, '0')} Uri  : {new Uri(partPath).AbsoluteUri}");
        }
    }

    public static void OpenAllFiles(IReadOnlyList<string> partPaths)
    {
        if (partPaths.Count == 0)
            return;

        foreach (var partPath in partPaths)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = partPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenFile failed: {partPath}");
                Console.WriteLine($"Reason         : {ex.Message}");
            }
        }
    }
}