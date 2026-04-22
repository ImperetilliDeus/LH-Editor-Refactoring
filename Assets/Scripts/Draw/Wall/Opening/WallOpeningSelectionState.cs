internal sealed class WallOpeningSelectionState
{
    public WallOpening SelectedOpening { get; private set; }

    public event System.Action<WallOpening> SelectionChanged;

    public bool SetSelectedOpening(WallOpening opening)
    {
        if (SelectedOpening == opening)
        {
            return false;
        }

        SelectedOpening = opening;
        SelectionChanged?.Invoke(SelectedOpening);
        return true;
    }

    public bool ClearSelectedOpening()
    {
        if (SelectedOpening == null)
        {
            return false;
        }

        SelectedOpening = null;
        SelectionChanged?.Invoke(null);
        return true;
    }
}
