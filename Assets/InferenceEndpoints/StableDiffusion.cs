using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class StableDiffusion : MonoBehaviour
{
    [SerializeField] string apiKey = "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";
    [SerializeField] string endpoint = "https://ol4bvx4dz46exugi.us-east-1.aws.endpoints.huggingface.cloud";
    [SerializeField] string prompt = "Astronaut riding a horse";

    IEnumerator Request(string prompt)
    {
        UnityWebRequest request = new UnityWebRequest(endpoint, "POST");
        request.SetRequestHeader("Accept", "image/png");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.SetRequestHeader("Content-Type", "application/json");
        string jsonData = "{\"inputs\": \"" + prompt + "\", \"parameters\": {}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error: " + request.error);
        }
        else
        {
            // バイナリデータ（PNG）を取得
            byte[] imageBytes = request.downloadHandler.data;
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(imageBytes);
            GetComponent<Renderer>().material.mainTexture = texture;
            Debug.Log("Image received and applied.");
        }
        request.Dispose();
    }

    void Start()
    {
        StartCoroutine(Request(prompt));
    }
}
