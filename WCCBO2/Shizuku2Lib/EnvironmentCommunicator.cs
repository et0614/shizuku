using System.IO.BACnet;

namespace Shizuku2.BACnet
{

  /// <summary>Shizuku2のEnvironmentモニタとの通信ユーティリティクラス</summary>
  public class EnvironmentCommunicator : DateTimeCommunicator
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
    private enum MemberNumber
    {
      /// <summary>乾球温度</summary>
      DrybulbTemperature = 1,
      /// <summary>相対湿度</summary>
      RelativeHumdity = 2,
      /// <summary>水平面全天日射</summary>
      GlobalHorizontalRadiation = 3,
      /// <summary>夜間放射</summary>
      NocturnalRadiation = 4,
      /// <summary>合計エネルギー消費量</summary>
      TotalEnergyConsumption = 5,
      /// <summary>平均不満足者率</summary>
      AveragedDissatisfactionRate = 6,
      /// <summary>瞬時エネルギー消費量</summary>
      InstantaneousEnergyConsumption = 7,
      /// <summary>瞬時不満足者率</summary>
      InstantaneousDissatisfactionRate = 8,
    }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="id">通信に使うBACnet DeviceのID</param>
    /// <param name="name">通信に使うBACnet Deviceの名前</param>
    /// <param name="ipAddress">エミュレータのIPアドレス（「xxx.xxx.xxx.xxx」の形式）</param>
    public EnvironmentCommunicator(uint id, string name = "anoymous device", string ipAddress = "127.0.0.1")
      : base(id)
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
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.DrybulbTemperature, out succeeded);
    }

    /// <summary>相対湿度[%]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>相対湿度[%]</returns>
    public float GetRelativeHumidity(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.RelativeHumdity, out succeeded);
    }

    /// <summary>水平面全天日射[W/m2]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>水平面全天日射[W/m2]</returns>
    public float GetGlobalHorizontalRadiation(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.GlobalHorizontalRadiation, out succeeded);
    }

    /// <summary>夜間放射[W/m2]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>夜間放射[W/m2]</returns>
    public float GetNocturnalRadiation(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.NocturnalRadiation, out succeeded);
    }

    #endregion

    #region 室内環境

    /// <summary>ゾーン（下部空間）の乾球温度[C]を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～5）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーン（下部空間）の乾球温度[C]</returns>
    public float GetZoneDrybulbTemperature(int oUnitIndex, int iUnitIndex, out bool succeeded)
    {
      uint instNum = (uint)(1000 * oUnitIndex + 100 * iUnitIndex + MemberNumber.DrybulbTemperature);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    /// <summary>ゾーン（下部空間）の相対湿度[%]を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～5）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ゾーン（下部空間）の相対湿度[%]</returns>
    public float GetZoneRelativeHumidity(int oUnitIndex, int iUnitIndex, out bool succeeded)
    {
      uint instNum = (uint)(1000 * oUnitIndex + 100 * iUnitIndex + MemberNumber.RelativeHumdity);
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum, out succeeded);
    }

    #endregion

    #region 成績関連

    /// <summary>合計エネルギー消費量[GJ]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>合計エネルギー消費量[GJ]</returns>
    public float GetTotalEnergyConsumption(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.TotalEnergyConsumption, out succeeded);
    }

    /// <summary>平均不満足者率[-]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>平均不満足者率[-]</returns>
    public float GetAveragedDissatisfactionRate(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.AveragedDissatisfactionRate, out succeeded);
    }

    /// <summary>瞬時エネルギー消費量[kW]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>瞬時エネルギー消費量[kW]</returns>
    public float GetInstantaneousEnergyConsumption(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.InstantaneousEnergyConsumption, out succeeded);
    }

    /// <summary>瞬時不満足者率[-]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>瞬時不満足者率[-]</returns>
    public float GetInstantaneousDissatisfactionRate(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.InstantaneousDissatisfactionRate, out succeeded);
    }

    #endregion

    /* 2024.10.01テスト。負荷が大きすぎるために保留
    #region COV登録関連

    private bool covSubscribed = false;

    private bool[,] znTmpCOVSubscribed = new bool[4, 8];
    private double[,] znTmps = new double[4, 8];

    public bool EnableZoneTemperatureCOV(int oUnitIndex, int iUnitIndex)
    {
      //COV未登録の場合は登録
      if (!covSubscribed)
        client.OnCOVNotification += Client_OnCOVNotification;

      //温度がそもそも登録されている場合
      if (znTmpCOVSubscribed[oUnitIndex - 1, iUnitIndex - 1]) return true;

      uint instNum = (uint)(1000 * oUnitIndex + 100 * iUnitIndex + memberNumber.DrybulbTemperature);
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum);

      //まず現在値を読み取る
      znTmps[oUnitIndex - 1, iUnitIndex - 1] = GetZoneDrybulbTemperature(oUnitIndex, iUnitIndex, out bool succeeded);
      if (!succeeded) return false;

      //COV登録
      znTmpCOVSubscribed[oUnitIndex - 1, iUnitIndex - 1] = client.SubscribeCOVRequest(bacAddress, boID, instNum, false, false, 3600);
      return znTmpCOVSubscribed[oUnitIndex, iUnitIndex];
    }

    public bool DisableZoneTemperatureCOV(int oUnitIndex, int iUnitIndex)
    {
      //温度がそもそも登録されていない場合
      if (znTmpCOVSubscribed[oUnitIndex - 1, iUnitIndex - 1]) return true;

      uint instNum = (uint)(1000 * oUnitIndex + 100 * iUnitIndex + memberNumber.DrybulbTemperature);
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, instNum);

      //COV解除
      bool rslt = client.SubscribeCOVRequest(bacAddress, boID, instNum, true, false, 3600);
      if(rslt) znTmpCOVSubscribed[oUnitIndex, iUnitIndex] = false;
      return rslt;
    }

    private void Client_OnCOVNotification(
      BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier, 
      BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier, 
      uint timeRemaining, bool needConfirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
    {
      UInt16 port = BitConverter.ToUInt16(new byte[] { adr.adr[5], adr.adr[4] });

      //ポート番号が異なる場合は終了
      if (port != ENVIRONMENTMONITOR_EXCLUSIVE_PORT) return;
      uint instID = monitoredObjectIdentifier.instance;

      //ゾーンの温湿度が変化した場合
      if (
        monitoredObjectIdentifier.type == BacnetObjectTypes.OBJECT_ANALOG_INPUT &&
        1100 < instID && instID < 4500)
      {
        //温度は1の位が1
        if (instID % 10 == 1)
        {
          foreach (BacnetPropertyValue vl in values)
          {
            if (vl.property.propertyIdentifier == 85) //Presentvalue
              znTmps[(instID / 1000) % 10 - 1, (instID / 100) % 10 - 1] = (float)vl.value[0].Value;
          }
          Console.WriteLine(znTmps[(instID / 1000) % 10 - 1, (instID / 100) % 10 - 1]);
        }

        //湿度は1の位が2
        if (instID % 10 == 2)
        {

        }
      }

    }

    
    #endregion
    */
  }
}
