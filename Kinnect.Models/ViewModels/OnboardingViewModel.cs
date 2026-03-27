namespace Kinnect.Models.ViewModels;

public sealed class OnboardingViewModel
{
    public bool CanImportGedcom { get; init; }

    public bool ShowImportedPersonSelection { get; init; }

    public List<OnboardingPersonOption> People { get; init; } = [];
}

public sealed class OnboardingPersonOption
{
    public int Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}