using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

public class WhisperHF : MonoBehaviour
{
    [SerializeField] string apiKey = "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";
    [SerializeField] string endpoint = "https://alsroi6mov5tf7gh.us-east-1.aws.endpoints.huggingface.cloud";

    IEnumerator Request(string filename)
    {
        UnityWebRequest request = new UnityWebRequest(endpoint, "POST");
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.SetRequestHeader("Content-Type", "audio/flac");
        byte[] data = File.ReadAllBytes(filename);
        request.uploadHandler = new UploadHandlerRaw(data);
        request.downloadHandler = new DownloadHandlerBuffer();
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(request.error);
        }
        else
        {
            Debug.Log("Response: " + request.downloadHandler.text);
        }
        request.Dispose();
    }

    void Start()
    {
        string filePath = Application.dataPath + "/Whisper/data/answering-machine16kHz.wav";
        StartCoroutine(Request(filePath));
    }
}
