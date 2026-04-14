using UnityEngine;
public class ActivePing : MonoBehaviour
{
    public GameObject target;
    public void EnableTarget()
    {
        target.SetActive(true);
        Debug.Log($"EnableTarget: {target.name} activeSelf={target.activeSelf} activeInHierarchy={target.activeInHierarchy}");
    }
}

