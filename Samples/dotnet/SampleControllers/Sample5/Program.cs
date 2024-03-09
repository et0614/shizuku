using Shizuku2.BACnet;

namespace Sample5
{
  internal class Program
  {
    static void Main(string[] args)
    {
      VRFSystemCommunicator vCom = new VRFSystemCommunicator(12);
      vCom.StartService();

      while (true)
      {
        bool succeeded;

        Console.Write("Reading return air temperature of VRF1-2...");
        double dbt = vCom.GetReturnAirTemperature(1, 2, out succeeded);
        Console.WriteLine(succeeded ? dbt.ToString("F1") : "failed");

        Console.Write("Reading return air relative humidity of VRF1-2...");
        double hmd = vCom.GetReturnAirRelativeHumidity(1, 2, out succeeded);
        Console.WriteLine(succeeded ? hmd.ToString("F1") : "failed");

        Console.Write("Turning on VRF1-2...");
        vCom.TurnOn(1, 2, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Turning off VRF1-2...");
        vCom.TurnOff(1, 2, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Changing mode of VRF1-2 to cooling...");
        vCom.ChangeMode(1, 2, VRFSystemCommunicator.Mode.Cooling, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Changing set point temperature of VRF1-2 to 26C...");
        vCom.ChangeSetpointTemperature(1, 2, 26, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Changing fan speed of VRF1-2 to high...");
        vCom.ChangeFanSpeed(1, 2, VRFSystemCommunicator.FanSpeed.High, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Changing direction of VRF1-2 to 45degree...");
        vCom.ChangeDirection(1, 2, VRFSystemCommunicator.Direction.Degree_450, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Permitting local control of VRF1-2...");
        vCom.PermitLocalControl(1,2,out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Prohibiting local control of VRF1-2...");
        vCom.ProhibitLocalControl(1,2,out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.Write("Enable Refrigerant temp. control of VRF1...");
        vCom.EnableRefrigerantTemperatureControl(1, out succeeded);
        Console.WriteLine(succeeded ? "success" : "failed");

        Console.WriteLine();
        Thread.Sleep(1000);
      }
    }
  }
}