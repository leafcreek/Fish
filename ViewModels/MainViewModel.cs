using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fish.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _status = "Bereit", _activity = "WoW öffnen und Angeln starten.";
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Activity { get => _activity; set => Set(ref _activity, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new(property));
    }
}
