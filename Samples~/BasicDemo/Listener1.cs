using System;
using SiYangUnityEventSystem.Core;
using UnityEngine;

namespace SiYangUnityEventSystem.Demo
{
    public class Listener1 : MonoBehaviour
    {
        private IDisposable _testEventDisposable;
        private void OnEnable()
        {
            _testEventDisposable = SiYangEventBus.Global.Subscribe<TestEvent>(OnTestEvent);
        }

        private void OnDisable()
        {
            _testEventDisposable.Dispose();
        }

        private void OnTestEvent(TestEvent e)
        {
            Debug.Log($"[Listener1] e.id:{e.id} e.name:{e.name}");
        }
    }
}