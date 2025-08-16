using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Newtonsoft.Json;
using TMPro;

[System.Serializable]
public class TagResult
{
    public string tag;
    public string tag_ja;
    public float score;
}

[System.Serializable]
public class ClassifyResponse
{
    public List<TagResult> top_k;
}

[System.Serializable]
public class ClassifyRequest
{
    public string image_b64;
    public int top_k = 3;
}

[System.Serializable]
public class ImageData
{
    public string fileName;
    public Texture2D texture;
    public List<TagResult> tags = new List<TagResult>();
    public string imagePath;
}

public class RunCLIP : MonoBehaviour
{
    [Header("UI References")]
    public Button selectFolderButton;
    public Button saveCSVButton;
    public Transform imageContainer;
    public GameObject imageCardPrefab;
    public Transform tagFilterContainer;
    public GameObject tagFilterButtonPrefab;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Slider progressBar; // プログレスバーSlider（オプション）
    [SerializeField] private Image progressBarFill; // プログレスバー進行用Image
    [SerializeField] private TMP_Text progressText; // プログレス表示用テキスト
    [SerializeField] private Slider thresholdSlider; // 閾値調整用スライダー
    [SerializeField] private TMP_Text thresholdValueText; // 閾値表示用テキスト
    
    public ScrollRect scrollRect;

    [Header("Server Settings")]
    public string serverURL = "http://127.0.0.1:8000";
    
    [Header("Filter Settings")]
    [SerializeField] private float scoreThreshold = 0.1f; // 選択されたタグのスコア閾値
    [SerializeField] private string selectedFilterTag = ""; // 現在選択されているフィルタータグ
    [SerializeField] private float lastSliderValue = -1f; // 前回のスライダー値
    
    private List<ImageData> imageDataList = new List<ImageData>();
    private List<GameObject> imageCards = new List<GameObject>();
    private List<Button> tagFilterButtons = new List<Button>();
    private HashSet<string> activeFilters = new HashSet<string>();
    private Dictionary<string, string> tagTranslations = new Dictionary<string, string>();
    private bool isProcessing = false; // 処理中フラグを追加

    // ▼Button から呼べる void メソッド（引数なし）
    public void OnClickPickFolder()
    {
        if (isProcessing)
        {
            Debug.Log("処理中のため、フォルダ選択をスキップします");
            return;
        }
        SelectImageFolder();
    }

    public void OnClickExportCSV()
    {
        if (isProcessing)
        {
            Debug.Log("処理中のため、CSV出力をスキップします");
            return;
        }
        SaveToCSV();
    }

    public void OnClickExportCurrent()
    {
        // 現在選択中の画像のCSV出力（後で実装）
        UpdateStatus("現在選択中の画像をCSV出力（未実装）");
    }

    // ▼Slider から呼べる（引数あり）
    public void OnThresholdChanged(float value)
    {
        Debug.Log($"OnThresholdChanged called with value: {value}");
        Debug.Log($"Slider current value: {thresholdSlider?.value}");
        Debug.Log($"Slider min/max: {thresholdSlider?.minValue}/{thresholdSlider?.maxValue}");
        
        scoreThreshold = value;
        Debug.Log($"scoreThreshold set to: {scoreThreshold}");
        
        // 閾値表示テキストを更新
        if (thresholdValueText != null)
        {
            thresholdValueText.text = $"{value:F2}";
        }
        
        // 選択されたタグに基づいて画像をフィルタリング
        FilterImagesBySelectedTag();
        
        if (string.IsNullOrEmpty(selectedFilterTag))
        {
            UpdateStatus($"閾値を {value:F2} に設定しました（タグを選択してください）");
        }
        else
        {
            UpdateStatus($"「{selectedFilterTag}」タグの閾値を {value:F2} に設定しました");
        }
        
        Debug.Log($"Threshold updated to: {value:F2} for tag: {selectedFilterTag}");
    }

    void Start()
    {
        InitializeUI();
        LoadTagTranslations();
        SetupLayoutGroups(); // レイアウト設定を追加
    }

    void Update()
    {
        // スライダーの値変更を監視
        if (thresholdSlider != null && lastSliderValue != thresholdSlider.value)
        {
            lastSliderValue = thresholdSlider.value;
            Debug.Log($"Slider value changed detected: {lastSliderValue}");
            OnThresholdChanged(lastSliderValue);
        }
    }

