using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SiYangUnityEventSystem.Core
{
    /// <summary>
    /// 继承这个基类，就可以通过 [ListenEvent] 自动订阅事件，
    /// 而不用自己写 OnEnable / OnDisable。
    /// </summary>
    public abstract class EventListenerBehaviour : MonoBehaviour
    {
        /// <summary>
        /// 缓存每个类型有哪些 [ListenEvent] 方法，避免每次 OnEnable 都反射。
        /// Type -> handler 数组
        /// </summary>
        private static readonly Dictionary<Type, HandlerInfo[]> s_HandlerCache = new(32);
            
        // 避免多次查找
        private static readonly MethodInfo s_SubscribeMethodDefinition =
            typeof(IEventBus)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == "Subscribe" && m.IsGenericMethodDefinition);

        
        /// <summary>
        /// 当前实例的订阅，OnDisable 时全部 Dispose。
        /// </summary>
        private readonly List<IDisposable> _subscriptions = new(4);

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

        /// <summary>
        /// 要使用的 EventBus。
        /// 默认是全局总线，你也可以在子类里重写，换成别的 Bus。
        /// </summary>
        protected virtual IEventBus EventBus => SiYangEventBus.Global;

        /// <summary>
        /// 启用时自动注册所有 [ListenEvent] 方法。
        /// </summary>
        protected virtual void OnEnable()
        {
            RegisterEventHandlers();
        }

        /// <summary>
        /// 关闭时自动取消所有订阅。
        /// </summary>
        protected virtual void OnDisable()
        {
            UnregisterEventHandlers();
        }

        private void RegisterEventHandlers()
        {
            var type = GetType();

            // 1. 取缓存（或第一次构建）
            if (!s_HandlerCache.TryGetValue(type, out var handlers))
            {
                handlers = BuildHandlerCache(type);
                s_HandlerCache.Add(type, handlers);
            }

            if (handlers == null || handlers.Length == 0)
                return;

            // 2. 针对每个 HandlerInfo，构建委托并订阅
            var bus = EventBus;
            if (bus == null)
            {
                Debug.LogError($"[EventListenerBehaviour] {name} 的 EventBus 为 null，无法注册事件。");
                return;
            }

            for (int i = 0; i < handlers.Length; i++)
            {
                var h = handlers[i];

                // 1) 构造 Action<TEvent> 委托类型
                var actionType = typeof(Action<>).MakeGenericType(h.EventType);

                // 2) 创建委托实例
                Delegate del;
                try
                {
                    del = Delegate.CreateDelegate(actionType, this, h.Method);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventListenerBehaviour] 在 {type.Name}.{h.Method.Name} 创建委托失败：{ex}");
                    continue;
                }

                // 3) 从泛型定义生成具体的 Subscribe<事件类型>
                MethodInfo genericSubscribe = null;
                try
                {
                    genericSubscribe = s_SubscribeMethodDefinition.MakeGenericMethod(h.EventType);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventListenerBehaviour] 为事件类型 {h.EventType.Name} 构建 Subscribe<> 泛型方法失败：{ex}");
                    continue;
                }

                try
                {
                    var disposable = (IDisposable)genericSubscribe.Invoke(
                        bus,
                        new object[] { del, this, h.Priority });

                    if (disposable != null)
                        _subscriptions.Add(disposable);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventListenerBehaviour] 在 {type.Name}.{h.Method.Name} 订阅事件 {h.EventType.Name} 失败：{ex}");
                }
            }
        }

        private void UnregisterEventHandlers()
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
                        Debug.LogError($"[EventListenerBehaviour] 取消订阅时出现异常：{ex}");
                    }
                }
            }

            _subscriptions.Clear();
        }

        /// <summary>
        /// 构建某个类型的监听方法缓存。
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
                if (attrs.Length == 0)
                    continue;

                var attr = (ListenEventAttribute)attrs[0];

                // 参数检查：必须有且只有 1 个参数
                var ps = m.GetParameters();
                if (ps.Length != 1)
                {
                    Debug.LogWarning(
                        $"[EventListenerBehaviour] {type.Name}.{m.Name} 使用了 [ListenEvent]，" +
                        $"但参数数量为 {ps.Length}，必须恰好为 1。已忽略该方法。");
                    continue;
                }

                var evtType = ps[0].ParameterType;

                if (evtType.IsByRef)
                {
                    Debug.LogWarning(
                        $"[EventListenerBehaviour] {type.Name}.{m.Name} 的参数为 ref/out，暂不支持。已忽略该方法。");
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
