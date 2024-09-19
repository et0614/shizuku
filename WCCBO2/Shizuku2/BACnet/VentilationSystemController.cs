using BaCSharp;
using System.IO.BACnet;

using System.IO.BACnet.Storage;
using System.Reflection;

namespace Shizuku2.BACnet
{
  internal class VentilationSystemController : IBACnetController
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

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator;

    private readonly VentilationSystem ventSystem;

    #endregion

    #region コンストラクタ

    public VentilationSystemController(VentilationSystem ventSystem, string localEndpointIP)
    {
      this.ventSystem = ventSystem;

      Communicator = new BACnetCommunicator(makeStorage(), EXCLUSIVE_PORT, localEndpointIP);
    }

    private DeviceStorage makeStorage()
    {
      DeviceStorage strg = DeviceStorage.Load(
        new StreamReader
        (Assembly.GetExecutingAssembly().GetManifestResourceStream("Shizuku2.Resources.VentilationSystemControllerStorage.xml"))
        );

      for (int ouIndx = 0; ouIndx < ventSystem.HeatExchangers.Length; ouIndx++)
      {
        for (int iuIndx = 0; iuIndx < ventSystem.HeatExchangers[ouIndx].Length; iuIndx++)
        {
          //全熱交換器ごとの情報
          int bBase = 1000 * (1 + ouIndx) + 100 * (1 + iuIndx);
          string hexName = "HEX" + (1 + ouIndx) + "-" + (1 + iuIndx);

          //On/Off情報
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.HexOnOff),
            Type = BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to control or monitor On/Off state of " + hexName),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_BINARY_OUTPUT:" + (bBase + MemberNumber.HexOnOff)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "On/Off setting/state (" + hexName + ")"),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "4"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_POLARITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            }
          });

          //バイパス制御有効無効
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.HexBypassEnabled),
            Type = BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to control or monitor bypass control state of " + hexName),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_BINARY_OUTPUT:" + (bBase + MemberNumber.HexBypassEnabled)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Bypass control setting/state (" + hexName + ")"),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "4"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_POLARITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            }
          });

          //ファン風量
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.HexFanSpeed),
            Type = BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_MULTI_STATE_OUTPUT:" + (bBase + MemberNumber.HexFanSpeed)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Fan speed (" + hexName + ")"),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "14"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "3"),
              new Property(BacnetPropertyIds.PROP_STATE_TEXT, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ["Low", "Middle", "High"]),
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to control or monitor fan speed of " + hexName + ". 1:Low; 2:Middle; 3:High"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_NUMBER_OF_STATES, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "3"),
              new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            }
          });
        }
      }
      return strg;
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime)
    {
      for (int ouIndx = 0; ouIndx < ventSystem.HeatExchangers.Length; ouIndx++)
      {
        for (int iuIndx = 0; iuIndx < ventSystem.HeatExchangers[ouIndx].Length; iuIndx++)
        {
          //全熱交換器ごとの情報
          int bBase = 1000 * (1 + ouIndx) + 100 * (1 + iuIndx);

          //On/off******************
          bool isOn = 1u == (uint)Communicator.Storage.ReadPresentValue(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + MemberNumber.HexOnOff)));
          if (!isOn)
            ventSystem.SetFanSpeed((uint)ouIndx, (uint)iuIndx, VentilationSystem.FanSpeed.Off);

          //バイパス制御******************
          bool bpEnabled = 1u == (uint)Communicator.Storage.ReadPresentValue(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + MemberNumber.HexBypassEnabled)));
          if (bpEnabled)
            ventSystem.EnableBypassControl((uint)ouIndx, (uint)iuIndx);
          else
            ventSystem.DisableBypassControl((uint)ouIndx, (uint)iuIndx);

          //風量***********************
          //1:弱, 2:中 ,3:強
          if (isOn) //Offの場合にはそもそも有効ではない
          {
            uint fanSpeed = (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + MemberNumber.HexFanSpeed)));
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
      Communicator.EndService();
    }

    public void ReadMeasuredValues(DateTime dTime)
    {
      //南側テナントCO2濃度
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.SouthCO2Level),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)ventSystem.CO2Level_SouthTenant)
        );

      //北側テナントCO2濃度
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.NorthCO2Level),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)ventSystem.CO2Level_NorthTenant)
        );
    }

    public void StartService()
    {
      Communicator.StartService();
    }

    #endregion

  }
}
