using BaCSharp;
using System.IO.BACnet;
using Popolo.ThermalLoad;
using Popolo.ThermophysicalProperty;

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

    /// <summary>Deviceの名称</summary>
    const string DEVICE_NAME = "Environment monitor";

    /// <summary>Deviceの説明</summary>
    const string DEVICE_DESCRIPTION = "BACnet device monitoring outdoor and room environment.";

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
      NocturnalRadiation = 4
    }

    #endregion

    #region インスタンス変数・プロパティ

    private readonly ExVRFSystem[] vrfSystems;

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator;

    /// <summary>熱負荷計算モデル</summary>
    private ImmutableBuildingThermalModel building { get; set; }

    #endregion

    #region コンストラクタ

    public EnvironmentMonitor(ImmutableBuildingThermalModel model, ExVRFSystem[] vrfs)
    {
      this.building = model;
      this.vrfSystems = vrfs;

      Communicator = new BACnetCommunicator
        (makeDeviceObject(), EXCLUSIVE_PORT);
    }

    /// <summary>BACnet Deviceを作成する</summary>
    private DeviceObject makeDeviceObject()
    {
      DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

      //乾球温度
      dObject.AddBacnetObject(new AnalogInput<float>
        ((int)MemberNumber.DrybulbTemperature,
        "Outdoor dry-bulb temperature",
        "Outdoor dry-bulb temperature.", 25, BacnetUnitsId.UNITS_DEGREES_CELSIUS));

      //相対湿度
      dObject.AddBacnetObject(new AnalogInput<float>
        ((int)MemberNumber.RelativeHumdity,
        "Outdoor relative humidity",
        "Outdoor relative humidity.", 50, BacnetUnitsId.UNITS_PERCENT_RELATIVE_HUMIDITY)
      { PROP_LOW_LIMIT = 0, PROP_HIGH_LIMIT = 100 });

      //水平面全天日射
      dObject.AddBacnetObject(new AnalogInput<float>
        ((int)MemberNumber.GlobalHorizontalRadiation,
        "Global horizontal radiation",
        "Global horizontal radiation.", 0, BacnetUnitsId.UNITS_WATTS_PER_SQUARE_METER));

      //夜間放射
      dObject.AddBacnetObject(new AnalogInput<float>
        ((int)MemberNumber.NocturnalRadiation,
        "Nocturnal radiation",
        "Nocturnal radiation.", 0, BacnetUnitsId.UNITS_WATTS_PER_SQUARE_METER));

      for (int ouIndx = 0; ouIndx < vrfSystems.Length; ouIndx++)
      {
        for (int iuIndx = 0; iuIndx < vrfSystems[ouIndx].IndoorUnitModes.Length; iuIndx++)
        {
          //室内機ごとの情報
          int bBase = 1000 * (1 + ouIndx) + 100 * (1 + iuIndx);
          string vrfName = "VRF" + (1 + ouIndx) + "-" + (1 + iuIndx);

          //乾球温度
          dObject.AddBacnetObject(new AnalogInput<float>
            ((int)(bBase + MemberNumber.DrybulbTemperature),
            "Dry-bulb temperature (" + vrfName + ")",
            "Dry-bulb temperature.", 25, BacnetUnitsId.UNITS_DEGREES_CELSIUS));

          //相対湿度
          dObject.AddBacnetObject(new AnalogInput<float>
            ((int)(bBase + MemberNumber.RelativeHumdity),
            "Relative humidity (" + vrfName + ")",
            "Relative humidity.", 50, BacnetUnitsId.UNITS_PERCENT_RELATIVE_HUMIDITY)
          { PROP_LOW_LIMIT = 0, PROP_HIGH_LIMIT = 100 });
        }
      }

      return dObject;
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
      BacnetObjectId boID;

      //乾球温度
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.DrybulbTemperature);
      ((AnalogInput<float>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)building.OutdoorTemperature;

      //相対湿度
      float rhmd = (float)MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(building.OutdoorTemperature, building.OutdoorHumidityRatio, 101.325);
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.RelativeHumdity);
      ((AnalogInput<float>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = rhmd;

      //水平面全天日射
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.GlobalHorizontalRadiation);
      ((AnalogInput<float>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)building.Sun.GlobalHorizontalRadiation;

      //夜間放射
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.NocturnalRadiation);
      ((AnalogInput<float>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)building.NocturnalRadiation;

      for (int ouIndx = 0; ouIndx < vrfSystems.Length; ouIndx++)
      {
        for (int iuIndx = 0; iuIndx < vrfSystems[ouIndx].IndoorUnitModes.Length; iuIndx++)
        {
          //室内機ごとの情報
          int bBase = 1000 * (1 + ouIndx) + 100 * (1 + iuIndx);

          //乾球温度
          float dbt = (float)vrfSystems[ouIndx].GetLowerZoneTemperature(iuIndx);
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.DrybulbTemperature));
          ((AnalogInput<float>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = dbt;

          //相対湿度
          float rhmd2 = (float)MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
            (dbt, (float)vrfSystems[ouIndx].GetLowerZoneAbsoluteHumidity(iuIndx), 101.325);
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(bBase + MemberNumber.RelativeHumdity));
          ((AnalogInput<float>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = rhmd2;
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
