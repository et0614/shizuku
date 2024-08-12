using BaCSharp;
using Popolo.HVAC.MultiplePackagedHeatPump;
using Popolo.ThermophysicalProperty;
using System.IO.BACnet;

using System.IO.BACnet.Storage;
using System.Reflection;

namespace Shizuku2.BACnet
{
  /// <summary>オリジナルVRFコントローラ</summary>
  public class VRFSystemController : IBACnetController
  {

    //BACnet Object IDは以下のルールで付与
    //1000*室外機番号 + 100*室内機番号 + Member Number
    //Ex. VRF 3-2 Setpoint_Setting(5)  ->  1000*3+100*2+5=3205
    //ただし蒸発・凝縮温度などシステム全体に関わる項目は室内機番号=0とする
    //Ex. VRF 3-2 CondensingTemperatureSetpoint_Status(19)  ->  1000*3+100*0+19=3019

    #region 定数宣言

    /// <summary>デバイスID</summary>
    const uint DEVICE_ID = 2;

    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    const int SIGNAL_UPDATE_SPAN = 60;

    /// <summary>大気圧[kPa]</summary>
    const double ATM = 101.325;

    #endregion

    #region 列挙型

    public enum MemberNumber
    {
      /// <summary>On/Offの設定</summary>
      OnOff_Setting = 1,
      /// <summary>On/Offの状態</summary>
      OnOff_Status = 2,
      /// <summary>運転モードの設定</summary>
      OperationMode_Setting = 3,
      /// <summary>運転モードの状態</summary>
      OperationMode_Status = 4,
      /// <summary>室温設定値の設定</summary>
      Setpoint_Setting = 5,
      /// <summary>室温設定値の状態</summary>
      Setpoint_Status = 6,
      /// <summary>還乾球温度</summary>
      MeasuredRoomTemperature = 7,
      /// <summary>還相対湿度</summary>
      MeasuredRelativeHumidity = 8,
      /// <summary>ファン風量の設定</summary>
      FanSpeed_Setting = 9,
      /// <summary>ファン風量の状態</summary>
      FanSpeed_Status = 10,
      /// <summary>風向の設定</summary>
      AirflowDirection_Setting = 11,
      /// <summary>風量の状態</summary>
      AirflowDirection_Status = 12,
      /// <summary>手元リモコン操作許可の設定</summary>
      RemoteControllerPermittion_Setpoint_Setting = 13,
      /// <summary>手元リモコン操作許可の状態</summary>
      RemoteControllerPermittion_Setpoint_Status = 14,
      /// <summary>冷媒温度強制制御の設定</summary>
      ForcedRefrigerantTemperature_Setting = 15,
      /// <summary>冷媒温度強制制御の状態</summary>
      ForcedRefrigerantTemperature_Status = 16,
      /// <summary>冷媒蒸発温度設定値の設定</summary>
      EvaporatingTemperatureSetpoint_Setting = 17,
      /// <summary>冷媒蒸発温度設定値の状態</summary>
      EvaporatingTemperatureSetpoint_Status = 18,
      /// <summary>冷媒凝縮温度設定値の設定</summary>
      CondensingTemperatureSetpoint_Setting = 19,
      /// <summary>冷媒凝縮温度設定値の状態</summary>
      CondensingTemperatureSetpoint_Status = 20,
      /// <summary>消費電力</summary>
      Electricity = 21,
      /// <summary>熱負荷</summary>
      HeatLoad = 22
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator;

    private readonly ExVRFSystem[] vrfSystems;

    private DateTime nextSignalApply = new DateTime(1980, 1, 1, 0, 0, 0);
    private DateTime nextSignalRead = new DateTime(1980, 1, 1, 0, 0, 0);

    #endregion

    #region コンストラクタ

    public VRFSystemController(ExVRFSystem[] vrfs)
    {
      vrfSystems = vrfs;

      Communicator = new BACnetCommunicator(makeStorage(), EXCLUSIVE_PORT);
    }

