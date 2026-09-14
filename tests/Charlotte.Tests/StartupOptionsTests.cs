using System.IO;
using Charlotte.Core.Animation;
using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class StartupOptionsTests
{
    private static readonly string ProjectRoot=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"CharlotteProject"));
    private static readonly string DefaultRoot=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"CharlotteDefault"));

    [Fact]
    public void No_override_uses_default_runtime_data_root()
    {
        var options=StartupOptions.Parse([],DefaultRoot,ProjectRoot);

        Assert.Equal(DefaultRoot,options.DataRoot);
        Assert.False(options.UsesTestDataRoot);
    }

    [Fact]
    public void Absolute_project_artifacts_path_is_accepted()
    {
        var testRoot=Path.Combine(ProjectRoot,"artifacts","profiles","performance");

        var options=StartupOptions.Parse(["--data-dir",testRoot,"--diagnostic-shell","--start-hidden"],DefaultRoot,ProjectRoot);

        Assert.Equal(Path.GetFullPath(testRoot),options.DataRoot);
        Assert.True(options.UsesTestDataRoot);
        Assert.True(options.DiagnosticShell);
        Assert.True(options.StartHidden);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("C:\\Windows\\Temp\\Charlotte")]
    public void Unsafe_data_override_is_rejected(string requested)
    {
        Assert.Throws<ArgumentException>(()=>StartupOptions.Parse(["--data-dir",requested],DefaultRoot,ProjectRoot));
    }

    [Fact]
    public void Missing_data_dir_value_is_rejected()
    {
        Assert.Throws<ArgumentException>(()=>StartupOptions.Parse(["--data-dir"],DefaultRoot,ProjectRoot));
    }

    [Fact]
    public void Start_hidden_is_rejected_for_normal_user_profile()
    {
        Assert.Throws<ArgumentException>(()=>StartupOptions.Parse(["--start-hidden"],DefaultRoot,ProjectRoot));
    }

    [Fact]
    public void Diagnostic_start_action_is_accepted_for_an_isolated_profile()
    {
        var testRoot=Path.Combine(ProjectRoot,"artifacts","profiles","effects");

        var options=StartupOptions.Parse(["--data-dir",testRoot,"--diagnostic-shell","--start-action","Battle"],DefaultRoot,ProjectRoot);

        Assert.Equal(AnimationId.Battle,options.StartAction);
    }

    [Theory]
    [InlineData("--start-action", "Battle")]
    [InlineData("--diagnostic-shell", "--start-action")]
    [InlineData("--start-action", "Unknown")]
    public void Invalid_diagnostic_start_action_is_rejected(string first,string second)
    {
        Assert.Throws<ArgumentException>(()=>StartupOptions.Parse([first,second],DefaultRoot,ProjectRoot));
    }

    [Fact]
    public void Unknown_action_is_rejected_even_for_an_isolated_profile()
    {
        var testRoot=Path.Combine(ProjectRoot,"artifacts","profiles","effects");

        Assert.Throws<ArgumentException>(()=>StartupOptions.Parse(
            ["--data-dir",testRoot,"--diagnostic-shell","--start-action","Unknown"],DefaultRoot,ProjectRoot));
    }
}
