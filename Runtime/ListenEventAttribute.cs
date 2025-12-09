using System;

namespace SiYangUnityEventSystem.Core
{
    /// <summary>
    /// 标记这是一个事件监听方法。
    /// 方法要求：
    /// - 必须是实例方法（非 static）
    /// - 必须有且只有 1 个参数，这个参数的类型就是事件类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ListenEventAttribute : Attribute
    {
        /// <summary>
        /// 优先级，越大越先执行。
        /// </summary>
        public int Priority { get; }

        public ListenEventAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }
}