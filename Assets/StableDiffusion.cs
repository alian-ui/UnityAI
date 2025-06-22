using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class StableDiffusion : MonoBehaviour
{
    // APIキーは適切に保護してください
    private string apiKey = "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";
    private string endpoint = "https://ol4bvx4dz46exugi.us-east-1.aws.endpoints.huggingface.cloud";

    void Start()
    {
        StartCoroutine(QueryHuggingFace("Astronaut riding a horse"));
    }

    IEnumerator QueryHuggingFace(string prompt)
    {
        // JSONデータ作成
        string jsonData = "{\"inputs\": \"" + prompt + "\", \"parameters\": {}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        // UnityWebRequestのセットアップ
        UnityWebRequest request = new UnityWebRequest(endpoint, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Accept", "image/png");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.SetRequestHeader("Content-Type", "application/json");

        // リクエスト送信
        yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (request.result != UnityWebRequest.Result.Success)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
        {
            Debug.LogError("Error: " + request.error);
        }
        else
        {
            // バイナリデータ（PNG）を取得
            byte[] imageBytes = request.downloadHandler.data;

            // 画像として読み込み
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(imageBytes);

            // 例: GameObjectに画像を表示
            GetComponent<Renderer>().material.mainTexture = texture;

            Debug.Log("Image received and applied.");
        }
    }
}
