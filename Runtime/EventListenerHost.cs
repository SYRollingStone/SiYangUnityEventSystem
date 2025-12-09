using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SiYangUnityEventSystem.Core.HostModel
{
    /// <summary>
    /// 挂在 GameObject 上的“事件监听宿主”组件。
    /// 会扫描同一 GameObject 上所有实现了 IAutoEventListener 的组件，
    /// 找出带 [ListenEvent] 的方法并自动订阅/取消订阅。
    /// </summary>
    [DisallowMultipleComponent]
    public class EventListenerHost : MonoBehaviour
    {
        // 避免多次查找
        private static readonly MethodInfo s_SubscribeMethodDefinition =
            typeof(IEventBus)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == "Subscribe" && m.IsGenericMethodDefinition);
        
        /// <summary>
        /// Type -> 该类型上所有 [ListenEvent] 方法缓存。
        /// </summary>
        private static readonly Dictionary<Type, HandlerInfo[]> s_HandlerCache =
            new Dictionary<Type, HandlerInfo[]>(32);

        /// <summary>
        /// 当前 Host 下所有订阅记录。
        /// </summary>
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(8);

        /// <summary>
        /// 一条监听配置：方法 + 事件类型 + 优先级。
        /// </summary>
        private struct HandlerInfo
        {
            public readonly MethodInfo Method;
            public readonly Type EventType;
            public readonly int Priority;

            public HandlerInfo(MethodInfo method, Type eventType, int priority)
            {
                Method = method;
                EventType = eventType;
                Priority = priority;
            }
        }

        [Header("Auto Register / Unregister")]
        [SerializeField] private bool _registerOnEnable = true;
        [SerializeField] private bool _unregisterOnDisable = true;

        /// <summary>
        /// 要使用的 EventBus。
        /// 默认是全局总线，你也可以在子类中 override 使用其他 Bus。
        /// </summary>
        protected virtual IEventBus EventBus => SiYangEventBus.Global;

        private void OnEnable()
        {
            if (_registerOnEnable)
            {
                RegisterAllListenersOnGameObject();
            }
        }

        private void OnDisable()
        {
            if (_unregisterOnDisable)
            {
                UnregisterAll();
            }
        }

        /// <summary>
        /// 手动触发注册（例如你不想在 OnEnable 时自动注册）。
        /// </summary>
        public void RegisterAllListenersOnGameObject()
        {
            UnregisterAll(); // 先清一遍，避免重复注册

            var bus = EventBus;
            if (bus == null)
            {
                Debug.LogError($"[EventListenerHost] {name} 的 EventBus 为 null，无法注册事件。");
                return;
            }

            // 找到同一 GameObject 上所有实现了 IAutoEventListener 的组件
            var components = GetComponents<MonoBehaviour>();

            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null) continue;

                if (comp is IAutoEventListener)
                {
                    RegisterListener(bus, comp);
                }
            }
        }

        /// <summary>
        /// 清除当前 Host 的所有订阅。
        /// </summary>
        public void UnregisterAll()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                var d = _subscriptions[i];
                if (d != null)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EventListenerHost] 取消订阅时出现异常：{ex}");
                    }
                }
            }

            _subscriptions.Clear();
        }

        /// <summary>
        /// 给单个 listener 组件注册所有 [ListenEvent] 方法。
        /// </summary>
        private void RegisterListener(IEventBus bus, MonoBehaviour listener)
        {
            var type = listener.GetType();
            if (!s_HandlerCache.TryGetValue(type, out var handlers))
            {
                handlers = BuildHandlerCache(type);
                s_HandlerCache.Add(type, handlers);
            }

            for (int i = 0; i < handlers.Length; i++)
            {
                var h = handlers[i];

                //Debug.Log($"[Host] listener={listener.GetType().FullName} eventType={h.EventType.AssemblyQualifiedName}");
                
                var actionType = typeof(Action<>).MakeGenericType(h.EventType);
                Delegate del;
                try
                {
                    del = Delegate.CreateDelegate(actionType, listener, h.Method);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventListenerHost] 在 {type.Name}.{h.Method.Name} 创建委托失败：{ex}");
                    continue;
                }

                MethodInfo genericSubscribe;
                try
                {
                    genericSubscribe = s_SubscribeMethodDefinition.MakeGenericMethod(h.EventType);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventListenerHost] 为事件类型 {h.EventType.Name} 构建 Subscribe<> 泛型方法失败：{ex}");
                    continue;
                }

                try
                {
                    var disposable = (IDisposable)genericSubscribe.Invoke(
                        bus,
                        new object[] { del, listener, h.Priority });

                    if (disposable != null)
                        _subscriptions.Add(disposable);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[EventListenerHost] 在 {type.Name}.{h.Method.Name} 订阅事件 {h.EventType.Name} 失败：{ex}");
                }
            }
        }

        /// <summary>
        /// 构建某个类型的 [ListenEvent] 方法缓存。
        /// 只在第一次遇到这个类型时调用一次。
        /// </summary>
        private static HandlerInfo[] BuildHandlerCache(Type type)
        {
            var methods = type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            var tempList = new List<HandlerInfo>(4);

            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];

                // 是否有 [ListenEvent] 标记
                var attrs = m.GetCustomAttributes(typeof(ListenEventAttribute), true);
                if (attrs == null || attrs.Length == 0)
                    continue;

                var attr = (ListenEventAttribute)attrs[0];

                // 参数检查：必须有且只有 1 个参数
                var ps = m.GetParameters();
                if (ps.Length != 1)
                {
                    Debug.LogWarning(
                        $"[EventListenerHost] {type.Name}.{m.Name} 使用了 [ListenEvent]，" +
                        $"但参数数量为 {ps.Length}，必须恰好为 1。已忽略该方法。");
                    continue;
                }

                var evtType = ps[0].ParameterType;

                if (evtType.IsByRef)
                {
                    Debug.LogWarning(
                        $"[EventListenerHost] {type.Name}.{m.Name} 的参数为 ref/out，暂不支持。已忽略该方法。");
                    continue;
                }

                tempList.Add(new HandlerInfo(m, evtType, attr.Priority));
            }

            if (tempList.Count == 0)
                return Array.Empty<HandlerInfo>();

            return tempList.ToArray();
        }
    }
}
