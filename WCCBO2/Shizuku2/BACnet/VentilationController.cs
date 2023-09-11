using BaCSharp;
using System.IO.BACnet;

namespace Shizuku2.BACnet
{
  internal class VentilationController : IBACnetController
  {

    //BACnet Object IDは以下のルールで付与
    //テナント全体に関する情報はMemberNumber
    //ゾーンごとの情報は
    //1000*室外機番号 + 100*室内機番号 + Member Number

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DEVICE_ID = 6;

    /// <summary>排他的ポート番号</summary>
    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    /// <summary>Deviceの名称</summary>
    const string DEVICE_NAME = "Ventilation system controller";

    /// <summary>Deviceの説明</summary>
    const string DEVICE_DESCRIPTION = "BACnet device cotrolling ventilation system.";

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    public enum MemberNumber
    {
      /// <summary>南側CO2濃度</summary>
      SouthCO2Level = 1,
      /// <summary>北側CO2濃度</summary>
      NorthCO2Level = 2,
      /// <summary>全熱交換器On/Off</summary>
      HexOnOff = 3,
      /// <summary>全熱交換器バイパス有効無効</summary>
      HexBypassEnabled = 4,
      /// <summary>全熱交換器ファン風量</summary>
      HexFanSpeed = 5
    }

    #endregion

    #region インスタンス変数・プロパティ

    private readonly VentilationSystem ventSystem;

    /// <summary>BACnet通信用オブジェクト</summary>
    private BACnetCommunicator communicator;

    #endregion

    #region コンストラクタ

    public VentilationController(VentilationSystem ventSystem)
    {
      this.ventSystem = ventSystem;

      communicator = new BACnetCommunicator
        (makeDeviceObject(), EXCLUSIVE_PORT);
    }

    /// <summary>BACnet Deviceを作成する</summary>
    private DeviceObject makeDeviceObject()
    {
      DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

      //南側CO2濃度
      dObject.AddBacnetObject(new AnalogInput<uint>
        ((int)MemberNumber.SouthCO2Level,
        "CO2 level of south tenant",
        "CO2 level of south tenant.", 400, BacnetUnitsId.UNITS_PARTS_PER_MILLION));

      //北側CO2濃度
      dObject.AddBacnetObject(new AnalogInput<uint>
        ((int)MemberNumber.NorthCO2Level,
        "CO2 level of north tenant",
        "CO2 level of north tenant.", 400, BacnetUnitsId.UNITS_PARTS_PER_MILLION));

      for (int ouIndx = 0; ouIndx < ventSystem.HeatExchangers.Length; ouIndx++)
      {
        for (int iuIndx = 0; iuIndx < ventSystem.HeatExchangers[ouIndx].Length; iuIndx++)
        {
          //全熱交換器ごとの情報
          int bBase = 1000 * (1 + ouIndx) + 100 * (1 + iuIndx);
          string hexName = "HEX" + (1 + ouIndx) + "-" + (1 + iuIndx);

          //On/Off情報
          dObject.AddBacnetObject(new BinaryOutput
            ((int)(bBase + MemberNumber.HexOnOff),
            "On/Off setting/state (" + hexName + ")",
            "This object is used to control or monitor On/Off state of " + hexName, false));

          //バイパス制御有効無効
          dObject.AddBacnetObject(new BinaryOutput
            ((int)(bBase + MemberNumber.HexBypassEnabled),
            "Bypass control setting/state (" + hexName + ")",
            "This object is used to control or monitor bypass control state of " + hexName, false));

          //相対湿度
          dObject.AddBacnetObject(new MultiStateOutput
           ((int)(bBase + MemberNumber.HexFanSpeed),
           "Fan speed (" + hexName + ")",
           "This object is used to control or monitor fan speed of " + hexName + ". 1:Low, 2:Middle, 3:High", 3, 3));
        }
      }

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

    public void ApplyManipulatedVariables(DateTime dTime)
    {
      for (int ouIndx = 0; ouIndx < ventSystem.HeatExchangers.Length; ouIndx++)
      {
        for (int iuIndx = 0; iuIndx < ventSystem.HeatExchangers[ouIndx].Length; iuIndx++)
        {
          BacnetObjectId boID;
          //全熱交換器ごとの情報
          int bBase = 1000 * (1 + ouIndx) + 100 * (1 + iuIndx);

          //On/off******************
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + MemberNumber.HexOnOff));
          bool isOn = BACnetCommunicator.ConvertToBool(((BinaryOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
          if (!isOn)
            ventSystem.SetFanSpeed((uint)ouIndx, (uint)iuIndx, VentilationSystem.FanSpeed.Off);

          //バイパス制御******************
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + MemberNumber.HexBypassEnabled));
          bool bpEnabled = BACnetCommunicator.ConvertToBool(((BinaryOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE);
          if (bpEnabled)
            ventSystem.EnableBypassControl((uint)ouIndx, (uint)iuIndx);
          else
            ventSystem.DisableBypassControl((uint)ouIndx, (uint)iuIndx);

          //風量***********************
          //1:弱, 2:中 ,3:強
          if (isOn) //Offの場合にはそもそも有効ではない
          {
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + MemberNumber.HexFanSpeed));
            uint fanSpeed = ((MultiStateOutput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE;
            switch (fanSpeed)
            {
              case 1:
                ventSystem.SetFanSpeed((uint)ouIndx, (uint)iuIndx, VentilationSystem.FanSpeed.Low);
                break;
              case 2:
                ventSystem.SetFanSpeed((uint)ouIndx, (uint)iuIndx, VentilationSystem.FanSpeed.Middle);
                break;
              case 3:
                ventSystem.SetFanSpeed((uint)ouIndx, (uint)iuIndx, VentilationSystem.FanSpeed.High);
                break;
            }
          }
        }
      }
    }

    public void EndService()
    {
      communicator.EndService();
    }

    public void ReadMeasuredValues(DateTime dTime)
    {
      BacnetObjectId boID;

      //南側テナントCO2濃度
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.SouthCO2Level);
      ((AnalogInput<uint>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (uint)ventSystem.CO2Level_SouthTenant;

      //北側テナントCO2濃度
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.NorthCO2Level);
      ((AnalogInput<uint>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (uint)ventSystem.CO2Level_NorthTenant;
    }

    public void StartService()
    {
      communicator.StartService();
    }

    #endregion

  }
}
