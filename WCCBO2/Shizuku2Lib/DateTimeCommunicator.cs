using System.IO.BACnet;

namespace Shizuku2.BACnet
{
  /// <summary>Shizuku2のDateTimeControllerとの通信ユーティリティクラス</summary>
  public class DateTimeCommunicator : PresentValueReadWriter
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DATETIME_CONTROLLER_DEVICE_ID = 1;

    /// <summary>排他的ポート番号</summary>
    public const int DATETIME_CONTROLLER_EXCLUSIVE_PORT = 0xBAC0 + (int)DATETIME_CONTROLLER_DEVICE_ID;

    /// <summary>Dummy DeviceのBACnetアドレス</summary>
    private readonly BacnetAddress bacAddress;

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    private enum MemberNumber
    {
      /// <summary>現在のシミュレーション上の日時</summary>
      CurrentDateTimeInSimulation = 1,
      /// <summary>加速度</summary>
      AccelerationRate = 2,
      /// <summary>現実時間の基準日時</summary>
      BaseRealDateTime = 3,
      /// <summary>シミュレーション上の基準日時</summary>
      BaseAcceleratedDateTime = 4,
      /// <summary>シミュレーション上の終了日時</summary>
      EndDateTime = 5,
      /// <summary>計算遅延中か否か</summary>
      IsDelayed = 6,
      /// <summary>計算完了済か否か</summary>
      IsFinished = 7,
      /// <summary>一時停止までの秒数</summary>
      PauseTimer = 8,
      /// <summary>一時停止中か否か</summary>
      IsPaused = 9
    }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="id">通信に使うBACnet DeviceのID</param>
    /// <param name="name">通信に使うBACnet Deviceの名前</param>
    /// <param name="ipAddress">エミュレータのIPアドレス（「xxx.xxx.xxx.xxx」の形式）</param>
    public DateTimeCommunicator(uint id, string name = "anoymous device", string ipAddress = "127.0.0.1")
      : base(id)
    {
      bacAddress = new BacnetAddress(BacnetAddressTypes.IP, ipAddress + ":" + DATETIME_CONTROLLER_EXCLUSIVE_PORT.ToString());
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>COV登録済みか否か</summary>
    private bool covSubscribed = false;

    /// <summary>現実時間の基準日時</summary>
    private DateTime baseRealDateTime = new DateTime(1999, 1, 1, 0, 0, 0);

    /// <summary>シミュレーション上の基準日時</summary>
    private DateTime baseAcceleratedDateTime = new DateTime(1999, 1, 1, 0, 0, 0);

    /// <summary>加速度[-]</summary>
    private int accelerationRate = 0;

    /// <summary>シミュレーション上の現在日時を取得する</summary>
    /// <remarks>
    /// 正しい値を取得するには予めSyncDateTime()で時刻を同期させるか、SubscribeDateTimeCOV()メソッドでCOV通知を受けるようにしておく必要がある
    /// </remarks>
    public DateTime CurrentDateTime
    {
      get
      {
        //一時停止中は初期日時
        if (IsPaused) return baseAcceleratedDateTime;
        //計算中は日時を計算
        else return baseAcceleratedDateTime.AddSeconds
          ((DateTime.Now - baseRealDateTime).TotalSeconds * accelerationRate);
      }
    }

    /// <summary>一時停止中か否かを取得する</summary>
    public bool IsPaused { get; private set; } = false;

    #endregion

    #region 現在日時取得関連

    /// <summary>シミュレーション日時に関するCOVを登録する</summary>
    /// <returns>登録成功の真偽</returns>
    public bool SubscribeDateTimeCOV()
    {
      if (!covSubscribed)
      {
        //加速度の変更を監視
        BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber.AccelerationRate);
        bool success = client.SubscribeCOVRequest(bacAddress, boID, (uint)MemberNumber.AccelerationRate, false, false, 3600);
        if (!success) return false;

        //一時停止を監視
        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)MemberNumber.IsPaused);
        success = client.SubscribeCOVRequest(bacAddress, boID, (uint)MemberNumber.IsPaused, false, false, 3600);
        if (!success) return false;

