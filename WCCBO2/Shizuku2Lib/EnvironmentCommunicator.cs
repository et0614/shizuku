using System.IO.BACnet;

namespace Shizuku2.BACnet
{

  /// <summary>Shizuku2のEnvironmentモニタとの通信ユーティリティクラス</summary>
  public class EnvironmentCommunicator : PresentValueReadWriter
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint ENVIRONMENTMONITOR_DEVICE_ID = 4;

    /// <summary>排他的ポート番号</summary>
    public const int ENVIRONMENTMONITOR_EXCLUSIVE_PORT = 0xBAC0 + (int)ENVIRONMENTMONITOR_DEVICE_ID;

    /// <summary>WeatherモニタのBACnetアドレス</summary>
    private readonly BacnetAddress bacAddress;

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    public enum WeatherMonitorMember
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

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="id">通信に使うBACnet DeviceのID</param>
    /// <param name="name">通信に使うBACnet Deviceの名前</param>
    /// <param name="ipAddress">WeatherモニタのIPアドレス（「xxx.xxx.xxx.xxx」の形式）</param>
    public EnvironmentCommunicator(uint id, string name, string ipAddress = "127.0.0.1")
      : base(id, name)
    {
      bacAddress = new BacnetAddress(BacnetAddressTypes.IP, ipAddress + ":" + ENVIRONMENTMONITOR_EXCLUSIVE_PORT.ToString());
    }

    #endregion

    #region 外気条件

    /// <summary>乾球温度[C]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>乾球温度[C]</returns>
    public float GetDrybulbTemperature(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)WeatherMonitorMember.DrybulbTemperature, out succeeded);
    }

    /// <summary>相対湿度[%]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>相対湿度[%]</returns>
    public float GetRelativeHumidity(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)WeatherMonitorMember.RelativeHumdity, out succeeded);
    }

    /// <summary>水平面全天日射[W/m2]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>水平面全天日射[W/m2]</returns>
    public float GetGlobalHorizontalRadiation(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)WeatherMonitorMember.GlobalHorizontalRadiation, out succeeded);
    }

    /// <summary>夜間放射[W/m2]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>夜間放射[W/m2]</returns>
    public float GetNocturnalRadiation(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)WeatherMonitorMember.NocturnalRadiation, out succeeded);
    }

    #endregion

    #region 室内環境

    /// <summary>ゾーン（下部空間）の乾球温度[C]を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーン（下部空間）の乾球温度[C]</returns>
    public float GetZoneDrybulbTemperature(int oUnitIndex, int iUnitIndex, out bool succeeded)
    {
      uint instNum = (uint)(1000 * oUnitIndex + 100 * iUnitIndex + WeatherMonitorMember.DrybulbTemperature);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    /// <summary>ゾーン（下部空間）の相対湿度[%]を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーン（下部空間）の相対湿度[%]</returns>
    public float GetZoneRelativeHumidity(int oUnitIndex, int iUnitIndex, out bool succeeded)
    {
      uint instNum = (uint)(1000 * oUnitIndex + 100 * iUnitIndex + WeatherMonitorMember.RelativeHumdity);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    #endregion

  }
}