    void SetupLayoutGroups()
    {
        // Image Container のレイアウト設定
        if (imageContainer != null)
        {
            GridLayoutGroup gridLayout = imageContainer.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
            {
                gridLayout = imageContainer.gameObject.AddComponent<GridLayoutGroup>();
            }
            
            gridLayout.cellSize = new Vector2(300, 320);
            gridLayout.spacing = new Vector2(12, 12);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
            
            ContentSizeFitter contentSizeFitter = imageContainer.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter == null)
            {
                contentSizeFitter = imageContainer.gameObject.AddComponent<ContentSizeFitter>();
            }
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            Debug.Log("Image Container のレイアウトグループを設定しました");
        }

        // Scroll Rect の設定調整
        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 1.0f;
            
            // Scrollbar の Layout Element 設定
            if (scrollRect.verticalScrollbar != null)
            {
                LayoutElement scrollbarLayout = scrollRect.verticalScrollbar.GetComponent<LayoutElement>();
                if (scrollbarLayout == null)
                {
                    scrollbarLayout = scrollRect.verticalScrollbar.gameObject.AddComponent<LayoutElement>();
                }
                scrollbarLayout.ignoreLayout = true;
            }
            
            if (scrollRect.horizontalScrollbar != null)
            {
                LayoutElement scrollbarLayout = scrollRect.horizontalScrollbar.GetComponent<LayoutElement>();
                if (scrollbarLayout == null)
                {
                    scrollbarLayout = scrollRect.horizontalScrollbar.gameObject.AddComponent<LayoutElement>();
                }
                scrollbarLayout.ignoreLayout = true;
            }
            