    private DeviceStorage makeStorage()
    {
      DeviceStorage strg = DeviceStorage.Load(
        new StreamReader
        (Assembly.GetExecutingAssembly().GetManifestResourceStream("Shizuku2.Resources.VRFSystemControllerStorage.xml"))
        );

      for (int ouIndx = 0; ouIndx < vrfSystems.Length; ouIndx++)
      {
        //室外機別の項目
        int bBase = 1000 * (1 + ouIndx);
        string vrfName = "VRF" + (1 + ouIndx);

        strg.AddObject(new System.IO.BACnet.Storage.Object()
        {
          Instance = (uint)(bBase + MemberNumber.ForcedRefrigerantTemperature_Setting),
          Type = BacnetObjectTypes.OBJECT_BINARY_VALUE,
          Properties = new Property[]
          {
            new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_BINARY_VALUE:" + (bBase + MemberNumber.ForcedRefrigerantTemperature_Setting)),
            new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "5"),
            new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "RefrigerantTempCtrlSetting_" + vrfName),
            new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to change the forced evaporating/condensing control of VRF system of " + vrfName),
            new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
            new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
            new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
          }
        });

        strg.AddObject(new System.IO.BACnet.Storage.Object()
        {
          Instance = (uint)(bBase + MemberNumber.ForcedRefrigerantTemperature_Status),
          Type = BacnetObjectTypes.OBJECT_BINARY_INPUT,
          Properties = new Property[]
          {
            new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_BINARY_INPUT:" + (bBase + MemberNumber.ForcedRefrigerantTemperature_Status)),
            new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "3"),
            new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "RefrigerantTempCtrlStatus_" + vrfName),
            new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the forced evaporating/condensing control of VRF system of " + vrfName),
            new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
            new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
            new Property(BacnetPropertyIds.PROP_POLARITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          }
        });

