namespace Shared.Helpers
{
    public static class TaskAwaiter
    {
        public static void Wait(int seconds)
        {
            Console.WriteLine($"Waiting for {seconds} seconds to wait for services to initialize.");
            Task.Delay(seconds * 1000).GetAwaiter().GetResult();
            Console.WriteLine("Wait ended");
        }
    }
}
