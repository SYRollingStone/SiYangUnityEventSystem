namespace SiYangUnityEventSystem.Demo
{
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
}