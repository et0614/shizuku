using System.IO.BACnet;
using BaCSharp;

namespace Shizuku2.BACnet
{

  /// <summary>Shizuku2のOccupantモニタとの通信ユーティリティクラス</summary>
  public class OccupantCommunicator
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint OCCUPANTMONITOR_DEVICE_ID = 5;

    /// <summary>排他的ポート番号</summary>
    public const int OCCUPANTMONITOR_EXCLUSIVE_PORT = 0xBAC0 + (int)OCCUPANTMONITOR_DEVICE_ID;

    /// <summary>OccupantモニタのBACnetアドレス</summary>
    private readonly BacnetAddress bacAddress;

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    public enum OccupantMonitorMember
    {
      /// <summary>執務者の数</summary>
      OccupantNumber = 1,
      /// <summary>在室状況</summary>
      Availability = 2,
      /// <summary>温冷感</summary>
      ThermalSensation = 3,
    }

    /// <summary>テナント</summary>
    public enum Tenant
    {
      /// <summary>南西テナント</summary>
      SouthWest = 1,
      /// <summary>南東テナント</summary>
      SouthEast = 2,
      /// <summary>北西テナント</summary>
      NorthWest = 3,
      /// <summary>北東テナント</summary>
      NorthEast = 4
    }

    /// <summary>温冷感申告値</summary>
    public enum ThermalSensation
    {
      /// <summary>Cold</summary>
      Cold = -3,
      /// <summary>Cool</summary>
      Cool = -2,
      /// <summary>Slightly Cool</summary>
      SlightlyCool = -1,
      /// <summary>Neutral</summary>
      Neutral = 0,
      /// <summary>Slightly Warm</summary>
      SlightlyWarm = 1,
      /// <summary>Warm</summary>
      Warm = 2,
      /// <summary>Hot</summary>
      Hot = 3
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    private BACnetCommunicator communicator;

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="id">通信に使うBACnet DeviceのID</param>
    /// <param name="name">通信に使うBACnet Deviceの名前</param>
    /// <param name="description">通信に使うBACnet Deviceの説明</param>
    /// <param name="ipAddress">WeatherモニタのIPアドレス（「xxx.xxx.xxx.xxx」の形式）</param>
    public OccupantCommunicator(uint id, string name, string description, string ipAddress = "127.0.0.1")
    {
      DeviceObject dObject = new DeviceObject(id, name, description, true);
      communicator = new BACnetCommunicator(dObject, (int)(0xBAC0 + id));
      bacAddress = new BacnetAddress(BacnetAddressTypes.IP, ipAddress + ":" + OCCUPANTMONITOR_EXCLUSIVE_PORT.ToString());
      communicator.StartService();

      //Who is送信
      communicator.Client.WhoIs();
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>在室している執務者数を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>在室している執務者数</returns>
    public int GetOccupantNumber(Tenant tenant, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        (uint)(10000 * (int)tenant + (int)OccupantMonitorMember.OccupantNumber));

      if (communicator.Client.ReadPropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (int)val[0].Value;
      }

      succeeded = false;
      return 0;
    }

    /// <summary>在室しているか否かを取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="occupantIndex">執務者番号（1～）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>在室しているか否か</returns>
    public bool IsOccupantStayInOffice(Tenant tenant, int occupantIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT,
        (uint)(10000 * (int)tenant + 100 * occupantIndex + (int)OccupantMonitorMember.Availability));

      if (communicator.Client.ReadPropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (uint)val[0].Value == 1;
      }

      succeeded = false;
      return false;
    }

    /// <summary>温冷感を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="occupantIndex">執務者番号（1～）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>温冷感</returns>
    public ThermalSensation GetThermalSensation(Tenant tenant, int occupantIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        (uint)(10000 * (int)tenant + 100 * occupantIndex + (int)OccupantMonitorMember.ThermalSensation));

      if (communicator.Client.ReadPropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;

        return convertVote((int)val[0].Value);
      }

      succeeded = false;
      return ThermalSensation.Neutral;
    }

    private ThermalSensation convertVote(int vote)
    {
      switch (vote)
      {
        case -3:
          return ThermalSensation.Cold;
        case -2:
          return ThermalSensation.Cool;
        case -1:
          return ThermalSensation.SlightlyCool;
        case 0:
          return ThermalSensation.Neutral;
        case 1:
          return ThermalSensation.SlightlyWarm;
        case 2:
          return ThermalSensation.Warm;
        case 3:
          return ThermalSensation.Hot;
        default:
          return ThermalSensation.Neutral;
      }
    }

    #endregion

  }
}
