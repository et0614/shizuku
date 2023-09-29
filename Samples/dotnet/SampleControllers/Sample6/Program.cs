using Shizuku2.BACnet;

namespace Sample6
{
  internal class Program
  {
    static void Main(string[] args)
    {
      VRFSystemCommunicator vrCom = new VRFSystemCommunicator(12);
      VentilationSystemCommunicator vsCom = new VentilationSystemCommunicator(16);
      vrCom.StartService();
      vsCom.StartService();

      // Enable CurrentDateTime property
      Console.Write("Subscribe COV...");
      while (!vrCom.SubscribeDateTimeCOV())
        Thread.Sleep(100);
      Console.WriteLine("success");

      // Number of indoor units in each VRF system
      int[] iUnitNum = new int[] { 5, 4, 5, 4 };

      DateTime lastDt = vrCom.CurrentDateTime;
      while (true)
      {
        DateTime dt = vrCom.CurrentDateTime;
        Console.WriteLine(dt.ToString("yyyy/MM/dd HH:mm:ss"));

        // Change mode, air flow direction, and set point temperature depends on season
        bool isSum = 5 <= dt.Month && dt.Month <= 10;
        VRFSystemCommunicator.Mode mode = VRFSystemCommunicator.Mode.Heating;
        VRFSystemCommunicator.Direction dir = VRFSystemCommunicator.Direction.Vertical;
        float sp = 22;
        if (isSum)
        {
          mode = VRFSystemCommunicator.Mode.Cooling;
          dir = VRFSystemCommunicator.Direction.Horizontal;
          sp = 26;
        }

        // When the HVAC changed to operating hours
        if (!isHVACTime(lastDt) && isHVACTime(dt))
        {
          for (int i = 0; i < iUnitNum.Length; i++)
          {
            for (int j = 0; j < iUnitNum[i]; j++)
            {
              bool succeeded;
              uint oIdx = (uint)(i + 1);
              uint iIdx = (uint)(j + 1);
              string vName = "VRF" + oIdx + "-" + iIdx;

              Console.Write("Turning on " + vName + "...");
              vrCom.TurnOn(oIdx, iIdx, out succeeded);
              Console.WriteLine(succeeded ? "success" : "failed");

              Console.Write("Turning on " + vName + "(Ventilation)...");
              vsCom.StartVentilation(oIdx, iIdx, out succeeded);
              Console.WriteLine(succeeded ? "success" : "failed");

              Console.Write("Changing mode of " + vName + " to " + mode + "...");
              vrCom.ChangeMode(oIdx, iIdx, mode, out succeeded);
              Console.WriteLine(succeeded ? "success" : "failed");

              Console.Write("Changing set point temperature of " + vName + " to " + sp + "C...");
              vrCom.ChangeSetpointTemperature(oIdx, iIdx, sp, out succeeded);
              Console.WriteLine(succeeded ? "success" : "failed");

              Console.Write("Changing fan speed of " + vName + " to Middle...");
              vrCom.ChangeFanSpeed(oIdx, iIdx, VRFSystemCommunicator.FanSpeed.Middle, out succeeded);
              Console.WriteLine(succeeded ? "success" : "failed");

              Console.Write("Changing air flow direction of " + vName + " to " + dir + "...");
              vrCom.ChangeDirection(oIdx, iIdx, dir, out succeeded);
              Console.WriteLine(succeeded ? "success" : "failed");
            }
          }
        }
        // When the HVAC changed to stop hours
        else if (isHVACTime(lastDt) && !isHVACTime(dt))
        {
          for (int i = 0; i < iUnitNum.Length; i++)
          {
            for (int j = 0; j < iUnitNum[i]; j++)
            {
              bool succeeded;
              uint oIdx = (uint)(i + 1);
              uint iIdx = (uint)(j + 1);
              string vName = "VRF" + oIdx + "-" + iIdx;

              Console.Write("Turning off " + vName + "...");
              vrCom.TurnOff(oIdx, iIdx, out succeeded);
              Console.WriteLine(succeeded ? "success" : "failed");

              Console.Write("Turning off " + vName + "(Ventilation)...");
              vsCom.StopVentilation(oIdx, iIdx, out succeeded);
              Console.WriteLine(succeeded ? "success" : "failed");
            }
          }
        }

        lastDt = dt;
        Thread.Sleep(500);
      }
    }

    static bool isHVACTime(DateTime dt)
    {
      bool isBusinessHour = 7 <= dt.Hour && dt.Hour <= 19;
      bool isWeekday = dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday;
      return isWeekday && isBusinessHour;
    }
  }
}