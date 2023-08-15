using BaCSharp;
using System.IO.BACnet;
using Popolo.ThermalLoad;
using Popolo.ThermophysicalProperty;

namespace Shizuku2.BACnet
{
  internal class WeatherMonitor : IBACnetController
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DEVICE_ID = 4;

    /// <summary>排他的ポート番号</summary>
    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    /// <summary>Deviceの名称</summary>
    const string DEVICE_NAME = "Weather monitor";

    /// <summary>Deviceの説明</summary>
    const string DEVICE_DESCRIPTION = "BACnet device monitoring weather state.";

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
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator;

    /// <summary>熱負荷計算モデル</summary>
    private ImmutableBuildingThermalModel building { get; set; }

    #endregion

    #region コンストラクタ

    public WeatherMonitor(ImmutableBuildingThermalModel model)
    {
      building = model;

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
        "Dry-bulb temperature",
        "Outdoor dry-bulb temperature.", 25, BacnetUnitsId.UNITS_DEGREES_CELSIUS));

      //相対湿度
      dObject.AddBacnetObject(new AnalogInput<float>
        ((int)MemberNumber.RelativeHumdity,
        "Relative humidity",
        "Outdoor relative humidity.", 50, BacnetUnitsId.UNITS_PERCENT_RELATIVE_HUMIDITY)
      { PROP_LOW_LIMIT = 0, PROP_HIGH_LIMIT = 100 });

      //水平面全天日射
      dObject.AddBacnetObject(new AnalogInput<float>
        ((int)MemberNumber.GlobalHorizontalRadiation,
        "Global horizontal radiation",
        "Global horizontal radiation.", 0, BacnetUnitsId.UNITS_WATTS_PER_SQUARE_METER));

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

      //水平面全天日射量
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.GlobalHorizontalRadiation);
      ((AnalogInput<float>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)building.Sun.GlobalHorizontalRadiation;

    }

    public void StartService()
    {
      Communicator.StartService();
    }

    #endregion

  }
}
