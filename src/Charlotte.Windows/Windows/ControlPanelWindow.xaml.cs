using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;
using Charlotte.Core.Animation;
using Charlotte.Windows.Services;
using Charlotte.Windows.ViewModels;

namespace Charlotte.Windows.Windows;

public partial class ControlPanelWindow : Window,IPanelHost
{
    private readonly ControlPanelViewModel viewModel;
    private readonly Func<bool> readAutoStart;
    private readonly Func<bool,AutoStartResult> setAutoStart;
    private readonly Func<Task> requestExit;
    private readonly Action requestClose;
    private readonly DispatcherTimer refreshTimer=new() { Interval=TimeSpan.FromMilliseconds(250) };
    private bool initialized;

    public ControlPanelWindow(
        ControlPanelViewModel viewModel,
        Func<bool> readAutoStart,
        Func<bool,AutoStartResult> setAutoStart,
        Func<Task> requestExit,
        Action requestClose)
    {
        this.viewModel=viewModel;
        this.readAutoStart=readAutoStart;
        this.setAutoStart=setAutoStart;
        this.requestExit=requestExit;
        this.requestClose=requestClose;
        InitializeComponent();
        DataContext=viewModel;
        Tabs.SelectedIndex=(int)viewModel.SelectedTab;
        initialized=true;
        foreach(var item in new[] { ("散步",AnimationId.Walk),("休息",AnimationId.Rest),("睡觉",AnimationId.Sleep),("战斗",AnimationId.Battle),("胜利",AnimationId.Victory) })
        {
            var button=new Button { Content=item.Item1,Margin=new(4),Padding=new(12,7,12,7),Command=viewModel.RequestActionCommand,CommandParameter=item.Item2 };
            ActionsPanel.Children.Add(button);
        }
        KeyDown+=(_,e)=>{ if(e.Key==Key.Escape) requestClose(); };
        Deactivated+=(_,_)=>Dispatcher.BeginInvoke(() =>
        {
            if(!OwnedWindows.Cast<Window>().Any(x=>x.IsVisible)) requestClose();
        });
        IsVisibleChanged+=(_,_)=>
        {
            if(IsVisible) { RefreshAll(); refreshTimer.Start(); }
            else refreshTimer.Stop();
        };
        viewModel.Changed+=RefreshAll;
        viewModel.PropertyChanged+=OnViewModelPropertyChanged;
        refreshTimer.Tick+=(_,_)=>RefreshTransientState();
        Closed+=(_,_)=>
        {
            refreshTimer.Stop();
            viewModel.Changed-=RefreshAll;
            viewModel.PropertyChanged-=OnViewModelPropertyChanged;
        };
    }

    public void RefreshAll()
    {
        if(Tabs.SelectedIndex!=(int)viewModel.SelectedTab) Tabs.SelectedIndex=(int)viewModel.SelectedTab;
        AutoStartCheck.IsChecked=readAutoStart();
        ProgressText.Text=viewModel.ProgressText;
        RefreshTransientState();
        ScheduleDateText.Text=viewModel.ScheduleDateText;

        TasksPanel.Children.Clear();
        foreach(var item in viewModel.Tasks)
        {
            var row=new DockPanel { Margin=new(0,3,0,3) };
            var delete=new Button { Content="删除",Padding=new(6,2,6,2),Command=viewModel.DeleteTaskCommand,CommandParameter=item.Id };
            DockPanel.SetDock(delete,Dock.Right);
            row.Children.Add(delete);
            var check=new CheckBox { Content=item.Title,IsChecked=item.IsCompleted,VerticalAlignment=VerticalAlignment.Center,Tag=item.Id };
            check.Click+=(_,_)=>viewModel.SetTaskCommand.Execute(new TaskCompletionChange((Guid)check.Tag,check.IsChecked==true));
            row.Children.Add(check);
            TasksPanel.Children.Add(row);
        }

        SchedulesPanel.Children.Clear();
        foreach(var item in viewModel.Schedules)
        {
            var row=new DockPanel { Margin=new(0,3,0,3) };
            var delete=new Button { Content="删除",Padding=new(6,2,6,2),Command=viewModel.DeleteScheduleCommand,CommandParameter=item.Id };
            DockPanel.SetDock(delete,Dock.Right);
            row.Children.Add(delete);
            var check=new CheckBox { Content=$"{item.Time:HH\\:mm}  {item.Title}",IsChecked=item.IsCompleted,VerticalAlignment=VerticalAlignment.Center,Tag=item.Id };
            if(viewModel.IsOverdue(item)) check.Foreground=System.Windows.Media.Brushes.Firebrick;
            check.Click+=(_,_)=>viewModel.SetScheduleCommand.Execute(new ScheduleCompletionChange((Guid)check.Tag,check.IsChecked==true));
            row.Children.Add(check);
            SchedulesPanel.Children.Add(row);
        }
    }

    private void AddTask_Click(object sender,RoutedEventArgs e)=>viewModel.AddTaskCommand.Execute(null);

    private void TaskInput_KeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Enter) { viewModel.AddTaskCommand.Execute(null); e.Handled=true; }
        else if(e.Key==Key.Escape) { viewModel.TaskInput=string.Empty; e.Handled=true; }
    }

    private void AddSchedule_Click(object sender,RoutedEventArgs e)=>viewModel.AddScheduleCommand.Execute(null);

    private void ScheduleInput_KeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Enter) { viewModel.AddScheduleCommand.Execute(null); e.Handled=true; }
        else if(e.Key==Key.Escape) { viewModel.ScheduleInput=string.Empty; viewModel.ScheduleTime="09:00"; e.Handled=true; }
    }

    private void Undo_Click(object sender,RoutedEventArgs e)=>viewModel.UndoCommand.Execute(null);

    private void AutoStart_Click(object sender,RoutedEventArgs e)
    {
        var result=setAutoStart(AutoStartCheck.IsChecked==true);
        AutoStartCheck.IsChecked=result.Enabled;
        if(!result.Success) viewModel.ReportError("无法修改当前用户的开机启动设置。");
    }

    private async void Exit_Click(object sender,RoutedEventArgs e)=>await requestExit();
    private void PreviousDay_Click(object sender,RoutedEventArgs e)=>viewModel.PreviousDayCommand.Execute(null);
    private void NextDay_Click(object sender,RoutedEventArgs e)=>viewModel.NextDayCommand.Execute(null);
    private void Today_Click(object sender,RoutedEventArgs e)=>viewModel.TodayCommand.Execute(null);

    private void Tabs_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(initialized && Tabs.SelectedIndex is >=0 and <=2) viewModel.SelectedTab=(PanelTab)Tabs.SelectedIndex;
    }

    private void RefreshTransientState()
    {
        UndoButton.Content=viewModel.UndoText;
        UndoButton.Visibility=viewModel.CanUndo?Visibility.Visible:Visibility.Collapsed;
    }

    private void OnViewModelPropertyChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(ControlPanelViewModel.ErrorText)) ErrorText.Text=viewModel.ErrorText??string.Empty;
    }
}
