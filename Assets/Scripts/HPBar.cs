using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple filled-image HP bar. Assign an Image with Image Type=Filled.
/// </summary>
public class HPBar : MonoBehaviour
{
    public Image hpFill;

    public void SetHP(float current, float max)
    {
        if (hpFill == null || max <= 0f) return;
        hpFill.fillAmount = Mathf.Clamp01(current / max);
    }
}
