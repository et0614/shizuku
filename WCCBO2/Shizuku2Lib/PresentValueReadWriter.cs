using System.IO.BACnet;

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

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    protected BacnetClient client;

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

  }
}