        strg.AddObject(new System.IO.BACnet.Storage.Object()
        {
          Instance = (uint)(bBase + MemberNumber.EvaporatingTemperatureSetpoint_Setting),
          Type = BacnetObjectTypes.OBJECT_ANALOG_VALUE,
          Properties = new Property[]
          {
            new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_VALUE:" + (bBase + MemberNumber.EvaporatingTemperatureSetpoint_Setting)),
            new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "2"),
            new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "EvpTempSetting_" + vrfName),
            new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "10"),
            new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to set the evaporating temperature of VRF system of " +  vrfName),
            new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
            new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
            new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"),
            new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
            new Property(BacnetPropertyIds.PROP_FAULT_HIGH_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "15"),
            new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "2"),
          }
        });

        strg.AddObject(new System.IO.BACnet.Storage.Object()
        {
          Instance = (uint)(bBase + MemberNumber.EvaporatingTemperatureSetpoint_Status),
          Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
          Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the evaporating temperature of VRF system of " + vrfName),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.EvaporatingTemperatureSetpoint_Status)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "EvpTempStatus_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "10"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"),
              new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "2"),
              new Property(BacnetPropertyIds.PROP_HIGH_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "15"),
            }
        });

        strg.AddObject(new System.IO.BACnet.Storage.Object()
        {
          Instance = (uint)(bBase + MemberNumber.CondensingTemperatureSetpoint_Setting),
          Type = BacnetObjectTypes.OBJECT_ANALOG_VALUE,
          Properties = new Property[]
          {
            new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_VALUE:" + (bBase + MemberNumber.CondensingTemperatureSetpoint_Setting)),
            new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "2"),
            new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "CndTempSetting_" + vrfName),
            new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "45"),
            new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to set the condensing temperature of VRF system of " +  vrfName),
            new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
            new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
            new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"),
            new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
            new Property(BacnetPropertyIds.PROP_HIGH_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "50"),
            new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "35"),
          }
        });

        strg.AddObject(new System.IO.BACnet.Storage.Object()
        {
          Instance = (uint)(bBase + MemberNumber.CondensingTemperatureSetpoint_Status),
          Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
          Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the condensing temperature of VRF system of " + vrfName),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.CondensingTemperatureSetpoint_Status)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "CndTempStatus_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "45"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"),
              new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "35"),
              new Property(BacnetPropertyIds.PROP_HIGH_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "50"),
            }
        });

        strg.AddObject(new System.IO.BACnet.Storage.Object()
        {
          Instance = (uint)(bBase + MemberNumber.Electricity),
          Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
          Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the outdoor unit's electric consumption (fans and compressors) of " + vrfName),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.Electricity)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Electricity_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "48"),
              new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
            }
        });

        strg.AddObject(new System.IO.BACnet.Storage.Object()
        {
          Instance = (uint)(bBase + MemberNumber.HeatLoad),
          Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
          Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the heat load of VRF system of " + vrfName),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.HeatLoad)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "HeatLoad_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "48"),
            }
        });

        for (int iuIndx = 0; iuIndx < vrfSystems[ouIndx].IndoorUnitModes.Length; iuIndx++)
        {
          //室内機ごとの情報
          bBase = 1000 * (1 + ouIndx) + 100 * (1 + iuIndx);
          vrfName = "VRF" + (1 + ouIndx) + "-" + (1 + iuIndx);

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.OnOff_Setting),
            Type = BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to start (On)/stop (Off) the indoor unit of " + vrfName),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_BINARY_OUTPUT:" + (bBase + MemberNumber.OnOff_Setting)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "OnOffCommand_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "4"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_POLARITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.OnOff_Status),
            Type = BacnetObjectTypes.OBJECT_BINARY_INPUT,
            Properties = new Property[]
          {
            new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_BINARY_INPUT:" + (bBase + MemberNumber.OnOff_Status)),
            new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "3"),
            new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "OnOffStatus_" + vrfName),
            new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the indoor unit's On/Off status of " + vrfName),
            new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
            new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
            new Property(BacnetPropertyIds.PROP_POLARITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.OperationMode_Setting),
            Type = BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_MULTI_STATE_OUTPUT:" + (bBase + MemberNumber.OperationMode_Setting)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "ModeCommand_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "14"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "3"),
              new Property(BacnetPropertyIds.PROP_STATE_TEXT, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ["Cool", "Heat", "Fan"]),
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to set an indoor unit's operation mode. 1: cool; 2: heat; 3: fan"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_NUMBER_OF_STATES, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "3"),
              new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.OperationMode_Status),
            Type = BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_MULTI_STATE_INPUT:" + (bBase + MemberNumber.OperationMode_Setting)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "ModeStatus_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "13"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "3"),
              new Property(BacnetPropertyIds.PROP_STATE_TEXT, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ["Cool", "Heat", "Fan"]),
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor an indoor unit's operation mode. 1: cool; 2: heat; 3: fan"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_NUMBER_OF_STATES, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "3"),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.Setpoint_Setting),
            Type = BacnetObjectTypes.OBJECT_ANALOG_VALUE,
            Properties = new Property[]
          {
            new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_VALUE:" + (bBase + MemberNumber.Setpoint_Setting)),
            new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "2"),
            new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "TempSPSetting_" + vrfName),
            new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "24"),
            new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to set the indoor unit's setpoint of " +  vrfName),
            new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
            new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
            new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"),
            new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
            new Property(BacnetPropertyIds.PROP_HIGH_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "32"),
            new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "16"),
          }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.Setpoint_Status),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the indoor unit's setpoint of " + vrfName),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.Setpoint_Status)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "TempSPStatus_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "24"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"),
            new Property(BacnetPropertyIds.PROP_HIGH_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "32"),
              new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "16"),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.MeasuredRoomTemperature),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the room dry-bulb temperature detected by the indoor unit return air sensor."),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.MeasuredRoomTemperature)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "RoomTemp_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "24"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.MeasuredRelativeHumidity),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the room relative humidity detected by the indoor unit return air sensor."),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.MeasuredRelativeHumidity)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "RoomRHmid_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "50"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "29"),
            new Property(BacnetPropertyIds.PROP_HIGH_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "100"),
              new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.FanSpeed_Setting),
            Type = BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_MULTI_STATE_OUTPUT:" + (bBase + MemberNumber.FanSpeed_Setting)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "AirFlowRateCommand_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "14"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "2"),
              new Property(BacnetPropertyIds.PROP_STATE_TEXT, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ["Low", "Middle", "High"]),
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to set an indoor unit's fan speed. 1: Low; 2: Middle; 3: High"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_NUMBER_OF_STATES, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "3"),
              new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.FanSpeed_Status),
            Type = BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_MULTI_STATE_INPUT:" + (bBase + MemberNumber.FanSpeed_Status)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "AirFlowRateStatus_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "13"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "2"),
              new Property(BacnetPropertyIds.PROP_STATE_TEXT, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ["Low", "Middle", "High"]),
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the indoor unit's fan speed. 1: Low; 2: Middle; 3: High"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_NUMBER_OF_STATES, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "3"),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.AirflowDirection_Setting),
            Type = BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_MULTI_STATE_OUTPUT:" + (bBase + MemberNumber.AirflowDirection_Setting)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "AirDirectionCommand_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "14"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "5"),
              new Property(BacnetPropertyIds.PROP_STATE_TEXT, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ["Horizontal", "22.5deg", "45deg", "67.5deg", "Vertical"]),
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to change the indoor unit's airflow direction. 1: Horizontal; 2: 22.5deg; 3: 45deg; 4: 67.5deg; 5: Vertical"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_NUMBER_OF_STATES, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "5"),
              new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.AirflowDirection_Status),
            Type = BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_MULTI_STATE_INPUT:" + (bBase + MemberNumber.AirflowDirection_Status)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "AirDirectionStatus_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "13"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "5"),
              new Property(BacnetPropertyIds.PROP_STATE_TEXT, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ["Horizontal", "22.5deg", "45deg", "67.5deg", "Vertical"]),
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the indoor unit's airflow direction. 1: Horizontal; 2: 22.5deg; 3: 45deg; 4: 67.5deg; 5: Vertical"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_NUMBER_OF_STATES, BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "5"),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Setting),
            Type = BacnetObjectTypes.OBJECT_BINARY_VALUE,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_BINARY_VALUE:" + (bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Setting)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "5"),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "RemoteControlStart_" + vrfName),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to permit or prohibit the On/Off operation from the remote controller."),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRIORITY_ARRAY, BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, ["","","","","","","","","","","","","","","",""]),
              new Property(BacnetPropertyIds.PROP_RELINQUISH_DEFAULT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Status),
            Type = BacnetObjectTypes.OBJECT_BINARY_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_BINARY_INPUT:" + (bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Status)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "3"),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "RemoteControlStart_" + vrfName),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor status of permit or prohibit the On/Off operation from the remote controller."),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_POLARITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.Electricity),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the indoor unit's electric consumption."),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.Electricity)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Electricity_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "48"),
              new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
            }
          });

          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.HeatLoad),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object is used to monitor the heat load of indoor unit."),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.HeatLoad)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "HeatLoad_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "48"),
              new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
            }
          });
        }
      }

      return strg;
    }

    #endregion

    #region IBACnetController実装

    /// <summary>制御値を機器やセンサに反映する</summary>
    public void ApplyManipulatedVariables(DateTime dTime)
    {
      if (dTime < nextSignalApply) return;
      nextSignalApply = dTime.AddSeconds(SIGNAL_UPDATE_SPAN);

      lock (Communicator)
      {
        int iuNum = 0;
        for (int i = 0; i < vrfSystems.Length; i++)
        {
          int bBase;
          BacnetObjectId boID;
          ExVRFSystem vrf = vrfSystems[i];
          bool isSystemOn = false;
          for (int j = 0; j < vrf.VRFSystem.IndoorUnitNumber; j++)
          {
            bBase = 1000 * (i + 1) + 100 * (j + 1);

            //On/off******************
            bool isIUonSet = 1u == (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + MemberNumber.OnOff_Setting)));
            bool isIUonStt = 1u == (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(bBase + MemberNumber.OnOff_Status)));
            if (isIUonSet != isIUonStt) //設定!=状態の場合には更新処理
              Communicator.Storage.WriteProperty(
                new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(bBase + MemberNumber.OnOff_Status)),
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, isIUonSet ? 1u : 0u)
                );
            //1台でも室内機が動いていれば室外機はOn
            isSystemOn |= isIUonSet;

            //運転モード****************
            uint modeSet = (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + MemberNumber.OperationMode_Setting)));
            uint modeStt = (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(bBase + MemberNumber.OperationMode_Status)));
            if (modeSet != modeStt) //設定!=状態の場合には更新処理
              Communicator.Storage.WriteProperty(
                new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(bBase + MemberNumber.OperationMode_Status)),
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, modeSet)
                );
            vrf.IndoorUnitModes[j] =
              !isIUonSet ? ExVRFSystem.Mode.ShutOff :
              modeSet == 1 ? ExVRFSystem.Mode.Cooling :
              modeSet == 2 ? ExVRFSystem.Mode.Heating : ExVRFSystem.Mode.ThermoOff;

            //室内温度設定***************
            float tSpSet = (float)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.Setpoint_Setting)));
            float tSpStt = (float)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.Setpoint_Status)));
            if (tSpSet != tSpStt) //設定!=状態の場合には更新処理
            {
              vrf.SetSetpoint(tSpSet, j, true);
              vrf.SetSetpoint(tSpSet, j, false);
              Communicator.Storage.WriteProperty(
                new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.Setpoint_Status)),
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, tSpSet)
                );
            }

            //ファン風量*****************
            uint fanSpdSet = (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + MemberNumber.FanSpeed_Setting)));
            uint fanSpdStt = (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(bBase + MemberNumber.FanSpeed_Status)));
            if (fanSpdSet != fanSpdStt)
              Communicator.Storage.WriteProperty(
                new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(bBase + MemberNumber.FanSpeed_Status)),
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, fanSpdSet)
                );
            vrf.FanSpeeds[j] =
              fanSpdSet == 1 ? ExVRFSystem.FanSpeed.Low :
              fanSpdSet == 2 ? ExVRFSystem.FanSpeed.Middle : ExVRFSystem.FanSpeed.High;

            //風向***********************
            //1:Horizontal, 2:22.5deg ,3:45deg ,4:67.5deg ,5:Vertical
            uint afDirSet = (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + MemberNumber.AirflowDirection_Setting)));
            uint afDirStt = (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(bBase + MemberNumber.AirflowDirection_Status)));
            if (afDirSet != afDirStt) //設定!=状態の場合には更新処理
              Communicator.Storage.WriteProperty(
                new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)(bBase + MemberNumber.AirflowDirection_Status)),
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, afDirSet)
                );
            vrf.Direction[j] = Math.PI / 180d * Math.Max(5, Math.Min(90, (afDirSet - 1) * 22.5)); //水平でも5degはあることにする

            //リモコン手元操作許可禁止*****
            bool rmtPmtSPSet = 1u == (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)(bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Setting)));
            bool rmtPmtSPStt = 1u == (uint)Communicator.Storage.ReadPresentValue(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(bBase + MemberNumber.RemoteControllerPermittion_Setpoint_Status)));
            if (rmtPmtSPSet != rmtPmtSPStt)
              vrf.PermitSPControl[j] = rmtPmtSPSet;

            iuNum++;
          }

          bBase = 1000 * (i + 1);

          //蒸発温度・凝縮温度強制設定***
          bool fcRefSet = 1u == (uint)Communicator.Storage.ReadPresentValue(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)(bBase + MemberNumber.ForcedRefrigerantTemperature_Setting)));
          bool fcRefStt = 1u == (uint)Communicator.Storage.ReadPresentValue(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(bBase + MemberNumber.ForcedRefrigerantTemperature_Status)));
          if (fcRefSet != fcRefStt) //設定!=状態の場合には更新処理
            Communicator.Storage.WriteProperty(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(bBase + MemberNumber.ForcedRefrigerantTemperature_Status)),
              BacnetPropertyIds.PROP_PRESENT_VALUE,
              new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, fcRefSet ? 1u :0u)
              );

          //蒸発温度設定***************
          float tEvpSet = (float)Communicator.Storage.ReadPresentValue(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.EvaporatingTemperatureSetpoint_Setting)));
          float tEvpStt = (float)Communicator.Storage.ReadPresentValue(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.EvaporatingTemperatureSetpoint_Status)));
          if (tEvpSet != tEvpStt) //設定!=状態の場合には更新処理
            Communicator.Storage.WriteProperty(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.EvaporatingTemperatureSetpoint_Status)),
              BacnetPropertyIds.PROP_PRESENT_VALUE,
              new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, tEvpSet)
              );

          //凝縮温度設定***************
          float tCndSet = (float)Communicator.Storage.ReadPresentValue(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.CondensingTemperatureSetpoint_Setting)));
          float tCndStt = (float)Communicator.Storage.ReadPresentValue(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.CondensingTemperatureSetpoint_Status)));
          if (tCndSet != tCndStt) //設定!=状態の場合には更新処理
            Communicator.Storage.WriteProperty(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.CondensingTemperatureSetpoint_Status)),
              BacnetPropertyIds.PROP_PRESENT_VALUE,
              new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, tCndSet)
              );

          //蒸発・凝縮温度反映
          vrfSystems[i].VRFSystem.TargetEvaporatingTemperature
            = fcRefSet ? tEvpSet : VRFSystem.NOMINAL_EVPORATING_TEMPERATURE;

          vrfSystems[i].VRFSystem.TargetCondensingTemperature
            = fcRefSet ? tCndSet : VRFSystem.NOMINAL_CONDENSING_TEMPERATURE;
        }
      }
    }

    /// <summary>機器やセンサの検出値を取得する</summary>
    public void ReadMeasuredValues(DateTime dTime)
    {
      if (dTime < nextSignalRead) return;
      nextSignalRead = dTime.AddSeconds(SIGNAL_UPDATE_SPAN);

      lock (Communicator)
      {
        int iuNum = 0;
        for (int i = 0; i < vrfSystems.Length; i++)
        {
          BacnetObjectId boID;
          ExVRFSystem vrf = vrfSystems[i];

          float hlSum = 0;
          for (int j = 0; j < vrf.VRFSystem.IndoorUnitNumber; j++)
          {
            int bBase = 1000 * (i + 1) + 100 * (j + 1);

            ImmutableVRFUnit unt = vrf.VRFSystem.IndoorUnits[j];

            //室内温度設定***************
            //変更があったときのみ反映としないと、室内機モデルの現在値が割り込みで反映されるリスクあり 2024.02.25 BugFix
            bool hasSPChanged = vrf.HasSetpointChanged(j, vrf.VRFSystem.CurrentMode != VRFSystem.Mode.Heating);
            if (hasSPChanged)
            {
              Communicator.Storage.WriteProperty(
                new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + MemberNumber.Setpoint_Setting)),
                BacnetPropertyIds.PROP_PRESENT_VALUE,
                new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)vrf.GetSetpoint(j, vrf.VRFSystem.CurrentMode != VRFSystem.Mode.Heating))
              );
              vrf.ResetSetpointChangedFlag(j, vrf.VRFSystem.CurrentMode != VRFSystem.Mode.Heating);
            }

            //吸い込み温度***************
            Communicator.Storage.WriteProperty(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.MeasuredRoomTemperature)),
              BacnetPropertyIds.PROP_PRESENT_VALUE,
              new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)unt.InletAirTemperature)
              );

            //吸い込み湿度***************
            float rhmd = (float)MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
              (vrf.VRFSystem.IndoorUnits[j].InletAirTemperature, unt.InletAirHumidityRatio, ATM);
            Communicator.Storage.WriteProperty(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.MeasuredRelativeHumidity)),
              BacnetPropertyIds.PROP_PRESENT_VALUE,
              new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, rhmd)
              );

            //室内機消費電力*************
            Communicator.Storage.WriteProperty(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.Electricity)),
              BacnetPropertyIds.PROP_PRESENT_VALUE,
              new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)unt.FanElectricity)
              );

            //熱負荷*************
            hlSum += (float)unt.HeatTransfer;
            Communicator.Storage.WriteProperty(
              new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.HeatLoad)),
              BacnetPropertyIds.PROP_PRESENT_VALUE,
              new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)unt.HeatTransfer)
              );

            iuNum++;
          }

          //室外機消費電力*************
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(1000 * (i + 1) + MemberNumber.Electricity)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)(vrf.VRFSystem.CompressorElectricity + vrf.VRFSystem.OutdoorUnitFanElectricity))
            );

          //室外機熱負荷*************
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(1000 * (i + 1) + MemberNumber.HeatLoad)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, hlSum)
            );

        }
      }
    }

    /// <summary>BACnetControllerのサービスを開始する</summary>
    public void StartService()
    {
      Communicator.StartService();
    }

    /// <summary>BACnetControllerのリソースを解放する</summary>
    public void EndService()
    {
      Communicator.EndService();
    }

    #endregion

  }
}
