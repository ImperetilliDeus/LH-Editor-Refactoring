using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class LhDefectMaterialCode
{
    public string mblSpceCd;
    public string mblSpceCdNm;
    public string mblLocCd;
    public string mblLocCdNm;
    public string mblMtrlCd;
    public string mblMtrlCdNm;

    public FurnitureDefectCatalogEntry ToFurnitureDefectEntry()
    {
        return new FurnitureDefectCatalogEntry
        {
            mntnCd = mblSpceCd ?? string.Empty,
            locCd = mblLocCd ?? string.Empty,
            mtrlCd = mblMtrlCd ?? string.Empty,
        };
    }
}

[Serializable]
public class LhDefectMaterialCodeList
{
    public List<LhDefectMaterialCode> mtrlList = new List<LhDefectMaterialCode>();

    public IReadOnlyList<LhRoomCodeOption> GetRoomCodes()
    {
        return mtrlList
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.mblSpceCd))
            .GroupBy(item => item.mblSpceCd)
            .Select(group => new LhRoomCodeOption
            {
                code = group.Key,
                displayName = group.First().mblSpceCdNm ?? string.Empty,
            })
            .ToList();
    }

    public IReadOnlyList<LhDefectLocationCodeOption> GetLocationCodes()
    {
        return mtrlList
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.mblLocCd))
            .GroupBy(item => item.mblLocCd)
            .Select(group => new LhDefectLocationCodeOption
            {
                code = group.Key,
                displayName = group.First().mblLocCdNm ?? string.Empty,
            })
            .ToList();
    }

    public List<LhDefectMaterialCode> Filter(string roomCode, string locationCode)
    {
        return mtrlList
            .Where(item => item != null &&
                           string.Equals(item.mblSpceCd, roomCode, StringComparison.Ordinal) &&
                           string.Equals(item.mblLocCd, locationCode, StringComparison.Ordinal))
            .ToList();
    }
}

[Serializable]
public class LhRoomCodeOption
{
    public string code;
    public string displayName;
}

[Serializable]
public class LhDefectLocationCodeOption
{
    public string code;
    public string displayName;
}
