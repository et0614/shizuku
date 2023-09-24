using Shizuku2.BACnet;

namespace DummyDeviceController
{
  internal class Program
  {

    static DummyDeviceCommunicator communicator = new DummyDeviceCommunicator(999, "Dummy device controller");

    static void Main(string[] args)
    {
      communicator.StartService();

      Console.WriteLine("Input command in \"read [Object type]\" or \"write [Object type] [Value]\"");
      Console.WriteLine("Object type: avi(analog value int), aoi(analog output int), aii(analog input int)");
      Console.WriteLine("             avr(analog value real), aoi(analog output real), aii(analog input real)");
      Console.WriteLine("             bv(binary value), bo(binary output), bi(binary input)");
      Console.WriteLine("             mv(multistate value), mo(multistate output), mi(multistate input)");
      Console.WriteLine("             dt(datetime)");
      Console.WriteLine();

      while (true)
      {
        Console.Write(">");
        string? cmd = Console.ReadLine();
        if (cmd != null)
        {
          string[] cmds = cmd.Split(' ');
          if (cmds.Length == 2 && cmds[0].ToLower() == "read")
            readProperty(cmds[1]);
          else if (cmds.Length == 3 && cmds[0].ToLower() == "write")
            writeProperty(cmds[1], cmds[2]);
        }
        Console.WriteLine();
      }
    }

    static void readProperty(string objType)
    {
      bool suc = false;
      object result = "";
      Console.Write("Reading present value... ");
      switch (objType)
      {
        case "avi":
          result = communicator.ReadAnalogValueInt(out suc);
          break;
        case "aoi":
          result = communicator.ReadAnalogOutputInt(out suc);
          break;
        case "aii":
          result = communicator.ReadAnalogInputInt(out suc);
          break;
        case "avr":
          result = communicator.ReadAnalogValueReal(out suc);
          break;
        case "aor":
          result = communicator.ReadAnalogOutputReal(out suc);
          break;
        case "air":
          result = communicator.ReadAnalogInputReal(out suc);
          break;
        case "bv":
          result = communicator.ReadBinaryValue(out suc);
          break;
        case "bo":
          result = communicator.ReadBinaryOutput(out suc);
          break;
        case "bi":
          result = communicator.ReadBinaryInput(out suc);
          break;
        case "mv":
          result = communicator.ReadMultiStateValue(out suc);
          break;
        case "mo":
          result = communicator.ReadMultiStateOutput(out suc);
          break;
        case "mi":
          result = communicator.ReadMultiStateInput(out suc);
          break;
        case "dt":
          result = communicator.ReadDateTime(out suc);
          break;
        default:
          Console.WriteLine("Undefined object type.");
          break;
      }
      if (suc)
        Console.WriteLine("success. Value = " + result.ToString());
      else
        Console.WriteLine("failed.");
    }

    static void writeProperty(string objType, string value)
    {
      bool suc = false;
      Console.Write("Writing present value... ");
      switch (objType)
      {
        case "avi":
          communicator.WriteAnalogValue(int.Parse(value), out suc);
          break;
        case "aoi":
          communicator.WriteAnalogOutput(int.Parse(value), out suc);
          break;
        case "aii":
          //Analog inputは書き込みできない
          break;
        case "avr":
          communicator.WriteAnalogValue(float.Parse(value), out suc);
          break;
        case "aor":
          communicator.WriteAnalogOutput(float.Parse(value), out suc);
          break;
        case "air":
          //Analog inputは書き込みできない
          break;
        case "bv":
          communicator.WriteBinaryValue(bool.Parse(value), out suc);
          break;
        case "bo":
          communicator.WriteBinaryOutput(bool.Parse(value), out suc);
          break;
        case "bi":
          //Binary inputは書き込みできない
          break;
        case "mv":
          communicator.WriteMultiStateValue(uint.Parse(value), out suc);
          break;
        case "mo":
          communicator.WriteMultiStateOutput(uint.Parse(value), out suc);
          break;
        case "mi":
          //MultiState inputは書き込みできない
          break;
        case "dt":
          //DateTimeは書き込みできない
          break;
        default:
          Console.WriteLine("Undefined object type.");
          break;
      }
      Console.WriteLine(suc ? "success." : "failed.");
    }

  }
}