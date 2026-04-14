using UnityEngine;

public class ActiveSpy : MonoBehaviour
{
    void OnEnable()  => Debug.Log($"{name} ENABLED", this);
    void OnDisable() => Debug.Log($"{name} DISABLED", this);
}
