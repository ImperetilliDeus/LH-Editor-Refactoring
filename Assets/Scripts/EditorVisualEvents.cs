using System;

public static class EditorVisualEvents
{
    public static event Action TopViewRefreshRequested;
    public static event Action OpeningMarkerRefreshRequested;

    public static void RequestTopViewRefresh()
    {
        TopViewRefreshRequested?.Invoke();
    }

    public static void RequestOpeningMarkerRefresh()
    {
        OpeningMarkerRefreshRequested?.Invoke();
    }
}