            Debug.Log("Scroll Rect の設定を調整しました");
        }

        // Tag Filter Container のレイアウト設定
        if (tagFilterContainer != null)
        {
            HorizontalLayoutGroup horizontalLayout = tagFilterContainer.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout == null)
            {
                horizontalLayout = tagFilterContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            }
            
            horizontalLayout.spacing = 6;
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = false;  // サイズ制御を無効に
            horizontalLayout.childControlHeight = false;
            horizontalLayout.childScaleWidth = false;
            horizontalLayout.childScaleHeight = false;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;
            
            ContentSizeFitter tagContentSizeFitter = tagFilterContainer.GetComponent<ContentSizeFitter>();
            if (tagContentSizeFitter == null)
            {
                tagContentSizeFitter = tagFilterContainer.gameObject.AddComponent<ContentSizeFitter>();
            }
            tagContentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tagContentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            Debug.Log("Tag Filter Container のレイアウトグループを設定しました");
        }
    }

    void InitializeUI()
    {
        // 必須コンポーネントのチェック
        if (selectFolderButton == null)
        {
            Debug.LogError("Pick Folder Button が未設定です！");
            return;
        }
        
        if (saveCSVButton == null)
        {
            Debug.LogError("Save CSV Button が未設定です！");
            return;
        }
        
        if (statusText == null)
        {
            Debug.LogError("Status Text が未設定です！");
            return;
        }

        // オプショナルコンポーネントの警告
        if (tagFilterContainer == null)
        {
            Debug.LogWarning("Tag Filter Container が未設定です。タグフィルター機能は無効になります。");
        }
        
        if (tagFilterButtonPrefab == null)
        {
            Debug.LogWarning("Tag Filter Button Prefab が未設定です。タグフィルター機能は無効になります。");
        }

        // イベントリスナーの重複を防ぐため、既存のリスナーをクリア
        selectFolderButton.onClick.RemoveAllListeners();
        saveCSVButton.onClick.RemoveAllListeners();

        // Unityエディタのイベント設定を使用する場合はここでのAddListenerは不要
        // selectFolderButton.onClick.AddListener(SelectImageFolder);
        // saveCSVButton.onClick.AddListener(SaveToCSV);
        
        saveCSVButton.interactable = false;
        UpdateStatus("フォルダを選択してください");
        UpdateProgress(0f); // プログレスバーを初期化
        
        // 閾値スライダーの初期化
        if (thresholdSlider != null)
        {
            // スライダーの設定を強制的に設定
            thresholdSlider.minValue = 0f;
            thresholdSlider.maxValue = 1f;
            thresholdSlider.wholeNumbers = false;
            thresholdSlider.value = scoreThreshold;
            
            Debug.Log($"Slider settings: min={thresholdSlider.minValue}, max={thresholdSlider.maxValue}, wholeNumbers={thresholdSlider.wholeNumbers}");
            
            // 初期値を記録
            lastSliderValue = thresholdSlider.value;
            
            // スライダーのイベントリスナーをクリアして再設定
            thresholdSlider.onValueChanged.RemoveAllListeners();
            thresholdSlider.onValueChanged.AddListener(OnThresholdChanged);
            Debug.Log($"Threshold slider initialized: value={thresholdSlider.value}, scoreThreshold={scoreThreshold}");
        }
        else
        {
            Debug.LogWarning("thresholdSlider is null! Please assign it in the inspector.");
        }
        
        // 閾値表示テキストの初期化
        if (thresholdValueText != null)
        {
            thresholdValueText.text = $"{scoreThreshold:F2}";
        }
        
        Debug.Log("InitializeUI 完了: Unityエディタのボタンイベント設定を使用");
    }

    void LoadTagTranslations()
    {
        // tags.jsonから日本語翻訳を読み込み
        string tagsPath = Path.Combine(Application.streamingAssetsPath, "tags.json");
        if (File.Exists(tagsPath))
        {
            string json = File.ReadAllText(tagsPath);
            var tagsData = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, string>>>>(json);
            foreach (var tag in tagsData["tags"])
            {
                tagTranslations[tag["en"]] = tag["ja"];
            }
        }
    }

    void SelectImageFolder()
    {
        if (isProcessing)
        {
            Debug.Log("既に処理中です。フォルダ選択をスキップします。");
            return;
        }

        // Unityエディタでのフォルダ選択（実際のビルドでは別の方法を使用）
        #if UNITY_EDITOR
        string folderPath = UnityEditor.EditorUtility.OpenFolderPanel("画像フォルダを選択", "", "");
        if (!string.IsNullOrEmpty(folderPath))
        {
            StartCoroutine(LoadImagesFromFolder(folderPath));
        }
        #else
        // ビルド版では固定パスまたは別の方法を使用
        UpdateStatus("エディタでのみフォルダ選択が可能です");
        #endif
    }

    IEnumerator LoadImagesFromFolder(string folderPath)
    {
        isProcessing = true; // 処理開始
        selectFolderButton.interactable = false; // ボタンを無効化
        UpdateProgress(0f); // プログレスバーを初期化
        
        UpdateStatus("画像を読み込み中...");
        Debug.Log($"フォルダから画像を読み込み開始: {folderPath}");
        
        string[] imageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
        List<string> validImages = new List<string>();
        int totalFiles = imageFiles.Length;
        
        foreach (string file in imageFiles)
        {
            string ext = Path.GetExtension(file).ToLower();
            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
            {
                validImages.Add(file);
            }
        }

        Debug.Log($"総ファイル数: {totalFiles}, 有効な画像ファイル数: {validImages.Count}");

        if (validImages.Count == 0)
        {
            if (totalFiles == 0)
            {
                UpdateStatus("フォルダが空です");
            }
            else
            {
                UpdateStatus($"対応する画像ファイルが見つかりませんでした（{totalFiles}個のファイルを確認）\n対応形式: JPG, JPEG, PNG");
            }
            UpdateProgress(0f); // プログレスバーをリセット
            isProcessing = false; // 処理終了
            selectFolderButton.interactable = true; // ボタンを有効化
            yield break;
        }

        // 画像以外のファイルがある場合の情報表示
        if (totalFiles > validImages.Count)
        {
            int ignoredFiles = totalFiles - validImages.Count;
            Debug.Log($"画像以外のファイル {ignoredFiles}個をスキップしました");
        }

        imageDataList.Clear();
        ClearImageCards();

        Debug.Log($"画像の読み込みを開始します ({validImages.Count} 枚)");

        for (int i = 0; i < validImages.Count; i++)
        {
            string imagePath = validImages[i];
            string fileName = Path.GetFileName(imagePath);
            
            // プログレス更新（読み込み段階: 0-0.5）
            float loadProgress = (float)i / validImages.Count * 0.5f;
            UpdateProgress(loadProgress);
            
            UpdateStatus($"画像を読み込み中... ({i + 1}/{validImages.Count}) - {fileName}");
            Debug.Log($"画像読み込み開始 ({i + 1}/{validImages.Count}): {fileName}");
            
            yield return StartCoroutine(LoadImage(imagePath));
            yield return new WaitForEndOfFrame();
        }

        Debug.Log($"全画像読み込み完了。ImageCards総数: {imageCards.Count}");
        UpdateProgress(0.5f); // 読み込み完了
        UpdateStatus($"{validImages.Count}枚の画像を読み込みました。タグ付けを開始中...");
        StartCoroutine(ProcessAllImages());
    }

    IEnumerator LoadImage(string imagePath)
    {
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture("file://" + imagePath))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                ImageData imageData = new ImageData
                {
                    fileName = Path.GetFileName(imagePath),
                    texture = texture,
                    imagePath = imagePath
                };
                imageDataList.Add(imageData);
                CreateImageCard(imageData);
            }
        }
    }

    void CreateImageCard(ImageData imageData)
    {
        Debug.Log($"CreateImageCard 開始: {imageData.fileName}");
        
        // 必須コンポーネントの事前チェック
        if (imageCardPrefab == null)
        {
            Debug.LogError("imageCardPrefab が設定されていません！");
            UpdateStatus("エラー: Image Card Prefab が未設定です");
            return;
        }
        
        if (imageContainer == null)
        {
            Debug.LogError("imageContainer が設定されていません！");
            UpdateStatus("エラー: Image Container が未設定です");
            return;
        }

        GameObject card = Instantiate(imageCardPrefab, imageContainer);
        imageCards.Add(card);
        
        Debug.Log($"カードを作成しました: {imageData.fileName} (総数: {imageCards.Count})");

        try
        {
            // カードのコンポーネントを設定（安全な検索）
            Transform imageTransform = card.transform.Find("RawImage_Thumb");
            if (imageTransform == null)
                imageTransform = card.transform.Find("Image");
            
            Transform fileNameTransform = card.transform.Find("FileName");
            Transform tagsContainerTransform = card.transform.Find("TagChips");
            if (tagsContainerTransform == null)
                tagsContainerTransform = card.transform.Find("TagsContainer");

            Debug.Log($"コンポーネント検索結果: Image={imageTransform != null}, FileName={fileNameTransform != null}, Tags={tagsContainerTransform != null}");

            // 画像コンポーネントの設定
            if (imageTransform != null)
            {
                RawImage rawImageComponent = imageTransform.GetComponent<RawImage>();
                if (rawImageComponent != null)
                {
                    // RawImageを使用する場合
                    rawImageComponent.texture = imageData.texture;
                    Debug.Log($"RawImage テクスチャを設定: {imageData.fileName}");
                }
                else
                {
                    // Imageを使用する場合
                    Image imageComponent = imageTransform.GetComponent<Image>();
                    if (imageComponent != null)
                    {
                        imageComponent.sprite = Sprite.Create(imageData.texture, 
                            new Rect(0, 0, imageData.texture.width, imageData.texture.height), 
                            new Vector2(0.5f, 0.5f));
                        Debug.Log($"Image スプライトを設定: {imageData.fileName}");
                    }
                    else
                    {
                        Debug.LogWarning($"画像コンポーネント (RawImage/Image) が見つかりません: {imageData.fileName}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"画像表示用Transformが見つかりません: {imageData.fileName}");
            }

            // ファイル名の設定
            if (fileNameTransform != null)
            {
                Text fileNameText = fileNameTransform.GetComponent<Text>();
                if (fileNameText != null)
                {
                    fileNameText.text = imageData.fileName;
                    Debug.Log($"ファイル名を設定 (Text): {imageData.fileName}");
                }
                else
                {
                    // TMP_Textの場合
                    TMP_Text tmpFileNameText = fileNameTransform.GetComponent<TMP_Text>();
                    if (tmpFileNameText != null)
                    {
                        tmpFileNameText.text = imageData.fileName;
                        Debug.Log($"ファイル名を設定 (TMP_Text): {imageData.fileName}");
                    }
                    else
                    {
                        Debug.LogWarning($"ファイル名用Textコンポーネントが見つかりません: {imageData.fileName}");
                    }
                }
            }

            // カードにImageDataを関連付け
            ImageCard imageCardComponent = card.GetComponent<ImageCard>();
            if (imageCardComponent != null)
            {
                imageCardComponent.imageData = imageData;
                // タグコンテナも設定
                if (tagsContainerTransform != null)
                {
                    imageCardComponent.tagsContainer = tagsContainerTransform;
                }
                
                // 初期タグ表示（閾値適用）
                if (imageData.tags != null && imageData.tags.Count > 0)
                {
                    imageCardComponent.UpdateTagDisplay(scoreThreshold);
                }
                
                Debug.Log($"ImageCard コンポーネントを設定: {imageData.fileName}");
            }
            else
            {
                Debug.LogWarning($"ImageCard コンポーネントが見つかりません: {imageData.fileName}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CreateImageCard でエラーが発生しました: {e.Message}");
            UpdateStatus($"カード作成エラー: {e.Message}");
        }
        
        Debug.Log($"CreateImageCard 完了: {imageData.fileName}");
        
        // レイアウトを強制更新
        StartCoroutine(ForceLayoutUpdate());
    }
    
    IEnumerator ForceLayoutUpdate()
    {
        yield return null; // 1フレーム待つ
        if (imageContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(imageContainer.GetComponent<RectTransform>());
        }
    }

    IEnumerator ProcessAllImages()
    {
        for (int i = 0; i < imageDataList.Count; i++)
        {
            // プログレス更新（タグ付け段階: 0.5-1.0）
            float tagProgress = 0.5f + (float)i / imageDataList.Count * 0.5f;
            UpdateProgress(tagProgress);
            
            UpdateStatus($"タグ付け中... ({i + 1}/{imageDataList.Count})");
            yield return StartCoroutine(ClassifyImage(imageDataList[i]));
            yield return new WaitForSeconds(0.1f); // サーバー負荷軽減
        }

        CreateTagFilterButtons();
        UpdateAllImageCards();
        saveCSVButton.interactable = true;
        UpdateProgress(1.0f); // 処理完了
        UpdateStatus($"完了！ {imageDataList.Count}枚の画像にタグ付けしました。");
        
        isProcessing = false; // 処理終了
        selectFolderButton.interactable = true; // ボタンを有効化
    }

    IEnumerator ClassifyImage(ImageData imageData)
    {
        // 画像をBase64エンコード
        byte[] imageBytes = imageData.texture.EncodeToJPG();
        string base64Image = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);

        ClassifyRequest request = new ClassifyRequest
        {
            image_b64 = base64Image,
            top_k = 3
        };

        string jsonRequest = JsonConvert.SerializeObject(request);

        string classifyURL = serverURL + "/classify";
        using (UnityWebRequest www = new UnityWebRequest(classifyURL, "POST"))
        {
            byte[] requestData = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
            www.uploadHandler = new UploadHandlerRaw(requestData);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    ClassifyResponse response = JsonConvert.DeserializeObject<ClassifyResponse>(www.downloadHandler.text);
                    imageData.tags = response.top_k;
                }
                catch (Exception e)
                {
                    Debug.LogError($"タグ付けエラー: {e.Message}");
                    // デフォルトタグを設定
                    imageData.tags.Add(new TagResult { tag = "unknown", tag_ja = "不明", score = 0.0f });
                }
            }
            else
            {
                Debug.LogError($"サーバーエラー: {www.error}");
                // デフォルトタグを設定
                imageData.tags.Add(new TagResult { tag = "error", tag_ja = "エラー", score = 0.0f });
            }
        }
    }

    void CreateTagFilterButtons()
    {
        // 必須コンポーネントのチェック
        if (tagFilterContainer == null)
        {
            Debug.LogError("tagFilterContainer が設定されていません！TagFilter機能をスキップします。");
            UpdateStatus("警告: タグフィルターコンテナが未設定です");
            return;
        }
        
        if (tagFilterButtonPrefab == null)
        {
            Debug.LogError("tagFilterButtonPrefab が設定されていません！TagFilter機能をスキップします。");
            UpdateStatus("警告: タグフィルターボタンプレファブが未設定です");
            return;
        }

        // 既存のフィルターボタンをクリア
        foreach (Button btn in tagFilterButtons)
        {
            if (btn != null) Destroy(btn.gameObject);
        }
        tagFilterButtons.Clear();

        // 全てのタグを収集
        HashSet<string> allTags = new HashSet<string>();
        foreach (ImageData imageData in imageDataList)
        {
            foreach (TagResult tag in imageData.tags)
            {
                allTags.Add(tag.tag);
            }
        }

        if (allTags.Count == 0)
        {
            Debug.Log("タグが見つかりませんでした。フィルターボタンは作成されません。");
            return;
        }

        // タグフィルターボタンを作成
        foreach (string tag in allTags)
        {
            try
            {
                GameObject filterBtn = Instantiate(tagFilterButtonPrefab, tagFilterContainer);
                Button button = filterBtn.GetComponent<Button>();
                
                if (button == null)
                {
                    Debug.LogError($"tagFilterButtonPrefab にButtonコンポーネントがありません！");
                    Destroy(filterBtn);
                    continue;
                }

                // ボタンサイズの設定
                RectTransform buttonRect = filterBtn.GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    buttonRect.sizeDelta = new Vector2(80, 30); // 適切なサイズに設定
                }

                // Layout Element でサイズを制御
                LayoutElement layoutElement = filterBtn.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = filterBtn.AddComponent<LayoutElement>();
                }
                layoutElement.preferredWidth = 80;
                layoutElement.preferredHeight = 30;

                // テキストコンポーネントを検索（Text または TMP_Text）
                Text buttonText = filterBtn.GetComponentInChildren<Text>();
                TMP_Text tmpButtonText = null;
                
                if (buttonText == null)
                {
                    tmpButtonText = filterBtn.GetComponentInChildren<TMP_Text>();
                }

                string displayText = tagTranslations.ContainsKey(tag) ? tagTranslations[tag] : tag;
                
                if (buttonText != null)
                {
                    buttonText.text = displayText;
                }
                else if (tmpButtonText != null)
                {
                    tmpButtonText.text = displayText;
                }
                else
                {
                    Debug.LogWarning($"タグボタンにTextコンポーネントが見つかりません: {tag}");
                }
                
                button.onClick.AddListener(() => ToggleTagFilter(tag, button));
                tagFilterButtons.Add(button);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"タグフィルターボタン作成エラー ({tag}): {e.Message}");
            }
        }
        
        Debug.Log($"タグフィルターボタンを {tagFilterButtons.Count} 個作成しました");
    }

    void ToggleTagFilter(string tag, Button button)
    {
        // 他のボタンの選択状態をクリア
        foreach (Button btn in tagFilterButtons)
        {
            if (btn != null && btn.GetComponent<Image>() != null)
            {
                btn.GetComponent<Image>().color = Color.white;
            }
        }

        // 同じタグが選択されている場合はクリア
        if (selectedFilterTag == tag)
        {
            selectedFilterTag = "";
            UpdateStatus("タグフィルターをクリアしました");
        }
        else
        {
            // 新しいタグを選択
            selectedFilterTag = tag;
            button.GetComponent<Image>().color = Color.cyan;
            SetSelectedTag(tag);
        }

        // フィルタリングを実行
        FilterImagesBySelectedTag();
    }

    void UpdateAllImageCards()
    {
        if (imageCards == null || imageCards.Count == 0)
        {
            Debug.Log("更新する画像カードがありません");
            return;
        }

        int updatedCount = 0;
        foreach (GameObject card in imageCards)
        {
            if (card == null)
            {
                Debug.LogWarning("null の画像カードが見つかりました");
                continue;
            }

            ImageCard imageCard = card.GetComponent<ImageCard>();
            if (imageCard == null)
            {
                Debug.LogWarning("ImageCardコンポーネントが見つかりません");
                continue;
            }

            try
            {
                imageCard.UpdateTagDisplay(scoreThreshold);
                updatedCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"画像カード更新エラー: {e.Message}");
            }
        }
        
        Debug.Log($"画像カードを {updatedCount} 個更新しました");
    }

    void SaveToCSV()
    {
        string csvContent = "FileName,Tag1,Score1,Tag1_JP,Tag2,Score2,Tag2_JP,Tag3,Score3,Tag3_JP\n";
        
        foreach (ImageData imageData in imageDataList)
        {
            string line = imageData.fileName;
            
            for (int i = 0; i < 3; i++)
            {
                if (i < imageData.tags.Count)
                {
                    TagResult tag = imageData.tags[i];
                    line += $",{tag.tag},{tag.score:F3},{tag.tag_ja}";
                }
                else
                {
                    line += ",,,";
                }
            }
            
            csvContent += line + "\n";
        }

        #if UNITY_EDITOR
        string savePath = UnityEditor.EditorUtility.SaveFilePanel("CSVファイルを保存", "", "image_tags", "csv");
        if (!string.IsNullOrEmpty(savePath))
        {
            File.WriteAllText(savePath, csvContent);
            UpdateStatus($"CSVファイルを保存しました: {savePath}");
        }
        #else
        string savePath = Path.Combine(Application.persistentDataPath, "image_tags.csv");
        File.WriteAllText(savePath, csvContent);
        UpdateStatus($"CSVファイルを保存しました: {savePath}");
        #endif
    }

    void ClearImageCards()
    {
        foreach (GameObject card in imageCards)
        {
            if (card != null) Destroy(card);
        }
        imageCards.Clear();
    }

    void UpdateStatus(string message)
    {
        statusText.text = message;
        Debug.Log(message);
    }

    void UpdateProgress(float progress)
    {
        // Sliderがある場合
        if (progressBar != null)
        {
            progressBar.value = progress;
        }
        
        // Image Fill方式の場合
        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = progress;
        }
        
        // プログレステキスト更新
        if (progressText != null)
        {
            progressText.text = $"{progress * 100:F0}%";
        }
        
        Debug.Log($"Progress updated: {progress:F2}");
    }

    void UpdateProgress(int current, int total)
    {
        if (total <= 0)
        {
            UpdateProgress(0f);
            return;
        }
        
        float progress = (float)current / total;
        UpdateProgress(progress);
    }

    void FilterImagesBySelectedTag()
    {
        Debug.Log($"FilterImagesBySelectedTag called - selectedTag: '{selectedFilterTag}', threshold: {scoreThreshold}");
        
        if (imageCards == null || imageCards.Count == 0)
        {
            Debug.Log("No image cards to filter");
            return;
        }

        int visibleCount = 0;
        int hiddenCount = 0;

        foreach (GameObject card in imageCards)
        {
            if (card == null) continue;

            ImageCard imageCard = card.GetComponent<ImageCard>();
            if (imageCard == null || imageCard.imageData == null) continue;

            bool shouldShow = ShouldShowImage(imageCard.imageData);
            card.SetActive(shouldShow);

            if (shouldShow)
            {
                visibleCount++;
            }
            else
            {
                hiddenCount++;
            }
        }

        Debug.Log($"画像フィルタリング完了: 表示={visibleCount}, 非表示={hiddenCount}");
    }

    bool ShouldShowImage(ImageData imageData)
    {
        // タグが選択されていない場合は全て表示
        if (string.IsNullOrEmpty(selectedFilterTag))
        {
            return true;
        }

        // 選択されたタグを検索
        if (imageData.tags != null)
        {
            foreach (TagResult tag in imageData.tags)
            {
                if (tag != null && 
                    (tag.tag == selectedFilterTag || tag.tag_ja == selectedFilterTag))
                {
                    Debug.Log($"Checking image {imageData.fileName}: tag '{tag.tag}' score={tag.score}, threshold={scoreThreshold}, show={tag.score >= scoreThreshold}");
                    if (tag.score >= scoreThreshold)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    void SetSelectedTag(string tagName)
    {
        selectedFilterTag = tagName;
        FilterImagesBySelectedTag();
        
        string displayName = tagTranslations.ContainsKey(tagName) ? tagTranslations[tagName] : tagName;
        UpdateStatus($"「{displayName}」タグでフィルタリング中（閾値: {scoreThreshold:F2}）");
        Debug.Log($"Selected filter tag: {tagName}");
    }

    public void ClearTagFilter()
    {
        selectedFilterTag = "";
        FilterImagesBySelectedTag();
        UpdateStatus("タグフィルターをクリアしました");
    }
}