        //COVイベント登録
        covSubscribed = true;
        client.OnCOVNotification += Client_OnCOVNotification;
      }

      //日時を更新
      SyncDateTime();

      return true;
    }

    private void Client_OnCOVNotification(
      BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier,
      BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier,
      uint timeRemaining, bool needConfirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
    {
      //加速度が変化した場合
      bool hasChanged = false;
      UInt16 port = BitConverter.ToUInt16(new byte[] { adr.adr[5], adr.adr[4] });
      if (
        port == DATETIME_CONTROLLER_EXCLUSIVE_PORT &&
        monitoredObjectIdentifier.type == BacnetObjectTypes.OBJECT_ANALOG_OUTPUT &&
        monitoredObjectIdentifier.instance == (uint)MemberNumber.AccelerationRate)
      {
        //変更後の加速度を取得
        foreach (BacnetPropertyValue vl in values)
        {
          if (vl.property.GetPropertyId() == BacnetPropertyIds.PROP_PRESENT_VALUE)
          {
            accelerationRate = (int)((float)vl.value[0].Value);
            break;
          }
        }

        hasChanged = true;
      }

      //一時停止状態が変化した場合
      else if (
        port == DATETIME_CONTROLLER_EXCLUSIVE_PORT &&
        monitoredObjectIdentifier.type == BacnetObjectTypes.OBJECT_BINARY_INPUT &&
        monitoredObjectIdentifier.instance == (uint)MemberNumber.IsPaused)
      {
        foreach (BacnetPropertyValue vl in values)
        {
          if (vl.property.GetPropertyId() == BacnetPropertyIds.PROP_PRESENT_VALUE)
          {
            hasChanged = true;
            IsPaused = (uint)vl.value[0].Value == 1u;
            break;
          }
        }
      }

      //変更があった場合には基準日時を更新
      if (hasChanged)
      {
        //現実時間の基準日時を取得
        for (int i = 0; i < 3; i++)
        {
          baseRealDateTime = getBaseRealDateTime(out bool suc);
          if (suc) break;
          Thread.Sleep(100);
        }

        //加速時間の基準日時を取得
        for (int i = 0; i < 3; i++)
        {
          baseAcceleratedDateTime = getBaseAcceleratedDateTime(out bool suc);
          if (suc) break;
          Thread.Sleep(100);
        }
      }
    }

    /// <summary>日時を同期させる</summary>
    /// <returns>同期が成功したか否か</returns>
    public bool SyncDateTime()
    {
      bool suc;

      //一時停止の情報を取得
      bool isp = GetIsPaused(out suc);
      if (!suc) return false;

      //加速度を取得
      int acc = GetAccelerationRate(out suc);
      if (!suc) return false;

      //現実時間の基準日時を取得
      DateTime bdt = getBaseRealDateTime(out suc);
      if (!suc) return false;

      //加速時間の基準日時を取得
      DateTime adt = getBaseAcceleratedDateTime(out suc);
      if (!suc) return false;

      IsPaused = isp;
      accelerationRate = acc;
      baseRealDateTime = bdt;
      baseAcceleratedDateTime = adt;
      return true;
    }

    #endregion

    #region 加速度関連の処理

    /// <summary>加速度[-]を変える</summary>
    /// <param name="accelerationRate">加速度[-]</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangeAccelerationRate
      (int accelerationRate, out bool succeeded)
    {
      WritePresentValue(bacAddress,
        BacnetObjectTypes.OBJECT_ANALOG_OUTPUT,
        (uint)MemberNumber.AccelerationRate,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)accelerationRate),
        out succeeded);
    }

    /// <summary>加速度[-]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>加速度[-]</returns>
    public int GetAccelerationRate(out bool succeeded)
    {
      return (int)ReadPresentValue<float>(bacAddress,
        BacnetObjectTypes.OBJECT_ANALOG_OUTPUT,
        (uint)MemberNumber.AccelerationRate,
        out succeeded);
    }

    /// <summary>現実時間の基準日時を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>現実時間の基準日時</returns>
    private DateTime getBaseRealDateTime(out bool succeeded)
    {
      return ReadPresentValue<DateTime>(bacAddress,
        BacnetObjectTypes.OBJECT_DATETIME_VALUE,
        (uint)MemberNumber.BaseRealDateTime,
        out succeeded);
    }

    /// <summary>シミュレーション上の基準日時を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>シミュレーション上の基準日時</returns>
    private DateTime getBaseAcceleratedDateTime(out bool succeeded)
    {
      return ReadPresentValue<DateTime>(bacAddress,
        BacnetObjectTypes.OBJECT_DATETIME_VALUE,
        (uint)MemberNumber.BaseAcceleratedDateTime,
        out succeeded);
    }

    #endregion

    #region 一時停止関連の処理

    /// <summary>一時停止中か否かを取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>一時停止中か否か</returns>
    public bool GetIsPaused(out bool succeeded)
    {
      return 1 == ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)MemberNumber.IsPaused, out succeeded);
    }

    /// <summary>一時停止までの秒数[sec]を変える</summary>
    /// <param name="pauseTimer">一時停止までの秒数[sec]</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangePauseTimer
      (int pauseTimer, out bool succeeded)
    {
      WritePresentValue(bacAddress,
        BacnetObjectTypes.OBJECT_ANALOG_OUTPUT,
        (uint)MemberNumber.PauseTimer,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)pauseTimer),
        out succeeded);
    }

    /// <summary>一時停止までの秒数[sec]を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>一時停止までの秒数[sec]</returns>
    public int GetPauseTimer(out bool succeeded)
    {
      return (int)ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber.PauseTimer, out succeeded);
    }

    #endregion

    #region その他の情報取得

    /// <summary>計算を終えるシミュレーション上の日時を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>計算を終えるシミュレーション上の日時</returns>
    public DateTime GetEndDateTime(out bool succeeded)
    {
      return ReadPresentValue<DateTime>(bacAddress,
        BacnetObjectTypes.OBJECT_DATETIME_VALUE,
        (uint)MemberNumber.EndDateTime,
        out succeeded);
    }

    /// <summary>計算が遅延しているか否かを取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>計算が遅延しているか否か</returns>
    public bool GetIsDelayed(out bool succeeded)
    {
      return 1 == ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)MemberNumber.IsDelayed, out succeeded);
    }

    /// <summary>計算が終了済か否かを取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>計算が終了済か否か</returns>
    public bool GetIsFinished(out bool succeeded)
    {
      return 1 == ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)MemberNumber.IsFinished, out succeeded);
    }

    #endregion

  }
}
