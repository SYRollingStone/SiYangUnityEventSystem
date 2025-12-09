# SiYangUnityEventSystem
一个 **干净、优雅、零 GC** 的 Unity 事件系统（Event Bus）。

## ✨ 特性（Features）

1. **事件发布（Publish）全程 0GC**  
   - 订阅阶段允许初始化开销（反射扫描、创建订阅对象）  
   - 运行时无装箱拆箱、无 LINQ、无 foreach  
   - 强类型事件分发，性能友好

2. **可在任意 Class 或 MonoBehaviour 中使用**

3. **注册/反注册代码与业务逻辑完全解耦**  
   提供三种监听方式：  
   - 继承 EventListenerBehaviour  
   - IAutoEventListener + EventListenerHost  
   - 手动 Subscribe / Dispose

4. **无三方依赖，纯 C# 实现**

---

# 📦 使用方法

## 1. 声明事件

```csharp
public readonly struct TestEvent
{
    public readonly int id;
    public readonly string name;

    public TestEvent(int id, string name)
    {
        this.id = id;
        this.name = name;
    }
}
```

## 2. 发送事件

```csharp
var evt = new TestEvent(1, "Test1");
SiYangEventBus.Global.Publish(evt);
```

## 3. 监听事件

---

## 🟦 方式一：基础写法（手动订阅/取消）

```csharp
public class SiYangEventListener3 : MonoBehaviour
{
    private IDisposable _subscription;

    private void OnEnable()
    {
        _subscription = SiYangEventBus.Global.Subscribe<TestEvent>(OnTestEvent);
    }

    private void OnDisable()
    {
        _subscription?.Dispose();
    }

    private void OnTestEvent(TestEvent e)
    {
        Debug.Log($"[SiYangEventListener3] id={e.id}, name={e.name}");
    }
}
```

---

## 🟧 方式二：继承 EventListenerBehaviour（推荐）

```csharp
public class SiYangEventListener1 : EventListenerBehaviour
{
    [ListenEvent]
    private void TestEventListener(TestEvent e)
    {
        Debug.Log($"[SiYangEventListener1] id={e.id}, name={e.name}");
    }
}
```

---

## 🟩 方式三：接口写法（最灵活）

```csharp
[RequireComponent(typeof(EventListenerHost))]
public class SiYangEventListener2 : MonoBehaviour, IAutoEventListener
{
    [ListenEvent]
    private void TestEventListener(TestEvent e)
    {
        Debug.Log($"[SiYangEventListener2] id={e.id}, name={e.name}");
    }
}
```

---
同物体上挂载脚本EventListenerHost

<img width="363" height="438" alt="image" src="https://github.com/user-attachments/assets/d0bb0ac9-af19-4e78-819c-4549f2f564d9" />

# 🎯 小结

SiYangUnityEventSystem 的目标是：

- **最小 GC 开销**
- **清晰的代码结构**
- **简单优雅的事件使用体验**
- **灵活的扩展方式**
