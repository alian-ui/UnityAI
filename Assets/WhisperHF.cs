using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

public class WhisperHF : MonoBehaviour
{
    private static readonly string endpoint = "https://alsroi6mov5tf7gh.us-east-1.aws.endpoints.huggingface.cloud";
    private static readonly string apiKey = "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";

    public IEnumerator Query(string filename, System.Action<string> onResult)
    {
        byte[] data = File.ReadAllBytes(filename);

        UnityWebRequest request = new UnityWebRequest(endpoint, "POST");
        request.uploadHandler = new UploadHandlerRaw(data);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.SetRequestHeader("Content-Type", "audio/flac");

        yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (request.result == UnityWebRequest.Result.Success)
#else
        if (!request.isNetworkError && !request.isHttpError)
#endif
        {
            onResult?.Invoke(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError(request.error);
            onResult?.Invoke(null);
        }
        request.Dispose();
    }

    void Start()
    {
        string filePath = Application.dataPath + "/Whisper/data/answering-machine16kHz.wav";
        StartCoroutine(this.Query(filePath, OnQueryResult));
    }

    void OnQueryResult(string result)
    {
        if (!string.IsNullOrEmpty(result))
        {
            Debug.Log("APIレスポンス: " + result);
        }
        else
        {
            Debug.LogError("APIリクエストに失敗しました");
        }
    }

    void Update()
    {        
    }
}
