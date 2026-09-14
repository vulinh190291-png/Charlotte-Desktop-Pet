namespace Charlotte.Windows.Services;

public interface IPanelHost
{
    bool IsVisible { get; }
    void Show();
    void Hide();
}
