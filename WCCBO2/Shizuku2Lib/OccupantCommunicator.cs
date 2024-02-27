using System.IO.BACnet;

namespace Shizuku2.BACnet
{

  /// <summary>Shizuku2のOccupantモニタとの通信ユーティリティクラス</summary>
  public class OccupantCommunicator : PresentValueReadWriter
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
    private enum memberNumber
    {
      /// <summary>執務者の数</summary>
      OccupantNumber = 1,
      /// <summary>在室状況</summary>
      Availability = 2,
      /// <summary>温冷感</summary>
      ThermalSensation = 3,
      /// <summary>着衣量</summary>
      ClothingIndex = 4,
      /// <summary>熱的な不満足者率</summary>
      Dissatisfied_Thermal,
      /// <summary>ドラフトによる不満足者率</summary>
      Dissatisfied_Draft,
      /// <summary>上下温度分布による不満足者率</summary>
      Dissatisfied_VerticalTemp
    }

    /// <summary>テナント</summary>
    public enum Tenant
    {
      /// <summary>南テナント</summary>
      South = 1,
      /// <summary>北テナント</summary>
      North = 2
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

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="id">通信に使うBACnet DeviceのID</param>
    /// <param name="name">通信に使うBACnet Deviceの名前</param>
    /// <param name="ipAddress">エミュレータのIPアドレス（「xxx.xxx.xxx.xxx」の形式）</param>
    public OccupantCommunicator(uint id, string name = "anoymous device", string ipAddress = "127.0.0.1")
      : base(id, name)
    {
      bacAddress = new BacnetAddress(BacnetAddressTypes.IP, ipAddress + ":" + OCCUPANTMONITOR_EXCLUSIVE_PORT.ToString());
    }

    #endregion

    #region テナント・ゾーン別

    /// <summary>在室している執務者数を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>在室している執務者数</returns>
    public int GetOccupantNumber(Tenant tenant, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + (int)memberNumber.OccupantNumber);
      return ReadPresentValue<int>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    /// <summary>ゾーンに在室している執務者数を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="zoneNumber">ゾーン番号</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーンに在室している執務者数</returns>
    public int GetOccupantNumber(Tenant tenant, int zoneNumber, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + 1000 * zoneNumber + (int)memberNumber.OccupantNumber);
      return ReadPresentValue<int>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    /// <summary>ゾーンに在室している執務者の平均温冷感申告値を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="zoneNumber">ゾーン番号</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーンに在室している執務者の平均温冷感申告値</returns>
    public float GetAveragedThermalSensation(Tenant tenant, int zoneNumber, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + 1000 * zoneNumber + (int)memberNumber.ThermalSensation);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    /// <summary>ゾーンに在室している執務者の平均着衣量を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="zoneNumber">ゾーン番号</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーンに在室している執務者の平均着衣量</returns>
    public float GetAveragedClothingIndex(Tenant tenant, int zoneNumber, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + 1000 * zoneNumber + (int)memberNumber.ClothingIndex);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    /// <summary>ゾーンに在室している執務者の温熱環境に対する不満足者率を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="zoneNumber">ゾーン番号</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーンに在室している執務者の温熱環境に対する不満足者率</returns>
    public float GetThermallyDissatisfiedRate(Tenant tenant, int zoneNumber, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + 1000 * zoneNumber + (int)memberNumber.Dissatisfied_Thermal);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    /// <summary>ゾーンに在室している執務者のドラフトに対する不満足者率を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="zoneNumber">ゾーン番号</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーンに在室している執務者のドラフトに対する不満足者率</returns>
    public float GetDissatisfiedRateCausedByDraft(Tenant tenant, int zoneNumber, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + 1000 * zoneNumber + (int)memberNumber.Dissatisfied_Draft);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    /// <summary>ゾーンに在室している執務者の上下温度分布に対する不満足者率を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="zoneNumber">ゾーン番号</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーンに在室している執務者の上下温度分布に対する不満足者率</returns>
    public float GetDissatisfiedRateCausedByVerticalTemperatureDistribution(Tenant tenant, int zoneNumber, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + 1000 * zoneNumber + (int)memberNumber.Dissatisfied_VerticalTemp);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    #endregion

    #region 執務者別

    /// <summary>在室しているか否かを取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="occupantIndex">執務者番号（1～）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>在室しているか否か</returns>
    public bool IsOccupantStayInOffice(Tenant tenant, int occupantIndex, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + 10 * occupantIndex + (int)memberNumber.Availability);
      return 1 == ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_BINARY_INPUT, instNum, out succeeded);
    }

    /// <summary>温冷感を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="occupantIndex">執務者番号（1～）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>温冷感</returns>
    public ThermalSensation GetThermalSensation(Tenant tenant, int occupantIndex, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + 10 * occupantIndex + (int)memberNumber.ThermalSensation);
      return convertVote(ReadPresentValue<int>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded));
    }

    /// <summary>着衣量を取得する</summary>
    /// <param name="tenant">テナント</param>
    /// <param name="occupantIndex">執務者番号（1～）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>着衣量</returns>
    public float GetClothingIndex(Tenant tenant, int occupantIndex, out bool succeeded)
    {
      uint instNum = (uint)(10000 * (int)tenant + 10 * occupantIndex + (int)memberNumber.ClothingIndex);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    #endregion

    #region privateメソッド

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
