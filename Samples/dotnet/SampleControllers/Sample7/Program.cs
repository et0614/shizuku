using Shizuku2.BACnet;

namespace Sample7
{
  internal class Program
  {
    static void Main(string[] args)
    {
      VentilationSystemCommunicator vsCom = new VentilationSystemCommunicator(26);
      vsCom.StartService();

      // Enable CurrentDateTime property
      Console.Write("Subscribe COV...");
      while (!vsCom.SubscribeDateTimeCOV())
        Thread.Sleep(100);
      Console.WriteLine("success");

      // Number of indoor units in each VRF system
      int[] iUnitNum = new int[] { 5, 4, 5, 4 };

      while (true)
      {
        DateTime dt = vsCom.CurrentDateTime;
        Console.WriteLine(dt.ToString("yyyy/MM/dd HH:mm:ss"));

        // When the HVAC changed to operating hours
        if (isHVACTime(dt))
        {
          for (int i = 0; i < iUnitNum.Length; i++)
          {
            bool succeeded;
            uint southCO2 = (uint)vsCom.GetSouthTenantCO2Level(out succeeded);
            uint northCO2 = (uint)vsCom.GetNorthTenantCO2Level(out succeeded);

            VentilationSystemCommunicator.FanSpeed southFS = getFanSpeed(southCO2);
            VentilationSystemCommunicator.FanSpeed northFS = getFanSpeed(northCO2);

            Console.WriteLine("South tenant: " + southFS.ToString() + "(" + southCO2.ToString() + ")");
            Console.WriteLine("North tenant: " + northFS.ToString() + "(" + northCO2.ToString() + ")");

            for (int j = 0; j < iUnitNum[i]; j++)
            {
              VentilationSystemCommunicator.FanSpeed fs = (i == 0 || i == 1) ? southFS : northFS;
              vsCom.ChangeFanSpeed((uint)(i + 1), (uint)(j + 1), fs, out _);
            }
          }
        }

        Thread.Sleep(1000);
      }
    }

    static VentilationSystemCommunicator.FanSpeed getFanSpeed(uint co2Level)
    {
      if (co2Level < 600) return VentilationSystemCommunicator.FanSpeed.Low;
      else if (co2Level < 800) return VentilationSystemCommunicator.FanSpeed.Middle;
      else return VentilationSystemCommunicator.FanSpeed.High;
    }

    static bool isHVACTime(DateTime dt)
    {
      bool isBusinessHour = 7 <= dt.Hour && dt.Hour <= 19;
      bool isWeekday = dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday;
      return isWeekday && isBusinessHour;
    }
  }
}