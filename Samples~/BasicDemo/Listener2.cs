using SiYangUnityEventSystem.Core;
using UnityEngine;

namespace SiYangUnityEventSystem.Demo
{
    public class Listener2 : EventListenerBehaviour
    {
        [ListenEvent]
        private void OnTestEvent(TestEvent e)
        {
            Debug.Log($"[Listener1] e.id:{e.id} e.name:{e.name}");
        }
    }
}