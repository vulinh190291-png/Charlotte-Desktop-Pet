using System.Diagnostics;
using System.IO;

namespace Charlotte.Tests;

public class FormalAssetPrepTests
{
    [Fact]
    public void Formal_asset_plan_maps_exactly_77_frames_without_overview_sheets()
    {
        var root=FindProjectRoot();
        var start=new ProcessStartInfo(Path.Combine(root,".tools","dotnet","dotnet.exe"))
        {
            WorkingDirectory=root,
            UseShellExecute=false,
            CreateNoWindow=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(Path.Combine(root,"tools","Charlotte.AssetPrep"));
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("--list");
        start.Environment["DOTNET_ROOT"]=Path.Combine(root,".tools","dotnet");
        start.Environment["DOTNET_CLI_HOME"]=Path.Combine(root,".tools","cli-home");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]="1";

        using var process=Process.Start(start)??throw new InvalidOperationException("Asset prep process did not start.");
        var output=process.StandardOutput.ReadToEnd();
        var error=process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode==0,$"Exit {process.ExitCode}: {error}");
        Assert.Contains("Mapped frames: 77",output);
        Assert.DoesNotContain("Battle.png ->",output);
        Assert.DoesNotContain("Drag.png ->",output);
        Assert.DoesNotContain("Walk.png ->",output);
    }

    private static string FindProjectRoot()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"Charlotte.sln"))) directory=directory.Parent;
        return directory?.FullName??throw new InvalidOperationException("Project root not found.");
    }
}
