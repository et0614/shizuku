using System.IO.BACnet;
using System.IO.BACnet.Base;

namespace Shizuku2.BACnet
{

  /// <summary>Shizuku2のVentilationコントローラとの通信ユーティリティクラス</summary>
  public class VentilationSystemCommunicator : PresentValueReadWriter
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint VENTCTRL_DEVICE_ID = 6;

    /// <summary>排他的ポート番号</summary>
    public const int VENTCTRL_EXCLUSIVE_PORT = 0xBAC0 + (int)VENTCTRL_DEVICE_ID;

    /// <summary>WeatherモニタのBACnetアドレス</summary>
    private readonly BacnetAddress bacAddress;

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    private enum memberNumber
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

    /// <summary>ファン風量</summary>
    public enum FanSpeed
    {
      /// <summary>弱</summary>
      Low = 1,
      /// <summary>中</summary>
      Middle = 2,
      /// <summary>強</summary>
      High = 3
    }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="id">通信に使うBACnet DeviceのID</param>
    /// <param name="name">通信に使うBACnet Deviceの名前</param>
    /// <param name="ipAddress">エミュレータのIPアドレス（「xxx.xxx.xxx.xxx」の形式）</param>
    public VentilationSystemCommunicator(uint id, string name = "anoymous device", string ipAddress = "127.0.0.1")
      : base(id)
    {
      bacAddress = new BacnetAddress(BacnetAddressTypes.IP, ipAddress + ":" + VENTCTRL_EXCLUSIVE_PORT.ToString());
    }

    #endregion

    #region テナント

    /// <summary>南側テナントのCO2濃度[ppm]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>南側テナントのCO2濃度[ppm]</returns>
    public float GetSouthTenantCO2Level(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)memberNumber.SouthCO2Level, out succeeded);
    }

    /// <summary>北側テナントのCO2濃度[ppm]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>北側テナントのCO2濃度[ppm]</returns>
    public float GetNorthTenantCO2Level(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)memberNumber.NorthCO2Level, out succeeded);
    }

    #endregion

    #region HEX別

    /// <summary>換気（全熱交換器）を起動する</summary>
    /// <param name="oUnitIndex">VRFの室外機番号（1～4）</param>
    /// <param name="iUnitIndex">VRFの室内機番号（1～5）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void StartVentilation
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      WritePresentValue(bacAddress,
        BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
        getInstanceNumber(oUnitIndex, iUnitIndex, memberNumber.HexOnOff),
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_ACTIVE),
        out succeeded);
    }

    /// <summary>換気（全熱交換器）を停止する</summary>
    /// <param name="oUnitIndex">VRFの室外機番号（1～4）</param>
    /// <param name="iUnitIndex">VRFの室内機番号（1～5）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void StopVentilation
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      WritePresentValue(bacAddress,
        BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
        getInstanceNumber(oUnitIndex, iUnitIndex, memberNumber.HexOnOff),
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_INACTIVE),
        out succeeded);
    }

    /// <summary>バイパス制御を有効にする</summary>
    /// <param name="oUnitIndex">VRFの室外機番号（1～4）</param>
    /// <param name="iUnitIndex">VRFの室内機番号（1～5）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void EnableBypassControl
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      WritePresentValue(bacAddress,
        BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
        getInstanceNumber(oUnitIndex, iUnitIndex, memberNumber.HexBypassEnabled),
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_ACTIVE),
        out succeeded);
    }

    /// <summary>バイパス制御を無効にする</summary>
    /// <param name="oUnitIndex">VRFの室外機番号（1～4）</param>
    /// <param name="iUnitIndex">VRFの室内機番号（1～5）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void DisableBypassControl
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      WritePresentValue(bacAddress,
        BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
        getInstanceNumber(oUnitIndex, iUnitIndex, memberNumber.HexBypassEnabled),
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_INACTIVE),
        out succeeded);
    }

    /// <summary>ファン風量を変える</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～5）</param>
    /// <param name="fanSpeed">ファン風量</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangeFanSpeed(uint oUnitIndex, uint iUnitIndex, FanSpeed fanSpeed, out bool succeeded)
    {
      WritePresentValue(bacAddress,
        BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
        getInstanceNumber(oUnitIndex, iUnitIndex, memberNumber.HexFanSpeed),
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, 
        fanSpeed == FanSpeed.Low ? 1u : 
        fanSpeed == FanSpeed.Middle ? 2u : 3u),
        out succeeded);
    }

    /// <summary>ファン風量を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～5）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ファン風量</returns>
    public FanSpeed GetFanSpeed(uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      uint fs = ReadPresentValue<uint>
        (bacAddress,
        BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
        getInstanceNumber(oUnitIndex, iUnitIndex, memberNumber.HexFanSpeed),
        out succeeded);
      switch (fs)
      {
        case 1:
          return FanSpeed.Low;
        case 2:
          return FanSpeed.Middle;
        default:
          return FanSpeed.High;
      }
    }

    #endregion

    #region 補助メソッド

    /// <summary>インスタンス番号を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～5）</param>
    /// <param name="member">項目</param>
    /// <returns>インスタンス番号</returns>
    private static uint getInstanceNumber
      (uint oUnitIndex, uint iUnitIndex, memberNumber member)
    {
      if (!isIndexValid(oUnitIndex, iUnitIndex))
        throw new Exception("Index of outdoor/indoor unit is invalid.");

      return 1000 * oUnitIndex + 100 * iUnitIndex + (uint)member;
    }

    /// <summary>室内外機の番号が有効か否かを判定する</summary>
    /// <param name="oUnitIndex">室外機番号</param>
    /// <param name="iUnitIndex">室内機番号</param>
    /// <returns>室内外機の番号が有効か否か</returns>
    private static bool isIndexValid
      (uint oUnitIndex, uint iUnitIndex)
    {
      if (oUnitIndex <= 0 || 4 < oUnitIndex || iUnitIndex <= 0)
        return false;

      if ((oUnitIndex == 1 || oUnitIndex == 3) && 5 < iUnitIndex)
        return false;

      if ((oUnitIndex == 2 || oUnitIndex == 4) && 4 < iUnitIndex)
        return false;

      return true;
    }

    #endregion

  }
}
