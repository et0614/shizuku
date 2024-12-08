using Shizuku2.BACnet;

namespace DummyDeviceController
{
  internal class Program
  {

    static string emulatorIpAddress = "127.0.0.1";

    static DummyDeviceCommunicator communicator;// = new DummyDeviceCommunicator(999, "Dummy device controller");

    static void Main(string[] args)
    {
      //初期設定ファイル読み込み
      string sFile = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "setting.ini";
      if (File.Exists(sFile))
      {
        using (StreamReader sReader = new StreamReader(sFile))
        {
          string ln;
          while ((ln = sReader.ReadLine()) != null)
          {
            if (!ln.StartsWith("#") && ln != "")
            {
              ln = ln.Remove(ln.IndexOf(';'));
              string[] st = ln.Split('=');
              if (st[0] == "ipadd") emulatorIpAddress = st[1];
            }
          }
        }
      }
      else
      {
        Console.WriteLine("Can't find \"setting.ini\".");
        return;
      }
      Console.WriteLine("Use " + emulatorIpAddress + " as the IP address of the emulator.");

      communicator = new DummyDeviceCommunicator(999, "Dummy device controller", emulatorIpAddress);
      communicator.StartService();

      Console.WriteLine("Input command in \"read [Object type]\" or \"write [Object type] [Value]\"");
      Console.WriteLine("Object type: av(analog value), ao(analog output), ai(analog input)");
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
        /*case "avi":
          result = communicator.ReadAnalogValueInt(out suc);
          break;
        case "aoi":
          result = communicator.ReadAnalogOutputInt(out suc);
          break;
        case "aii":
          result = communicator.ReadAnalogInputInt(out suc);
          break;*/
        case "av":
          result = communicator.ReadAnalogValueReal(out suc);
          break;
        case "ao":
          result = communicator.ReadAnalogOutputReal(out suc);
          break;
        case "ai":
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
        /*case "avi":
          communicator.WriteAnalogValue(int.Parse(value), out suc);
          break;
        case "aoi":
          communicator.WriteAnalogOutput(int.Parse(value), out suc);
          break;
        case "aii":
          //Analog inputは書き込みできない
          break;*/
        case "av":
          communicator.WriteAnalogValue(float.Parse(value), out suc);
          break;
        case "ao":
          communicator.WriteAnalogOutput(float.Parse(value), out suc);
          break;
        case "ai":
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