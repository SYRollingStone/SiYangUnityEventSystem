# SiYangUnityEventSystem
一个干净、方便、优雅的Unity事件系统。

<h1 id="iCUKD">简介</h1>

1. 事件系统的发布（Publish）过程 0GC，订阅阶段允许初始化 GC（反射扫描、创建订阅对象），没有装箱与拆箱，没有linq/Foreach，性能友好。

2. 在任意Class或MonoBehavior脚本中使用。

3. 事件的注册和取消注册和业务逻辑无关，不应该书写在业务类的代码中。所以提供了2种方式帮助在MonoBehavior的业务代码中不书写注册和取消注册，干净优雅。
4. 没有任何三方依赖。

<h1 id="JzLut">使用</h1>
<h2 id="lhQ7k">声明一个事件</h2>
事件名称就是struct的类名。

事件参数就是struct中的参数。

当然也可以用class而不是struct，但推荐用struct，不需要gc。

```csharp
public readonly struct TestEvent
{
    public readonly  int id;
    public readonly  string name;

    public TestEvent(int id, string name)
    {
        this.id = id;
        this.name = name;
    }
}
```

<h2 id="JiwFO">发送事件</h2>

```csharp
TestEvent testEvent = new TestEvent(1, "Test1");
SiYangEventBus.Global.Publish(testEvent);
```

<h2 id="UaftW">监听事件</h2>
<h3 id="taZnT">基础写法</h3>
这种是常规用法，就是在OnEnable和OnDisable中注册和取消注册监听。

有缺点，就是注册监听和取消注册监听都不是业务逻辑，占用类代码的书写空间，不好。

可以用在任意普通类或继承MonoBehaviour类中：

```csharp
public class SiYangEventListener3 : MonoBehaviour
    {
        private IDisposable _subscription;
        private void OnEnable()
        {
            // 在构造函数里订阅事件
            _subscription = SiYangEventBus.Global.Subscribe<TestEvent>(OnTestEvent);
        }
        
        /// <summary>
        /// 不再需要这个控制器时，手动取消订阅
        /// </summary>
        private void OnDisable()
        {
            _subscription?.Dispose();
        }
        
        private void OnTestEvent(TestEvent e)
        {
            // 收到事件，写你的逻辑
            Console.WriteLine($"[SiYangEventListener3] 收到 TestEvent: id={e.id}, name={e.name}");
        }
    }
```

<h3 id="bT3Cv">继承父类写法</h3>
EventListenerBehaviour类继承了MonoBehavior，需要使用事件系统的类直接继承EventListenerBehaviour。

事件监听方法被[ListenEvent]标记。

方法传入的参数(TestEvent e)代表监听了什么事件。

```csharp
    public class SiYangEventListener1 : EventListenerBehaviour
    {
        [ListenEvent]
        private void TestEventListener(TestEvent e)
        {
            Debug.Log($"[SiYangEventListener1]  id:{e.id} name:{e.name}");
        }
    }
```

<h3 id="sjrir">接口写法，更灵活</h3>

```csharp
    [RequireComponent(typeof(EventListenerHost))]
    public class SiYangEventListener2:MonoBehaviour, IAutoEventListener
    {
        [ListenEvent]
        private void TestEventListener(TestEvent e)
        {
            Debug.Log($"[SiYangEventListener2]  id:{e.id} name:{e.name}");
        }
    }
```

同时在挂在SiYangEventListener2脚本的物体上挂载EventListenerHost脚本。

EventListenerHost负责在OnEnable中收集同物体上的IAutoEventListener，然后获取标签方法，向EventBus注册事件。

也就是说将注册、取消注册的功能收敛到EventListenerHost中，无需在各个类中管理。

