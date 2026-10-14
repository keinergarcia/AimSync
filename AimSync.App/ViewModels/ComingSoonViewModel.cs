using AimSync.App.Infrastructure;

namespace AimSync.App.ViewModels;

public sealed class ComingSoonViewModel : ObservableObject
{
    public ComingSoonViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }

    public string Description { get; }
}