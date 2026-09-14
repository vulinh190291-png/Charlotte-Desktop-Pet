using Charlotte.Core.Organizer;
using Charlotte.Tests.Fixtures;
using Charlotte.Windows.Services;
using Charlotte.Windows.ViewModels;
using Charlotte.Windows.Windows;
using System.Windows.Controls;

namespace Charlotte.Tests;

public sealed class ControlPanelViewModelTests
{
    [Fact]
    public void Panel_reopen_keeps_session_tab()
    {
        using var panel=PanelFixture.Create();
        Assert.Equal(PanelTab.DailyTasks,panel.ViewModel.SelectedTab);
        panel.Open();
        panel.ViewModel.SelectedTab=PanelTab.Schedule;
        panel.Close();
        panel.Open();

        Assert.Equal(PanelTab.Schedule,panel.ViewModel.SelectedTab);
        Assert.Equal(2,panel.Host.ShowCount);
    }

    [Fact]
    public void Opening_an_already_visible_panel_is_idempotent()
    {
        using var panel=PanelFixture.Create();

        panel.Open();
        panel.Open();

        Assert.Equal(1,panel.Host.ShowCount);
        Assert.Equal(1,panel.OpenPreparations);
        Assert.True(panel.LastReportedVisibility);
    }

    [Fact]
    public void Task_completion_routes_interaction_and_victory_once()
    {
        using var panel=PanelFixture.Create();
        var id=panel.Organizer.AddTask("喝水");

        panel.Organizer.SetTaskCompleted(id,true);
        panel.Organizer.SetTaskCompleted(id,true);

        Assert.Equal(1,panel.Interactions);
        Assert.Equal(1,panel.VictoryRequests);
        Assert.Equal(0,panel.SaveRequests);
    }

    [Fact]
    public void Expected_input_error_is_exposed_without_mutating_state()
    {
        using var panel=PanelFixture.Create();
        panel.ViewModel.TaskInput="   ";

        panel.ViewModel.AddTaskCommand.Execute(null);

        Assert.Empty(panel.State.Tasks);
        Assert.False(string.IsNullOrWhiteSpace(panel.ViewModel.ErrorText));
        Assert.Equal(0,panel.SaveRequests);
    }

    [Fact]
    public void Successful_task_add_clears_input_and_requests_one_save()
    {
        using var panel=PanelFixture.Create();
        panel.ViewModel.TaskInput="整理书桌";

        panel.ViewModel.AddTaskCommand.Execute(null);

        Assert.Equal("整理书桌",panel.State.Tasks.Single().Title);
        Assert.Equal(string.Empty,panel.ViewModel.TaskInput);
        Assert.Null(panel.ViewModel.ErrorText);
        Assert.Equal(1,panel.SaveRequests);
    }

    [Fact]
    public void Unexpected_command_exception_is_not_swallowed()
    {
        var command=new RelayCommand(_=>throw new InvalidOperationException("boom"),_=>false);

        Assert.Throws<InvalidOperationException>(()=>command.Execute(null));
    }

    [Fact]
    public void Window_uses_the_persistent_view_model_tab()
    {
        using var panel=PanelFixture.Create();
        panel.ViewModel.SelectedTab=PanelTab.Schedule;

        StaTest.Run(() =>
        {
            var window=new ControlPanelWindow(
                panel.ViewModel,
                ()=>false,
                enabled=>new AutoStartResult(true,enabled),
                ()=>Task.CompletedTask,
                ()=>{ });
            try
            {
                window.RefreshAll();

                Assert.Same(panel.ViewModel,window.DataContext);
                Assert.Equal(2,((TabControl)window.FindName("Tabs")).SelectedIndex);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Window_immediately_displays_expected_input_error()
    {
        using var panel=PanelFixture.Create();

        StaTest.Run(() =>
        {
            var window=new ControlPanelWindow(
                panel.ViewModel,
                ()=>false,
                enabled=>new AutoStartResult(true,enabled),
                ()=>Task.CompletedTask,
                ()=>{ });
            try
            {
                panel.ViewModel.TaskInput="   ";
                panel.ViewModel.AddTaskCommand.Execute(null);

                Assert.Equal(panel.ViewModel.ErrorText,((TextBlock)window.FindName("ErrorText")).Text);
            }
            finally { window.Close(); }
        });
    }
}
