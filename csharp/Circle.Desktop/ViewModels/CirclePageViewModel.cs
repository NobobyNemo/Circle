namespace Circle.Desktop.ViewModels;

public sealed class CirclePageViewModel : ViewModelBase
{
    public CircleViewModel CircleViewModel { get; }
    public CirclePanelViewModel CirclePanelViewModel { get; }

    public CirclePageViewModel(CircleViewModel circleViewModel, CirclePanelViewModel circlePanelViewModel)
    {
        CircleViewModel = circleViewModel;
        CirclePanelViewModel = circlePanelViewModel;
    }
}
