using Shizuku2.BACnet;

namespace Sample3
{
  internal class Program
  {
    static void Main(string[] args)
    {
      OccupantCommunicator oCom = new OccupantCommunicator(15);
      oCom.StartService();

      while (true)
      {
        bool succeeded;

        Console.Write("Reading occupant number in north tenant......");
        int oNum = oCom.GetOccupantNumber(OccupantCommunicator.Tenant.North, out succeeded);
        Console.WriteLine(succeeded ? oNum.ToString() : "failed");

        Console.Write("Reading occupant number in south tenant zone-1...");
        int oNumZ = oCom.GetOccupantNumber(OccupantCommunicator.Tenant.North, 1, out succeeded);
        Console.WriteLine(succeeded ? oNumZ.ToString() : "failed");

        Console.Write("Reading averaged thermal sensation (south tenant zone-1)...");
        float aTS = oCom.GetAveragedThermalSensation(OccupantCommunicator.Tenant.North, 1, out succeeded);
        Console.WriteLine(succeeded ? aTS.ToString("F1") : "failed");

        Console.Write("Reading averaged clothing index (south tenant zone-1)...");
        float aCI = oCom.GetAveragedClothingIndex(OccupantCommunicator.Tenant.North, 1, out succeeded);
        Console.WriteLine(succeeded ? aCI.ToString("F1") : "failed");

        Console.Write("Is occupant No.1 in south tenant stay in office? ...");
        bool ocS = oCom.IsOccupantStayInOffice(OccupantCommunicator.Tenant.North, 1, out succeeded);
        Console.WriteLine(succeeded ? ocS.ToString() : "failed");

        Console.Write("Reading thermal sensation of occupant No.2 in south tenant...");
        OccupantCommunicator.ThermalSensation ts =
          oCom.GetThermalSensation(OccupantCommunicator.Tenant.South, 2, out succeeded);
        Console.WriteLine(succeeded ? ts.ToString() : "failed");

        Console.Write("Reading clothing index of occupant No.3 in south tenant...");
        float ci = oCom.GetClothingIndex(OccupantCommunicator.Tenant.North, 3, out succeeded);
        Console.WriteLine(succeeded ? ci.ToString("F2") : "failed");

        Console.WriteLine();
        Thread.Sleep(1000);
      }
    }
  }
}