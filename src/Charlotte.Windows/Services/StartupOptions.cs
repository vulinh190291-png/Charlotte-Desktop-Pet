using System.IO;

namespace Charlotte.Windows.Services;

public sealed record StartupOptions(string DataRoot,bool UsesTestDataRoot,bool DiagnosticShell,bool StartHidden)
{
    public static StartupOptions Parse(IReadOnlyList<string> args,string defaultDataRoot,string? projectRoot)
    {
        string? requested=null;
        var diagnostic=args.Contains("--diagnostic-shell",StringComparer.OrdinalIgnoreCase);
        var startHidden=args.Contains("--start-hidden",StringComparer.OrdinalIgnoreCase);
        for(var index=0;index<args.Count;index++)
        {
            if(!string.Equals(args[index],"--data-dir",StringComparison.OrdinalIgnoreCase)) continue;
            if(requested is not null || index+1>=args.Count || args[index+1].StartsWith("--",StringComparison.Ordinal))
                throw new ArgumentException("--data-dir 需要且只允许一个目录参数。",nameof(args));
            requested=args[++index];
        }

        if(requested is null)
        {
            if(startHidden) throw new ArgumentException("--start-hidden 只允许用于隔离测试配置。",nameof(args));
            return new(Path.GetFullPath(defaultDataRoot),false,diagnostic,false);
        }
        if(!Path.IsPathFullyQualified(requested) || string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("--data-dir 必须是项目 artifacts 内的绝对路径。",nameof(args));

        var candidate=Path.GetFullPath(requested).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        var artifacts=Path.GetFullPath(Path.Combine(projectRoot,"artifacts")).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        var inside=candidate.Equals(artifacts,StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(artifacts+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        if(!inside) throw new ArgumentException("--data-dir 只能指向当前项目 artifacts 目录。",nameof(args));
        return new(candidate,true,diagnostic,startHidden);
    }

    public static string? FindProjectRoot(string startDirectory)
    {
        for(var directory=new DirectoryInfo(Path.GetFullPath(startDirectory));directory is not null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"Charlotte.sln"))) return directory.FullName;
        return null;
    }
}
