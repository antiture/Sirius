// ==============================
// File: MergePromptProvider.cs
// 修改点 :
// - 适配新的 MergeResult 结构
// - 不再使用 result.OutputPath
// - 改为使用 OutputDirectory / PartPaths.Count
// ==============================

public static class PromptProvider
{
    public static string Build(MergeProfile profile, MergeResult result)
    {
        return $$"""
 

阅读约束:
1. 先读取顶部索引, 再按文件顺序理解代码.
2. 回答时优先基于真实文件路径引用具体文件.
3. 如果发现重复定义, 先判断是否是 partial / 同名不同目录 / 历史遗留.
4. 如果发现明显分层, 先总结结构, 再分析实现.
5. 如果需要修改代码, 以“文件为单位”输出完整代码.
6. 每个文件开头使用如下格式:
   // ==============================
   // File: 实际文件路径
   // 修改点 :
   // ==============================

当前导出目标:
- TargetKey: {{profile.Key}}
- DisplayName: {{profile.DisplayName}}
- RootPath: {{profile.RootPath}}
- FileCount: {{result.FileCount}}
- OutputDirectory: {{result.OutputDirectory}}
- PartCount: {{result.PartPaths.Count}}

分片文件:
{{BuildPartList(result.PartPaths)}}



Articulation = Manifold（可编辑工作区，始终存在） + Snapshots（追加式历史）

编辑      -> 修改 Manifold
生成      -> 基于 Manifold 推导
提交      -> 冻结 Manifold -> 生成 Snapshot
查看      -> 读取 Snapshot

状态: Started | Snapshotted | Executed
可编辑 = 状态 != Executed


 


你接下来应执行:
A. 先给出项目结构摘要.
B. 再给出关键主线.
C. 再回答具体问题.
D. 如果需要修改代码, 以“文件为单位”输出完整代码, 但是不要打包，直接在聊天中以代码块的形式输出. 
E. 你需要极度重视边界判断，一旦有边界模糊的情况要及时提醒，对边界不确定的情况要敢于提问，而不是似是而非的回答。
F. 我们现在的目标之一是复制度控制， 把原本会扩散、会漂移、会靠人脑记忆维持的复杂性，钉成有限且可验证的规则。复杂度控制 = 减少状态数 + 减少分叉数 + 减少影响面 + 减少脑补量 如有符合上述目标的规则被发现，请务必总结并明确指出来。









""";
    }

    private static string BuildPartList(IReadOnlyList<string> partPaths)
    {
        if (partPaths.Count == 0)
            return "- (none)";

        return string.Join(
            Environment.NewLine,
            partPaths.Select((path, index) => $"- Part {(index + 1):00}: {path}"));
    }
}