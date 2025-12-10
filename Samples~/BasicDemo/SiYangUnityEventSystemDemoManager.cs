using System.Collections;
using System.Collections.Generic;
using SiYangUnityEventSystem.Core;
using SiYangUnityEventSystem.Demo;
using UnityEngine;

public class SiYangUnityEventSystemDemoManager : MonoBehaviour
{
    public void SendEventTest()
    {
        SiYangEventBus.Global.Publish(new TestEvent(1,"Test1"));
        SiYangEventBus.Global.Publish(new TestEvent(2,"Test2"));
    }
}
