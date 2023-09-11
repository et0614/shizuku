using BaCSharp;
using System.IO.BACnet;

namespace Shizuku2.BACnet
{
  internal class DummyDevice : IBACnetController
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DEVICE_ID = 9;

    /// <summary>排他的ポート番号</summary>
    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    const string DEVICE_NAME = "Dummy device";

    const string DEVICE_DESCRIPTION = "Dummy device to test BACnet communication.";

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    private BACnetCommunicator communicator;

    #endregion

    #region 列挙型

    public enum MemberNumber
    {
      AnalogValueInt = 1,
      AnalogOutputInt = 2,
      AnalogInputInt = 3,
      AnalogValueReal = 4,
      AnalogOutputReal = 5,
      AnalogInputReal = 6,
      BinaryValue = 7,
      BinaryOutput = 8,
      BinaryInput = 9,
      MultiStateValue = 10,
      MultiStateOutput = 11,
      MultiStateInput = 12,
      DateTime = 13
    }

    #endregion

    #region コンストラクタ

    /// <summary>BACnet通信テストのためのダミーDevice</summary>
    public DummyDevice()
    {
      communicator = new BACnetCommunicator
        (makeDeviceObject(), EXCLUSIVE_PORT, true);
    }

    /// <summary>BACnet Deviceを作成する</summary>
    private DeviceObject makeDeviceObject()
    {
      DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

      dObject.AddBacnetObject(new AnalogValue<int>
        ((int)MemberNumber.AnalogValueInt,
        "Analog value (int)",
        "Dummy object to test communication of analog value (int).", 1, BacnetUnitsId.UNITS_NO_UNITS, false));

      dObject.AddBacnetObject(new AnalogOutput<int>
       ((int)MemberNumber.AnalogOutputInt,
       "Analog output (int)",
       "Dummy object to test communication of analog output (int).", 2, BacnetUnitsId.UNITS_NO_UNITS));

      dObject.AddBacnetObject(new AnalogInput<int>
       ((int)MemberNumber.AnalogInputInt,
       "Analog input (int)",
       "Dummy object to test communication of analog input (int).", 3, BacnetUnitsId.UNITS_NO_UNITS));

      dObject.AddBacnetObject(new AnalogValue<float>
        ((int)MemberNumber.AnalogValueReal,
        "Analog value (float)",
        "Dummy object to test communication of analog value (real).", 4f, BacnetUnitsId.UNITS_NO_UNITS, false));

      dObject.AddBacnetObject(new AnalogOutput<float>
       ((int)MemberNumber.AnalogOutputReal,
       "Analog output (float)",
       "Dummy object to test communication of analog output (real).", 5f, BacnetUnitsId.UNITS_NO_UNITS));

      dObject.AddBacnetObject(new AnalogInput<float>
       ((int)MemberNumber.AnalogInputReal,
       "Analog input (float)",
       "Dummy object to test communication of analog input (real).", 6f, BacnetUnitsId.UNITS_NO_UNITS));

      dObject.AddBacnetObject(new BinaryValue
       ((int)MemberNumber.BinaryValue,
       "Binary value",
       "Dummy object to test communication of binary value.", false, false));

      dObject.AddBacnetObject(new BinaryOutput
      ((int)MemberNumber.BinaryOutput,
      "Binary output",
      "Dummy object to test communication of binary output.", false));

      dObject.AddBacnetObject(new BinaryInput
       ((int)MemberNumber.BinaryInput,
       "Binary input",
       "Dummy object to test communication of binary input.", false));

      dObject.AddBacnetObject(new MultiStateValue
       ((int)MemberNumber.MultiStateValue,
       "Multistate value",
       "Dummy object to test communication of multistate value.", 1u, 5, false));

      dObject.AddBacnetObject(new MultiStateOutput
       ((int)MemberNumber.MultiStateOutput,
       "Multistate output",
       "Dummy object to test communication of multistate output.", 2u, 5));

      dObject.AddBacnetObject(new MultiStateInput
       ((int)MemberNumber.MultiStateInput,
       "Multistate input",
       "Dummy object to test communication of multistate input.", 5, 3u, false));

      BacnetDateTime dTime1 = new BacnetDateTime(
        (int)MemberNumber.DateTime,
        "BACnet date time",
        "Dummy object to test communication of bacnet date time.");
      dTime1.m_PresentValue = new DateTime(1980, 6, 14, 0, 0, 0);
      dObject.AddBacnetObject(dTime1);

      return dObject;
    }

    #endregion

    #region インスタンスメソッド

    public void OutputBACnetObjectInfo
      (out uint[] instances, out string[] types, out string[] names, out string[] descriptions, out string[] values)
    {
      List<string> tLst = new List<string>();
      List<uint> iLst = new List<uint>();
      List<string> nLst = new List<string>();
      List<string> dLst = new List<string>();
      List<string> vLst = new List<string>();
      foreach (BaCSharpObject bObj in communicator.BACnetDevice.ObjectsList)
      {
        tLst.Add(bObj.PROP_OBJECT_IDENTIFIER.type.ToString().Substring(7));
        iLst.Add(bObj.PROP_OBJECT_IDENTIFIER.instance);
        nLst.Add(bObj.PROP_OBJECT_NAME);
        dLst.Add(bObj.PROP_DESCRIPTION);
        IList<BacnetValue> bVal = bObj.FindPropValue("PROP_PRESENT_VALUE");
        if (bVal != null) vLst.Add(bVal[0].Value.ToString());
        else vLst.Add(null);
      }
      types = tLst.ToArray();
      instances = iLst.ToArray();
      names = nLst.ToArray();
      descriptions = dLst.ToArray();
      values = vLst.ToArray();
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime) { }

    public void EndService()
    {
      communicator.EndService();
    }

    public void ReadMeasuredValues(DateTime dTime) { }

    public void StartService()
    {
      communicator.StartService();
    }

    #endregion

  }
}
