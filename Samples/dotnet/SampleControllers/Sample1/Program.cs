using Shizuku2.BACnet;

namespace Sample1
{
  internal class Program
  {
    static void Main(string[] args)
    {
      PresentValueReadWriter pvrw = new PresentValueReadWriter(10);
      pvrw.StartService();

      Console.Write("Subscribe COV...");
      while (!pvrw.SubscribeDateTimeCOV())
        Thread.Sleep(100);
      Console.WriteLine("success");

      while (true)
      {
        DateTime dt = pvrw.CurrentDateTime;
        Console.WriteLine(dt.ToString("yyyy/MM/dd HH:mm:ss"));
        Thread.Sleep(1000);
      }
    }
  }
}