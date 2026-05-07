using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FurnitureAuthoring.Contracts.Models;

public sealed class FurnitureDefectDto : INotifyPropertyChanged
{
    private string mntnCd = string.Empty;
    private string locCd = string.Empty;
    private string mtrlCd = string.Empty;

    public string MntnCd
    {
        get => mntnCd;
        set => SetField(ref mntnCd, value);
    }

    public string LocCd
    {
        get => locCd;
        set => SetField(ref locCd, value);
    }

    public string MtrlCd
    {
        get => mtrlCd;
        set => SetField(ref mtrlCd, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
