using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Charlotte.Core.Animation;

namespace Charlotte.Windows.Windows;

public partial class ControlPanelWindow : Window
{
    private readonly AppCoordinator app;
    private DateOnly selectedDate=DateOnly.FromDateTime(DateTime.Now);
    public ControlPanelWindow(AppCoordinator app)
    {
        this.app=app; InitializeComponent();
        foreach(var item in new[] { ("散步",AnimationId.Walk),("休息",AnimationId.Rest),("睡觉",AnimationId.Sleep),("战斗",AnimationId.Battle),("胜利",AnimationId.Victory) })
        { var button=new Button { Content=item.Item1,Margin=new(4),Padding=new(12,7,12,7) }; button.Click+=(_,_)=>app.RequestAction(item.Item2); ActionsPanel.Children.Add(button); }
        KeyDown+=(_,e)=>{ if(e.Key==Key.Escape) Hide(); };
        Deactivated+=(_,_)=>Hide();
        IsVisibleChanged+=(_,_)=>app.OnPanelVisibilityChanged(IsVisible);
    }

    public void RefreshAll()
    {
        AutoStartCheck.IsChecked=app.AutoStartEnabled;
        TasksPanel.Children.Clear();
        foreach(var item in app.State.Tasks.OrderBy(x=>x.Order))
        {
            var row=new DockPanel { Margin=new(0,3,0,3) };
            var delete=new Button { Content="删除",Padding=new(6,2,6,2),Tag=item.Id }; delete.Click+=(_,_)=>app.DeleteTask((Guid)delete.Tag); DockPanel.SetDock(delete,Dock.Right); row.Children.Add(delete);
            var check=new CheckBox { Content=item.Title,IsChecked=item.IsCompleted,VerticalAlignment=VerticalAlignment.Center,Tag=item.Id };
            check.Click+=(_,_)=>app.SetTask((Guid)check.Tag,check.IsChecked==true); row.Children.Add(check); TasksPanel.Children.Add(row);
        }
        ProgressText.Text=$"已完成 {app.State.Tasks.Count(x=>x.IsCompleted)} / {app.State.Tasks.Count}（最多 7 条）";
        UndoButton.Visibility=app.Undo.ExpiresIn>TimeSpan.Zero?Visibility.Visible:Visibility.Collapsed;
        ScheduleDateText.Text=selectedDate==DateOnly.FromDateTime(DateTime.Now)?$"{selectedDate:yyyy-MM-dd} · 今天":selectedDate.ToString("yyyy-MM-dd");
        SchedulesPanel.Children.Clear();
        foreach(var item in app.Organizer.ForDate(selectedDate))
        {
            var row=new DockPanel { Margin=new(0,3,0,3) };
            var delete=new Button { Content="删除",Padding=new(6,2,6,2),Tag=item.Id }; delete.Click+=(_,_)=>app.DeleteSchedule((Guid)delete.Tag); DockPanel.SetDock(delete,Dock.Right); row.Children.Add(delete);
            var check=new CheckBox { Content=$"{item.Time:HH\\:mm}  {item.Title}",IsChecked=item.IsCompleted,VerticalAlignment=VerticalAlignment.Center,Tag=item.Id };
            if(app.Organizer.IsOverdue(item,DateTime.Now)) check.Foreground=System.Windows.Media.Brushes.Firebrick;
            check.Click+=(_,_)=>app.SetSchedule((Guid)check.Tag,check.IsChecked==true); row.Children.Add(check); SchedulesPanel.Children.Add(row);
        }
    }
    private void AddTask_Click(object s,RoutedEventArgs e) { Try(()=>app.AddTask(TaskInput.Text)); TaskInput.Clear(); }
    private void TaskInput_KeyDown(object s,KeyEventArgs e) { if(e.Key==Key.Enter) AddTask_Click(s,e); else if(e.Key==Key.Escape) TaskInput.Clear(); }
    private void AddSchedule_Click(object s,RoutedEventArgs e) { Try(()=>app.AddSchedule(ScheduleInput.Text,selectedDate,TimeOnly.ParseExact(ScheduleTime.Text,"HH:mm"))); ScheduleInput.Clear(); }
    private void Undo_Click(object s,RoutedEventArgs e)=>app.UndoDelete();
    private void AutoStart_Click(object s,RoutedEventArgs e)
    {
        var result=app.SetAutoStart(AutoStartCheck.IsChecked==true);
        AutoStartCheck.IsChecked=result.Enabled;
        if(!result.Success) MessageBox.Show(this,"无法修改当前用户的开机启动设置。","Charlotte",MessageBoxButton.OK,MessageBoxImage.Information);
    }
    private async void Exit_Click(object s,RoutedEventArgs e)=>await app.RequestExitAsync();
    private void PreviousDay_Click(object s,RoutedEventArgs e) { selectedDate=selectedDate.AddDays(-1); RefreshAll(); }
    private void NextDay_Click(object s,RoutedEventArgs e) { selectedDate=selectedDate.AddDays(1); RefreshAll(); }
    private void Today_Click(object s,RoutedEventArgs e) { selectedDate=DateOnly.FromDateTime(DateTime.Now); RefreshAll(); }
    private void Try(Action action) { try { action(); } catch(Exception error) { MessageBox.Show(this,error.Message,"Charlotte",MessageBoxButton.OK,MessageBoxImage.Information); } }
}
