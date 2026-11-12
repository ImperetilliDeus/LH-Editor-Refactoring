using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public class LhDefectCodeService : MonoBehaviour
{
    private const string ProductionEndpoint = "https://lhbaro.lh.or.kr/api/selectMblMntnFlwCsList.do";

    [Header("Request")]
    [SerializeField] private string endpointUrl = ProductionEndpoint;
    [SerializeField] private bool requestOnStart = true;

    [Header("Room Types")]
    [SerializeField] private RoomTypePreset roomTypePreset;
    [SerializeField] private bool applyRoomCodesToRoomTypePreset = true;

    [Header("Events")]
    public DefectCodeListEvent DefectCodesLoaded = new DefectCodeListEvent();
    public StringEvent RequestFailed = new StringEvent();

    [Header("Debug")]
    [SerializeField] private LhDefectMaterialCodeList loadedCodes = new LhDefectMaterialCodeList();
    [SerializeField] private string lastError;
    [SerializeField] private bool enableDebugLogs = true;

    public static LhDefectCodeService Instance { get; private set; }
    public LhDefectMaterialCodeList LoadedCodes => loadedCodes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple LhDefectCodeService instances exist. Using the latest instance.", this);
        }

        Instance = this;
    }

    private void Start()
    {
        if (requestOnStart)
        {
            RequestDefectCodes();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RequestDefectCodes()
    {
        StartCoroutine(RequestDefectCodesInternal());
    }

    public void LoadFromJson(string json)
    {
        if (TryParseDefectCodes(json, out LhDefectMaterialCodeList parsed, out string error))
        {
            loadedCodes = parsed;
            if (applyRoomCodesToRoomTypePreset)
            {
                ApplyRoomCodesToPreset(loadedCodes);
            }

            lastError = string.Empty;
            LogDebug($"Loaded defect codes. count={loadedCodes?.mtrlList?.Count ?? 0}");
            DefectCodesLoaded?.Invoke(loadedCodes);
            return;
        }

        lastError = error;
        LogWarning($"Failed to parse defect codes. error={lastError}");
        RequestFailed?.Invoke(lastError);
    }

    public void ApplyRoomCodesToPreset(LhDefectMaterialCodeList codes)
    {
        RoomTypePreset targetPreset = roomTypePreset != null ? roomTypePreset : LoadDefaultRoomTypePreset();
        if (targetPreset == null || codes == null)
        {
            return;
        }

        if (targetPreset.presets == null)
        {
            targetPreset.presets = new List<RoomTypePreset.RoomType>();
        }

        targetPreset.presets.Clear();
        HashSet<int> addedCodes = new HashSet<int>();
        IReadOnlyList<LhRoomCodeOption> roomCodes = codes.GetRoomCodes();
        for (int i = 0; i < roomCodes.Count; i++)
        {
            LhRoomCodeOption roomCode = roomCodes[i];
            if (roomCode == null || !int.TryParse(roomCode.code, out int parsedCode) || !addedCodes.Add(parsedCode))
            {
                continue;
            }

            targetPreset.presets.Add(new RoomTypePreset.RoomType
            {
                name = !string.IsNullOrWhiteSpace(roomCode.displayName) ? roomCode.displayName : roomCode.code,
                code = parsedCode,
            });
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetPreset);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    public static bool TryParseDefectCodes(string json, out LhDefectMaterialCodeList parsed, out string error)
    {
        parsed = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Defect code response is empty.";
            return false;
        }

        try
        {
            string normalizedJson = NormalizeDefectCodeJson(json);
            parsed = JsonUtility.FromJson<LhDefectMaterialCodeList>(normalizedJson);
            if (parsed == null)
            {
                error = "Defect code response could not be parsed.";
                return false;
            }

            if (parsed.mtrlList == null)
            {
                parsed.mtrlList = new System.Collections.Generic.List<LhDefectMaterialCode>();
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private IEnumerator RequestDefectCodesInternal()
    {
        string jsonData = JsonUtility.ToJson(new RequestBody());
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(jsonBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                lastError = request.error;
                LogWarning($"Defect code request failed. url={endpointUrl}, error={lastError}");
                RequestFailed?.Invoke(lastError);
                yield break;
            }

            LoadFromJson(request.downloadHandler.text);
        }
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[LhDefectCodeService] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[LhDefectCodeService] {message}", this);
        }
    }

    private static string NormalizeDefectCodeJson(string json)
    {
        string trimmed = json.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return "{\"mtrlList\":" + trimmed + "}";
        }

        if (trimmed.Contains("\"mtrlList\""))
        {
            return trimmed;
        }

        Response response = JsonUtility.FromJson<Response>(trimmed);
        if (response == null || response.body == null || string.IsNullOrWhiteSpace(response.body.resultData))
        {
            return trimmed;
        }

        string resultData = response.body.resultData.Replace("\\", string.Empty).Trim('"');
        return "{\"mtrlList\":" + resultData + "}";
    }

    private static RoomTypePreset LoadDefaultRoomTypePreset()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<RoomTypePreset>(RoomTypeCatalog.DefaultAssetPath);
#else
        return null;
#endif
    }

    [Serializable]
    private class RequestBody
    {
    }

    [Serializable]
    private class Response
    {
#pragma warning disable CS0649
        public ResponseHead head;
        public ResponseBody body;
#pragma warning restore CS0649
    }

    [Serializable]
    private class ResponseHead
    {
#pragma warning disable CS0649
        public string result_msg;
        public string result_code;
#pragma warning restore CS0649
    }

    [Serializable]
    private class ResponseBody
    {
#pragma warning disable CS0649
        public string resultData;
#pragma warning restore CS0649
    }

    [Serializable]
    public class DefectCodeListEvent : UnityEvent<LhDefectMaterialCodeList>
    {
    }

    [Serializable]
    public class StringEvent : UnityEvent<string>
    {
    }
}
