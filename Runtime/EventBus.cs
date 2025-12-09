using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiYangUnityEventSystem.Core
{
    /// <summary>
    /// 事件总线接口。
    /// 使用泛型事件类型，避免装箱和字符串 ID。
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// 订阅某个事件类型。
        /// </summary>
        /// <typeparam name="T">事件类型（建议 readonly struct）</typeparam>
        /// <param name="handler">处理函数</param>
        /// <param name="owner">
        /// 所属对象（可选）：
        /// - 传入 MonoBehaviour/ScriptableObject 时，可以在其被销毁后自动不再触发。
        /// - 传 null 则只在 Dispose 时停止。
        /// </param>
        /// <param name="priority">优先级，越大越先执行（默认 0）</param>
        /// <returns>返回 IDisposable，用于手动取消订阅</returns>
        IDisposable Subscribe<T>(Action<T> handler, object owner = null, int priority = 0);

        /// <summary>
        /// 发布事件。
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="evt">事件实例</param>
        void Publish<T>(T evt);

        /// <summary>
        /// 清除所有订阅。
        /// 通常只在切场景或重置系统时使用。
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// 默认事件总线实现。
    /// 设计目标：在 Publish 路径上 0GC。
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        /// <summary>
        /// Type -> SubscriptionList
        /// 不使用字符串，事件类型即 Topic。
        /// </summary>
        private readonly Dictionary<Type, ISubscriptionList> _subscriptions = new(32);

        /// <summary>
        /// 多线程安全锁，如果你只在主线程用，也可以去掉锁提高一点性能。
        /// </summary>
        private readonly object _lock = new();

        /// <inheritdoc />
        /// 订阅
        public IDisposable Subscribe<T>(Action<T> handler, object owner = null, int priority = 0)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                SubscriptionList<T> list;
                if (!_subscriptions.TryGetValue(typeof(T), out var boxedList))
                {
                    list = new SubscriptionList<T>();
                    _subscriptions.Add(typeof(T), list);
                }
                else
                {
                    list = (SubscriptionList<T>)boxedList;
                }

                return list.Add(handler, owner, priority);
            }
        }

        /// <inheritdoc />
        /// 发布事件
        public void Publish<T>(T evt)
        {
            SubscriptionList<T> list;

            // 从字典取一次引用，避免在锁内执行用户回调
            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(typeof(T), out var boxedList))
                {
                    return; // 没有任何订阅
                }

                list = (SubscriptionList<T>)boxedList;
            }

            // 真正执行回调在锁外，避免回调里再 Subscribe/Dispose 导致死锁
            list.Invoke(evt);
        }

        /// <inheritdoc />
        public void Clear()
        {
            lock (_lock)
            {
                _subscriptions.Clear();
            }
        }

        // -------------------- 内部类型 --------------------

        /// <summary>
        /// 只是一个标记接口，用于在 Dictionary 中存放不同泛型的 SubscriptionList。
        /// </summary>
        private interface ISubscriptionList
        {
        }

        /// <summary>
        /// 某个具体事件类型 T 的订阅列表。
        /// </summary>
        private sealed class SubscriptionList<T> : ISubscriptionList
        {
            // 所有订阅此事件类型的订阅者
            // 为避免 GC，整个生命周期只扩容，不缩容，不使用 LINQ/foreach
            private readonly List<EventSubscription<T>> _list = new(32);

            /// <summary>
            /// 添加一个订阅。
            /// </summary>
            public IDisposable Add(Action<T> handler, object owner, int priority)
            {
                var sub = new EventSubscription<T>(this, handler, owner, priority);
                _list.Add(sub);

                // 按 priority 降序插入排序（避免每次都重排整个列表）
                int i = _list.Count - 1;
                while (i > 0 && _list[i].Priority > _list[i - 1].Priority)
                {
                    (_list[i - 1], _list[i]) = (_list[i], _list[i - 1]);
                    i--;
                }

                return sub;
            }

            /// <summary>
            /// 触发所有订阅回调。
            /// 这里是高频路径，严格控制 0GC。
            /// </summary>
            public void Invoke(T evt)
            {
                var list = _list;
                int i = 0;

                while (i < list.Count)
                {
                    var sub = list[i];

                    if (!sub.IsAlive)
                    {
                        // 失效订阅直接移除（例如 owner 已经 Destroy，或者手动 Dispose）
                        list.RemoveAt(i);
                        continue; // 不自增 i
                    }

                    try
                    {
                        sub.Invoke(evt);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    i++;
                }
            }

            /// <summary>
            /// 由订阅对象在 Dispose 时调用，立即从列表移除自己。
            /// </summary>
            /// <param name="sub">要移除的订阅对象</param>
            internal void Remove(EventSubscription<T> sub)
            {
                var list = _list;
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    if (ReferenceEquals(list[i], sub))
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 单个订阅对象。
        /// 通过 IDisposable.Dispose 取消订阅。
        /// </summary>
        private sealed class EventSubscription<T> : IDisposable
        {
            private readonly SubscriptionList<T> _ownerList;
            private readonly WeakReference _ownerRef; // 可以为 null
            private Action<T> _handler;

            /// <summary>
            /// 优先级值，越大越先执行。
            /// </summary>
            public int Priority { get; }

            /// <summary>
            /// 是否仍然有效。
            /// - handler 为 null 表示已经 Dispose。
            /// - ownerRef 不再存活（或 Unity 对象已 Destroy）也视为无效。
            /// </summary>
            public bool IsAlive
            {
                get
                {
                    if (_handler == null)
                        return false;

                    if (_ownerRef == null)
                        return true;

                    var target = _ownerRef.Target;
                    if (target == null)
                        return false;

                    if (target is UnityEngine.Object unityObj)
                        return unityObj != null;

                    return true;
                }
            }

            public EventSubscription(SubscriptionList<T> ownerList, Action<T> handler, object owner, int priority)
            {
                _ownerList = ownerList ?? throw new ArgumentNullException(nameof(ownerList));
                _handler = handler ?? throw new ArgumentNullException(nameof(handler));
                Priority = priority;

                if (owner != null)
                {
                    _ownerRef = new WeakReference(owner);
                }
            }

            /// <summary>
            /// 真正的事件调用。
            /// </summary>
            public void Invoke(T evt)
            {
                _handler?.Invoke(evt);
            }

            /// <summary>
            /// 取消订阅。
            /// </summary>
            public void Dispose()
            {
                // 先将 handler 置空，避免再次触发
                _handler = null;

                // 再从列表中移除自己
                _ownerList.Remove(this);
            }
        }
    }

    /// <summary>
    /// 提供一个全局静态事件总线。
    /// 大多数情况下你可以直接用 SiYangEventBus.Global。
    /// </summary>
    public static class SiYangEventBus
    {
        /// <summary>
        /// 全局事件总线实例。
        /// </summary>
        public static readonly IEventBus Global = new EventBus();
    }
}
