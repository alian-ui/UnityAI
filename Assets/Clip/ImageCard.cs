using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ImageCard : MonoBehaviour
{
    [HideInInspector]
    public ImageData imageData;
    
    public GameObject tagChipPrefab;
    public Transform tagsContainer;
    
    void Start()
    {
        if (tagsContainer == null)
        {
            tagsContainer = transform.Find("TagsContainer");
        }
    }
    
    public void UpdateTagDisplay(float scoreThreshold = 0.0f)
    {
        // 必須データのチェック
        if (imageData == null)
        {
            Debug.LogWarning("ImageCard: imageData が null です");
            return;
        }
        
        if (tagsContainer == null)
        {
            // TagsContainer を検索
            tagsContainer = transform.Find("TagChips");
            if (tagsContainer == null)
                tagsContainer = transform.Find("TagsContainer");
            
            if (tagsContainer == null)
            {
                Debug.LogWarning("ImageCard: TagsContainer が見つかりません");
                return;
            }
        }
        
        // 既存のタグチップをクリア
        foreach (Transform child in tagsContainer)
        {
            if (child != null)
                Destroy(child.gameObject);
        }
        
        // タグが存在しない場合は終了
        if (imageData.tags == null || imageData.tags.Count == 0)
        {
            Debug.Log($"ImageCard ({imageData.fileName}): タグがありません");
            return;
        }
        
        // 新しいタグチップを作成（閾値フィルタリング適用）
        int createdChips = 0;
        foreach (TagResult tag in imageData.tags)
        {
            if (tag != null && tag.score >= scoreThreshold)
            {
                CreateTagChip(tag);
                createdChips++;
            }
        }
        
        Debug.Log($"ImageCard ({imageData.fileName}): {createdChips} 個のタグチップを作成（閾値: {scoreThreshold:F2}）");
    }
    
    void CreateTagChip(TagResult tag)
    {
        // プレファブのチェック
        if (tagChipPrefab == null)
        {
            Debug.LogWarning("ImageCard: tagChipPrefab が設定されていません");
            return;
        }
        
        if (tag == null)
        {
            Debug.LogWarning("ImageCard: tag が null です");
            return;
        }

        try
        {
            GameObject chip = Instantiate(tagChipPrefab, tagsContainer);
            
            // テキストコンポーネントを検索
            Text chipText = chip.GetComponentInChildren<Text>();
            TMPro.TMP_Text tmpChipText = null;
            
            if (chipText == null)
            {
                tmpChipText = chip.GetComponentInChildren<TMPro.TMP_Text>();
            }
            
            // スコアに応じてタグの表示を決定
            string displayText = $"{tag.tag_ja} {tag.score:F2}";
            
            if (chipText != null)
            {
                chipText.text = displayText;
            }
            else if (tmpChipText != null)
            {
                tmpChipText.text = displayText;
            }
            else
            {
                Debug.LogWarning("ImageCard: タグチップにTextコンポーネントが見つかりません");
            }
            
            // スコアに応じて色を変更
            Image chipImage = chip.GetComponent<Image>();
            if (chipImage != null)
            {
                if (tag.score > 0.7f)
                {
                    chipImage.color = Color.green;
                }
                else if (tag.score > 0.5f)
                {
                    chipImage.color = Color.yellow;
                }
                else
                {
                    chipImage.color = Color.gray;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ImageCard: タグチップ作成エラー ({tag.tag}): {e.Message}");
        }
    }
    
    public void OnImageClick()
    {
        // 画像をクリックした時の処理（拡大表示など）
        Debug.Log($"画像をクリック: {imageData.fileName}");
    }
}
