namespace Content.Shared._WG.Performance;

/// <summary>Helpers for reused scratch collections.</summary>
public static class WGCollectionExtensions
{
    /// <summary>
    /// Clears a reused set, and releases its storage once a small result follows a crowded one. A hash set's clear
    /// wipes its whole capacity, so one crowded query would otherwise slow every later clear for good, while a run of
    /// crowded queries keeps its storage instead of growing it again each time.
    /// </summary>
    public static void ClearAndTrim<T>(this HashSet<T> set, int trimAbove = 256)
    {
        var small = set.Count <= trimAbove;
        set.Clear();
        if (small && set.Capacity > trimAbove * 4)
            set.TrimExcess();
    }
}
