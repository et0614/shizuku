using System.IO.BACnet;
using BaCSharp;

namespace Shizuku2.BACnet
{
  /// <summary>BACnet通信でPresent valueを読み書きするクラス</summary>
  public class PresentValueReadWriter
  {

    #region 定数宣言

    /// <summary>DateTimeコントローラのデバイスID</summary>
    private const uint DATETIMECONTROLLER_DEVICE_ID = 1;

    /// <summary>DateTimeコントローラの排他的ポート番号</summary>
    private const int DATETIMECONTROLLER_EXCLUSIVE_PORT = 0xBAC0 + (int)DATETIMECONTROLLER_DEVICE_ID;

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    private enum DateTimeControllerMember
    {
      /// <summary>シミュレーション内の加速された日時</summary>
      CurrentDateTimeInSimulation = 1,
      /// <summary>加速度</summary>
      AccerarationRate = 2,
      /// <summary>現実時間の基準日時</summary>
      BaseRealDateTime = 3,
      /// <summary>シミュレーション内の基準日時</summary>
      BaseAcceleratedDateTime = 4,
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>COV登録済みか否か</summary>
    private bool covSubscribed = false;

    /// <summary>BACnet通信用オブジェクト</summary>
    protected BacnetClient client;

    /// <summary>DateTimeコントローラのIPアドレス</summary>
    private string dtCtrlIP = "127.0.0.1";

    /// <summary>加速度を取得する</summary>
    public int AccelerationRate { get; private set; } = 0;

    /// <summary>現実時間の基準日時を取得する</summary>
    public DateTime BaseRealDateTime { get; private set; } = new DateTime(1999, 1, 1, 0, 0, 0);

    /// <summary>シミュレーション内の基準日時を取得する</summary>
    public DateTime BaseAcceleratedDateTime { get; private set; } = new DateTime(1999, 1, 1, 0, 0, 0);

    /// <summary>シミュレーション内の現在日時を取得する</summary>
    public DateTime CurrentDateTime
    {
      get
      {
        return BaseAcceleratedDateTime.AddSeconds
          ((DateTime.Now - BaseRealDateTime).TotalSeconds * AccelerationRate);
      }
    }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="id">通信に使うBACnet DeviceのID</param>
    public PresentValueReadWriter(uint id)
    {
      client = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, (int)(0xBAC0 + id)));
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>サービスを開始する</summary>
    public void StartService()
    {
      client.Start();
    }

    /// <summary>リソースを解放する</summary>
    public void EndService()
    {
      if (client != null) client.Dispose();
    }

    /// <summary>PresentValueを読み取る</summary>
    /// <typeparam name="T">データの種類</typeparam>
    /// <param name="bacAddress">通信相手のBACnetアドレス</param>
    /// <param name="boType">BACnetオブジェクトタイプ</param>
    /// <param name="instanceNumber">インスタンス番号</param>
    /// <param name="succeeded">読み取り成功の真偽</param>
    /// <returns>読み取ったPresentValue</returns>
    public T? ReadPresentValue<T>(BacnetAddress bacAddress, BacnetObjectTypes boType, uint instanceNumber, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(boType, instanceNumber);

      //日付型の場合には処理が特殊
      if (typeof(T) == typeof(DateTime) && boType == BacnetObjectTypes.OBJECT_DATETIME_VALUE)
      {
        if (client.ReadPropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
        {
          succeeded = true;
          DateTime dt1 = (DateTime)val[0].Value;
          DateTime dt2 = (DateTime)val[1].Value;
          return (T)(object)(new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second));
        }
        succeeded = false;
        return default;
      }
      //その他の型
      else if (client.ReadPropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (T)val[0].Value;
      }
      succeeded = false;
      return default;
    }

    /// <summary>PresentValueを書き込む</summary>
    /// <param name="bacAddress">通信相手のBACnetアドレス</param>
    /// <param name="boType">BACnetオブジェクトタイプ</param>
    /// <param name="instanceNumber">インスタンス番号</param>
    /// <param name="val">書き込むPresentValue</param>
    /// <param name="succeeded">書き込み成功の真偽</param>
    public void WritePresentValue(BacnetAddress bacAddress, BacnetObjectTypes boType, uint instanceNumber,
      BacnetValue val, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(boType, instanceNumber);

      succeeded = client.WritePropertyRequest(
        bacAddress, 
        boID, 
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue> { val }
        );
    }

