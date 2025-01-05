using BaCSharp;
using System.IO.BACnet;
using Popolo.ThermalLoad;
using Popolo.ThermophysicalProperty;

using System.IO.BACnet.Storage;
using System.Reflection;

namespace Shizuku2.BACnet
{
  internal class EnvironmentMonitor : IBACnetController
  {
    //BACnet Object IDは以下のルールで付与
    //外界情報はMember Number
    //ゾーンの温湿度は
    //1000*室外機番号 + 100*室内機番号 + Member Number

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DEVICE_ID = 4;

    /// <summary>排他的ポート番号</summary>
    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    public enum MemberNumber
    {
      /// <summary>乾球温度</summary>
      DrybulbTemperature = 1,
      /// <summary>相対湿度</summary>
      RelativeHumdity = 2,
      /// <summary>水平面全天日射</summary>
      GlobalHorizontalRadiation = 3,
      /// <summary>夜間放射</summary>
      NocturnalRadiation = 4,
      /// <summary>エネルギー消費量</summary>
      EnergyConsumption = 5,
      /// <summary>平均不満足者率</summary>
      DissatisfactionRate = 6
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator;

    private readonly ExVRFSystem[] vrfSystems;

    /// <summary>熱負荷計算モデル</summary>
    private ImmutableBuildingThermalModel building { get; set; }

    /// <summary>平均不満足者率[-]を設定・取得する</summary>
    public double AveragedDissatisfactionRate { get; set; }

    /// <summary>積算エネルギー消費量[GJ]を設定・取得する</summary>
    public double TotalEnergyConsumption { get; set; }    

    #endregion

    #region コンストラクタ

    public EnvironmentMonitor(ImmutableBuildingThermalModel model, ExVRFSystem[] vrfs, string localEndpointIP)
    {
      this.building = model;
      this.vrfSystems = vrfs;

      DeviceStorage strg = DeviceStorage.Load(
        new StreamReader
        (Assembly.GetExecutingAssembly().GetManifestResourceStream("Shizuku2.Resources.EnvironmentMonitorStorage.xml"))
        );

      for (int ouIndx = 0; ouIndx < vrfSystems.Length; ouIndx++)
      {
        for (int iuIndx = 0; iuIndx < vrfSystems[ouIndx].IndoorUnitModes.Length; iuIndx++)
        {
          //室内機ごとの情報
          int bBase = 1000 * (1 + ouIndx) + 100 * (1 + iuIndx);
          string vrfName = "VRF" + (1 + ouIndx) + "-" + (1 + iuIndx);

          //乾球温度
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.DrybulbTemperature),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Dry-bulb temperature of zone at " + vrfName + "."),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.DrybulbTemperature)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "DBT_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "25.0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"),
            }
          });

          //相対湿度
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(bBase + MemberNumber.RelativeHumdity),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Relative humidity of zone at " + vrfName + "."),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (bBase + MemberNumber.RelativeHumdity)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "RHMD_" + vrfName),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "50.0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "29"),
              new Property(BacnetPropertyIds.PROP_LOW_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_HIGH_LIMIT, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "100"),
            }
          });
        }
      }

      Communicator = new BACnetCommunicator(strg, EXCLUSIVE_PORT, localEndpointIP);
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime)
    { }

    public void EndService()
    {
      Communicator.EndService();
    }

    public void ReadMeasuredValues(DateTime dTime)
    {
      //乾球温度
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.DrybulbTemperature),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)building.OutdoorTemperature)
        );

      //相対湿度
      float rhmd = (float)MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
        (building.OutdoorTemperature, building.OutdoorHumidityRatio, 101.325);
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.RelativeHumdity),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, rhmd)
        );

      //水平面全天日射
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.GlobalHorizontalRadiation),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)building.Sun.GlobalHorizontalRadiation)
        );

      //夜間放射
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.NocturnalRadiation),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)building.NocturnalRadiation)
        );

      //エネルギー消費
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.EnergyConsumption),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)(1000 * TotalEnergyConsumption))
        );

      //平均不満足者率
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.DissatisfactionRate),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)AveragedDissatisfactionRate)
        );

      for (int ouIndx = 0; ouIndx < vrfSystems.Length; ouIndx++)
      {
        for (int iuIndx = 0; iuIndx < vrfSystems[ouIndx].IndoorUnitModes.Length; iuIndx++)
        {
          //室内機ごとの情報
          int bBase = 1000 * (1 + ouIndx) + 100 * (1 + iuIndx);

          //乾球温度
          float dbt = (float)vrfSystems[ouIndx].GetLowerZoneTemperature(iuIndx);
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.DrybulbTemperature)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, dbt)
            );

          //相対湿度
          float rhmd2 = (float)MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
            (dbt, (float)vrfSystems[ouIndx].GetLowerZoneAbsoluteHumidity(iuIndx), 101.325);
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.RelativeHumdity)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, rhmd2)
            );
        }
      }
    }

    public void StartService()
    {
      Communicator.StartService();
    }

    #endregion

  }
}
