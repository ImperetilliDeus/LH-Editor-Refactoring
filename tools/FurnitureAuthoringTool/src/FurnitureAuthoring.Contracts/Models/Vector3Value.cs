using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FurnitureAuthoring.Contracts.Models;

public sealed class Vector3Value : INotifyPropertyChanged
{
    private decimal x;
    private decimal y;
    private decimal z;

    public decimal X
    {
        get => x;
        set => SetField(ref x, value);
    }

    public decimal Y
    {
        get => y;
        set => SetField(ref y, value);
    }

    public decimal Z
    {
        get => z;
        set => SetField(ref z, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref decimal field, decimal value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