    #endregion

    #region 現在日時取得関連

    /// <summary>シミュレーション日時の加速度に関するCOVを登録する</summary>
    /// <param name="ipAddress">DateTimeControllerオブジェクトのIPアドレス（xxx.xxx.xxx.xxxの形式）</param>
    /// <returns>登録成功の真偽</returns>
    public bool SubscribeDateTimeCOV(string ipAddress="127.0.0.1")
    {
      if (!covSubscribed)
      {
        dtCtrlIP = ipAddress;
        BacnetAddress bacAddDT = new BacnetAddress(BacnetAddressTypes.IP, ipAddress + ":" + DATETIMECONTROLLER_EXCLUSIVE_PORT.ToString());

        //加速度の変更を監視
        client.OnCOVNotification += Client_OnCOVNotification;
        BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)DateTimeControllerMember.AccerarationRate);
        covSubscribed = client.SubscribeCOVRequest(bacAddDT, boID, (uint)DateTimeControllerMember.AccerarationRate, false, false, 3600);
      }

      //日時を更新
      updateDateTime(3, 100);

      return covSubscribed;
    }

    private void Client_OnCOVNotification(
      BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier,
      BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier,
      uint timeRemaining, bool needConfirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
    {
      //加速度が変化した場合      
      UInt16 port = BitConverter.ToUInt16(new byte[] { adr.adr[5], adr.adr[4] });
      if (
        port == DATETIMECONTROLLER_EXCLUSIVE_PORT &&
        monitoredObjectIdentifier.type == BacnetObjectTypes.OBJECT_ANALOG_OUTPUT &&
        monitoredObjectIdentifier.instance == (uint)DateTimeControllerMember.AccerarationRate)
      {
        updateDateTime(3, 100);
      }
    }

    /// <summary>日時を更新する</summary>
    /// <param name="maxTrial">日時更新の最大試行回数[回]</param>
    /// <param name="trialIntervalMSec">試行間の時間間隔[msec]</param>
    private void updateDateTime(int maxTrial, int trialIntervalMSec)
    {
      BacnetAddress bacAddDT = new BacnetAddress(BacnetAddressTypes.IP, dtCtrlIP + ":" + DATETIMECONTROLLER_EXCLUSIVE_PORT.ToString());

      //加速度を取得
      for (int i = 0; i < maxTrial; i++)
      {
        AccelerationRate = (int)ReadPresentValue<float>(bacAddDT, BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)DateTimeControllerMember.AccerarationRate, out bool suc);
        if (suc) break;
        if (i == maxTrial - 1) throw new Exception("Can't update date time");
        Thread.Sleep(trialIntervalMSec);
      }

      //現実時間の基準日時を取得
      for (int i = 0; i < maxTrial; i++)
      {
        BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)DateTimeControllerMember.BaseRealDateTime);
        if (client.ReadPropertyRequest(bacAddDT, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
        {
          DateTime dt1 = (DateTime)val[0].Value;
          DateTime dt2 = (DateTime)val[1].Value;
          BaseRealDateTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);
          break;
        }
        if (i == maxTrial - 1) throw new Exception("Can't update date time");
        Thread.Sleep(trialIntervalMSec);
      }

      //加速時間の基準日時を取得
      for (int i = 0; i < maxTrial; i++)
      {
        BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)DateTimeControllerMember.BaseAcceleratedDateTime);
        if (client.ReadPropertyRequest(bacAddDT, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
        {
          DateTime dt1 = (DateTime)val[0].Value;
          DateTime dt2 = (DateTime)val[1].Value;
          BaseAcceleratedDateTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);
          break;
        }
        if (i == maxTrial - 1) throw new Exception("Can't update date time");
        Thread.Sleep(trialIntervalMSec);
      }
    }

    #endregion

  }
}
