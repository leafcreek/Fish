using System.Collections.ObjectModel;
using Fish.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fish.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<BotLogEntry> Log { get; } = [];
    public ObservableCollection<BotLogEntry> FilteredLog { get; } = [];
    public string SearchText { get; private set; } = "";
    public LogFilterLevel FilterLevel { get; private set; }
    public int FilterMinutes { get; private set; }
    public string LogSummary => FilteredLog.Count == 0 ? (Log.Count == 0 ? "Noch keine Einträge." : "Keine Treffer · Filter ändern oder zurücksetzen.") : $"{FilteredLog.Count} von {Log.Count} Einträgen · neueste zuerst";
    public MainViewModel() => Log.CollectionChanged += (_, _) => RefreshLogFilter();
    public void SetLogFilter(string query, LogFilterLevel level, int minutes)
    {
        SearchText = query; FilterLevel = level; FilterMinutes = minutes;
        RefreshLogFilter();
    }
    public void RefreshLogFilter(DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        var desired = Log.Where(entry => LogFilter.Matches(entry, SearchText, FilterLevel, FilterMinutes, at)).ToArray();
        for (int i = FilteredLog.Count - 1; i >= 0; i--)
            if (!desired.Contains(FilteredLog[i])) FilteredLog.RemoveAt(i);
        for (int i = 0; i < desired.Length; i++)
            if (i >= FilteredLog.Count || FilteredLog[i] != desired[i]) FilteredLog.Insert(i, desired[i]);
        while (FilteredLog.Count > desired.Length) FilteredLog.RemoveAt(FilteredLog.Count - 1);
        PropertyChanged?.Invoke(this, new(nameof(LogSummary)));
    }
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
