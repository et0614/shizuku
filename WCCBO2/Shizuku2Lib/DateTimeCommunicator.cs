using System.IO.BACnet;
using System.IO.BACnet.Base;
using BaCSharp;

namespace Shizuku2.BACnet
{
  /// <summary>Shizuku2のDateTimeコントローラとの通信ユーティリティクラス</summary>
  public class DateTimeCommunicator
  {

    #region 定数宣言

    /// <summary>DateTimeコントローラのデバイスID</summary>
    public const uint DATETIMECONTROLLER_DEVICE_ID = 1;

    /// <summary>DateTimeコントローラの排他的ポート番号</summary>
    public const int DATETIMECONTROLLER_EXCLUSIVE_PORT = 0xBAC0 + (int)DATETIMECONTROLLER_DEVICE_ID;

    /// <summary>VRFコントローラのBACnetアドレス</summary>
    private readonly BacnetAddress bacAddress;

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    public enum DateTimeControllerMember
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

    /// <summary>BACnet通信用オブジェクト</summary>
    private BACnetCommunicator communicator;

    private DateTimeAccelerator dtAcc = new DateTimeAccelerator(0, DateTime.Now);

    /// <summary>日時初期化の真偽を取得する</summary>
    public bool DateTimeInitialized { get; private set; } = false; 

    /// <summary>シミュレーション内の現在時刻を取得する</summary>
    public DateTime CurrentDateTimeInSimulation { get { return dtAcc.AcceleratedDateTime; } }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="id">通信に使うBACnet DeviceのID</param>
    /// <param name="name">通信に使うBACnet Deviceの名前</param>
    /// <param name="description">通信に使うBACnet Deviceの説明</param>
    /// <param name="ipAddress">DateTimeコントローラのIPアドレス（「xxx.xxx.xxx.xxx」の形式）</param>
    public DateTimeCommunicator(uint id, string name, string description, string ipAddress = "127.0.0.1")
    {
      DeviceObject dObject = new DeviceObject(id, name, description, true);
      communicator = new BACnetCommunicator(dObject, (int)(0xBAC0 + id));
      bacAddress = new BacnetAddress(BacnetAddressTypes.IP, ipAddress + ":" + DATETIMECONTROLLER_EXCLUSIVE_PORT.ToString());
      communicator.StartService();

      //加速度の変更を監視
      communicator.Client.OnCOVNotification += Client_OnCOVNotification;
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)DateTimeControllerMember.AccerarationRate);
      communicator.Client.SubscribeCOVRequest(bacAddress, boID, (uint)DateTimeControllerMember.AccerarationRate, false, false, 3600);

      //Who is送信
      communicator.Client.WhoIs();
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>加速度を変更する</summary>
    /// <param name="accRate">加速度</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangeAccerarationRate(double accRate, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)DateTimeControllerMember.AccerarationRate);

      succeeded = communicator.Client.WritePropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE, accRate) }
        );
    }

    /// <summary>加速度を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>加速度</returns>
    public double GetAccerarationRate(out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)DateTimeControllerMember.AccerarationRate);

      if (communicator.Client.ReadPropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (double)val[0].Value;
      }

      succeeded = false;
      return 0;
    }

    #endregion

    #region COVイベント対応処理


    private void Client_OnCOVNotification
      (BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier, 
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
        //この処理は汚いが・・・
        foreach (BacnetPropertyValue value in values)
        {
          if (value.property.propertyIdentifier == (uint)BacnetPropertyIds.PROP_PRESENT_VALUE)
          {
            int acc = (int)value.value[0].Value;

            BacnetObjectId boID;
            //基準日時（加速時間）
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)DateTimeControllerMember.BaseAcceleratedDateTime);
            if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val1))
            {
              DateTime dt1 = (DateTime)val1[0].Value;
              DateTime dt2 = (DateTime)val1[1].Value;
              DateTime bAccDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

              //基準日時（現実時間）
              boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)DateTimeControllerMember.BaseRealDateTime);
              if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val2))
              {
                dt1 = (DateTime)val2[0].Value;
                dt2 = (DateTime)val2[1].Value;
                DateTime bRealDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

                //初期化
                dtAcc.InitDateTime(acc, bRealDTime, bAccDTime);
                DateTimeInitialized = true;
              }
            }

            break;
          }
        }
      }
    }
    
    #endregion

  }
}
