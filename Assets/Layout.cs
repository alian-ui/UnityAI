using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TagChipView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image background;

    public void Set(string text, float score)
    {
        label.text = $"{text} {score:F2}";
        var c = background.color;
        c.a = Mathf.Lerp(0.3f, 0.9f, Mathf.Clamp01(score)); // 濃淡
        background.color = c;
    }
}