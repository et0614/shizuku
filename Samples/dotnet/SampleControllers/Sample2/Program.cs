using Shizuku2.BACnet;

namespace Sample2
{
  internal class Program
  {
    static void Main(string[] args)
    {
      EnvironmentCommunicator eCom = new EnvironmentCommunicator(14);
      eCom.StartService();

      while (true)
      {
        bool succeeded;

        Console.Write("Reading outdoor air temperature...");
        double dbt = eCom.GetDrybulbTemperature(out succeeded);
        Console.WriteLine(succeeded ? dbt.ToString("F1") : "failed");

        Console.Write("Reading outdoor air relative humidity...");
        double hmd = eCom.GetRelativeHumidity(out succeeded);
        Console.WriteLine(succeeded ? hmd.ToString("F1") : "failed");

        Console.Write("Reading global horizontal radiation...");
        double rad = eCom.GetGlobalHorizontalRadiation(out succeeded);
        Console.WriteLine(succeeded ? rad.ToString("F1") : "failed");

        Console.Write("Reading drybulb temperature of zone at VRF2-4...");
        double dbtZn = eCom.GetZoneDrybulbTemperature(2, 4, out succeeded);
        Console.WriteLine(succeeded ? dbtZn.ToString("F1") : "failed");

        Console.Write("Reading relative humidity of zone at VRF2-4...");
        double hmdZn = eCom.GetZoneRelativeHumidity(2, 4, out succeeded);
        Console.WriteLine(succeeded ? hmdZn.ToString("F1") : "failed");

        Console.WriteLine();
        Thread.Sleep(1000);
      }
    }
  }
}