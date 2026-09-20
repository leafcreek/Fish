using System.Collections.ObjectModel;
using Fish.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fish.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<BotLogEntry> Log { get; } = [];
    private string _status = "Bereit", _activity = "WoW öffnen und Angeln starten.", _tolerances = "";
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Activity { get => _activity; set => Set(ref _activity, value); }
    public string Tolerances { get => _tolerances; set => Set(ref _tolerances, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new(property));
    }
    public void AddLog(string message, LogLevel level = LogLevel.Info)
        => AddLog(new BotLogEntry(DateTimeOffset.Now, level, "Fish", "app.message", message));

    public void AddLog(BotLogEntry entry)
    {
        Log.Insert(0, entry);
        while (Log.Count > 300) Log.RemoveAt(Log.Count - 1);
    }
}
