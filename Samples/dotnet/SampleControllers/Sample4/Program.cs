using Shizuku2.BACnet;

namespace Sample4
{
  internal class Program
  {
    static void Main(string[] args)
    {
      VentilationSystemCommunicator vCom = new VentilationSystemCommunicator(16);
      vCom.StartService();

      while (true)
      {
        bool succeeded;

        Console.Write("Reading CO2 level of south tenant...");
        double coS = vCom.GetSouthTenantCO2Level(out succeeded);
        Console.WriteLine(succeeded ? coS.ToString() : "failed");

        Console.Write("Reading CO2 level of north tenant...");
        double coN = vCom.GetNorthTenantCO2Level(out succeeded);
        Console.WriteLine(succeeded ? coN.ToString() : "failed");

        Console.Write("Turning on HEX1-1...");
        vCom.StartVentilation(1, 1, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Turning off HEX1-1...");
        vCom.StopVentilation(1, 1, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Reading fan speed of HEX1-1...");
        VentilationSystemCommunicator.FanSpeed fs = vCom.GetFanSpeed(1, 1, out succeeded);
        Console.WriteLine(succeeded ? fs.ToString() : "failed");

        Console.Write("Changing fan speed of HEX1-1 to Middle...");
        vCom.ChangeFanSpeed(1, 1, VentilationSystemCommunicator.FanSpeed.Middle, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.WriteLine();
        Thread.Sleep(1000);
      }
    }
  }
}