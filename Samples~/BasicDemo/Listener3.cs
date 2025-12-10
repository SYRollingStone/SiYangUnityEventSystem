using SiYangUnityEventSystem.Core;
using SiYangUnityEventSystem.Core.HostModel;
using UnityEngine;

namespace SiYangUnityEventSystem.Demo
{
    public class Listener3 : MonoBehaviour,IAutoEventListener
    {
        [ListenEvent]
        private void OnTestEvent(TestEvent e)
        {
            Debug.Log($"[Listener1] e.id:{e.id} e.name:{e.name}");
        }
    }
}