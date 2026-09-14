using Charlotte.Windows.ViewModels;

namespace Charlotte.Windows.Services;

public sealed class PanelController(
    IPanelHost host,
    ControlPanelViewModel viewModel,
    Action prepareOpen,
    Action<bool> reportVisibility)
{
    public ControlPanelViewModel ViewModel { get; }=viewModel;

    public bool Open()
    {
        if(host.IsVisible) return false;
        prepareOpen();
        host.Show();
        reportVisibility(true);
        return true;
    }

    public bool Close()
    {
        if(!host.IsVisible) return false;
        host.Hide();
        reportVisibility(false);
        return true;
    }

    public void Toggle()
    {
        if(host.IsVisible) Close(); else Open();
    }
}
