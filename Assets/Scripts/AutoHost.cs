using Mirror;
using UnityEngine;

public class AutoHost : MonoBehaviour
{
    void Start()
    {
        // Запускаем хост автоматически при старте сцены
        if (!NetworkServer.active && !NetworkClient.active)
        {
            NetworkManager.singleton.StartHost();
            Debug.Log("AutoHost: Host started automatically");
        }
    }
}