# SiYangUnityEventSystem
A **clean, elegant, zero-GC** event system (Event Bus) for Unity.

## ✨ Features

1. **Zero-GC event publishing**  
   - Subscribing may allocate (reflection & setup)  
   - Publishing allocates nothing  
   - No boxing/unboxing, no LINQ, no foreach  
   - Strongly typed event dispatch for high performance

2. **Usable in any C# class or MonoBehaviour**

3. **Subscription/Unsubscription decoupled from business logic**  
   - Extend EventListenerBehaviour  
   - Use IAutoEventListener + EventListenerHost  
   - Or manually Subscribe / Dispose

4. **No third‑party dependencies**

---

# 📦 Usage

## 1. Define an event

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

## 2. Publish an event

```csharp
var evt = new TestEvent(1, "Test1");
SiYangEventBus.Global.Publish(evt);
```

## 3. Listen for events

---

## 🟦 Option 1: Manual subscription

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

## 🟧 Option 2: Extend EventListenerBehaviour (recommended)

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

## 🟩 Option 3: Interface-based (most flexible)

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
<img width="363" height="438" alt="image" src="https://github.com/user-attachments/assets/d0bb0ac9-af19-4e78-819c-4549f2f564d9" />
# 🎯 Summary

SiYangUnityEventSystem aims for:

- **Minimal GC**
- **Clean architecture**
- **Elegant event usage**
- **Flexibility and extensibility**
