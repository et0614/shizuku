using BaCSharp;
using System.IO.BACnet;

namespace Shizuku2
{
  /// <summary>BACnetで通信するオブジェクト</summary>
  public class BACnetCommunicator
  {

    #region インスタンス変数・プロパティの定義

    /// <summary>BACnetClient</summary>
    [NonSerialized]
    private BacnetClient client;

    /// <summary>BACnetDeviceを取得する</summary>
    public DeviceObject BACnetDevice { get; private set; }

    /// <summary>BACnet Device IDを取得する</summary>
    public uint DeviceID { get { return BACnetDevice.PROP_OBJECT_IDENTIFIER.instance; } }

    /// <summary>BACnet DeviceのDeviceIDとポート番号対応表を取得する</summary>
    public static Dictionary<uint, int> BACnetDevicePortList
    {
      get;
      private set;
    } = new Dictionary<uint, int>();

    #region 使うかわからない機能

    /// <summary>Priority 8で書き込む</summary>
    /// <param name="newVal">新しい値</param>
    /// <param name="bObj">書き込むBACnetオブジェクト</param>
    public static Type WriteValueWithPriority8<Type>(Type newVal, AnalogOutput<Type> bObj)
    {
      BacnetPropertyValue bpv = new BacnetPropertyValue();
      bpv.priority = 8;
      bpv.property = new BacnetPropertyReference((uint)BacnetPropertyIds.PROP_PRESENT_VALUE, 0);
      bpv.value = new List<BacnetValue>() { new BacnetValue(newVal) };
      bObj.WritePropertyValue(bpv, false);
      return bObj.m_PROP_PRESENT_VALUE;
    }

    /// <summary>Priority 8で書き込む</summary>
    /// <param name="newVal">新しい値</param>
    /// <param name="bObj">書き込むBACnetオブジェクト</param>
    public static bool WriteValueWithPriority8(bool newVal, BinaryOutput bObj)
    {
      BacnetPropertyValue bpv = new BacnetPropertyValue();
      bpv.priority = 8;
      bpv.property = new BacnetPropertyReference((uint)BacnetPropertyIds.PROP_PRESENT_VALUE, 0);
      bpv.value = new List<BacnetValue>() { new BacnetValue(newVal ? (uint)1 : (uint)0) };
      bObj.WritePropertyValue(bpv, false);
      return bObj.m_PROP_PRESENT_VALUE == 1;
    }

    /// <summary>object型をbool値に変換する</summary>
    /// <param name="obj">object型変数</param>
    /// <returns>bool値</returns>
    public static bool ConvertToBool(object obj)
    {
      if (obj is int) return ((int)obj == 1);
      else if (obj is bool) return (bool)obj;
      else return false;
    }

    /// <summary>日時がカレンダーに含まれるか否か</summary>
    /// <param name="entry">カレンダエントリ</param>
    /// <param name="dTime">日時</param>
    /// <returns>日時がカレンダーに含まれるか否か</returns>
    protected static bool isAFittingDate(BACnetCalendarEntry entry, DateTime dTime)
    {
      foreach (object ent in entry.Entries)
      {
        if (ent is BacnetDate)
          if (((BacnetDate)ent).IsAFittingDate(dTime)) return true;
        if (ent is BacnetDateRange)
          if (((BacnetDateRange)ent).IsAFittingDate(dTime)) return true;
        if (ent is BacnetweekNDay)
          if (((BacnetweekNDay)ent).IsAFittingDate(dTime)) return true;
      }
      return false;
    }

    #endregion

    #endregion

    #region コンストラクタ

    public BACnetCommunicator
      (DeviceObject device, int exclusivePort)
    {
      this.BACnetDevice = device;

      BacnetIpUdpProtocolTransport bUDP = new BacnetIpUdpProtocolTransport(0xBAC0, exclusivePort);
      client = new BacnetClient(bUDP);

      //イベント登録//当面はIam,WhoIs,WhoHasは使わないが、遅くなるだけか？？？
      client.OnIam += client_OnIam;
      client.OnWhoIs += client_OnWhoIs;
      client.OnWhoHas += client_OnWhoHas; ;
      client.OnReadPropertyRequest += client_OnReadPropertyRequest;
      client.OnReadPropertyMultipleRequest += client_OnReadPropertyMultipleRequest;
      client.OnWritePropertyRequest += client_OnWritePropertyRequest;

      //Device IDとポート番号対応表に追加
      if (BACnetDevicePortList.ContainsKey(DeviceID)) BACnetDevicePortList[DeviceID] = exclusivePort;
      else BACnetDevicePortList.Add(DeviceID, exclusivePort);
    }

    #endregion

    #region BACnet通信開始/終了処理

    /// <summary>サービスを開始する</summary>
    public void StartService()
    {
      //サーバー開始,ポート登録
      client.Start();
    }

    /// <summary>リソースを解放する</summary>
    public void EndService()
    {
      if (BACnetDevice != null) BACnetDevice.Dispose();
      if (client != null) client.Dispose();
    }

    #endregion

    #region イベント発生時の処理

    /// <summary>IamRequest発生時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="adr"></param>
    /// <param name="device_id"></param>
    /// <param name="max_apdu"></param>
    /// <param name="segmentation"></param>
    /// <param name="vendor_id"></param>
    private void client_OnIam(BacnetClient sender, BacnetAddress adr, uint device_id, uint max_apdu, BacnetSegmentations segmentation, ushort vendor_id)
    {
      BACnetDevice.ReceivedIam(sender, adr, device_id);
    }

    /// <summary>WhoHas発生時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="adr"></param>
    /// <param name="lowLimit"></param>
    /// <param name="highLimit"></param>
    /// <param name="objId"></param>
    /// <param name="objName"></param>
    private void client_OnWhoHas(BacnetClient sender, BacnetAddress adr, int lowLimit, int highLimit, BacnetObjectId? objId, string objName)
    {
      if (lowLimit != -1 && DeviceID < lowLimit) return;
      else if (highLimit != -1 && DeviceID > highLimit) return;

      BaCSharpObject o;

      if (objId != null)
      {
        BacnetObjectId objIdNotNull = (BacnetObjectId)objId;
        o = BACnetDevice.FindBacnetObject(objIdNotNull);
        if (o != null)
          sender.IHave(BACnetDevice.m_PROP_OBJECT_IDENTIFIER, objIdNotNull, o.m_PROP_OBJECT_NAME);
      }      
    }

    /// <summary>WhoIsRequest発生時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="adr">Request発信者のアドレス</param>
    /// <param name="low_limit"></param>
    /// <param name="high_limit"></param>
    private void client_OnWhoIs(BacnetClient sender, BacnetAddress adr, int low_limit, int high_limit)
    {
      if (low_limit != -1 && DeviceID < low_limit) return;
      else if (high_limit != -1 && DeviceID > high_limit) return;
      sender.Iam(DeviceID, BacnetSegmentations.SEGMENTATION_BOTH);
    }

    /// <summary>ReadPropertyRequest発生時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="adr">Request発信者のアドレス</param>
    /// <param name="invoke_id"></param>
    /// <param name="object_id"></param>
    /// <param name="property"></param>
    /// <param name="max_segments"></param>
    private void client_OnReadPropertyRequest
      (BacnetClient sender, BacnetAddress adr, byte invoke_id,
      BacnetObjectId object_id, BacnetPropertyReference property, BacnetMaxSegments max_segments)
    {
      lock (BACnetDevice)
      {
        BaCSharpObject bacobj = BACnetDevice.FindBacnetObject(object_id);

        if (bacobj != null)
        {
          IList<BacnetValue> value;
          ErrorCodes error = bacobj.ReadPropertyValue(sender, adr, property, out value);
          if (error == ErrorCodes.Good)
            sender.ReadPropertyResponse(adr, invoke_id, sender.GetSegmentBuffer(max_segments), object_id, property, value);
          else
              if (error == ErrorCodes.NotExist)
            sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_PROPERTY, BacnetErrorCodes.ERROR_CODE_INVALID_ARRAY_INDEX);
          else
            sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_UNKNOWN_PROPERTY);
        }
        else
          sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_OBJECT, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT);
      }
    }

    /// <summary>WritePropertyRequest発生時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="adr">Request発信者のアドレス</param>
    /// <param name="invoke_id"></param>
    /// <param name="object_id"></param>
    /// <param name="value"></param>
    /// <param name="max_segments"></param>
    private void client_OnWritePropertyRequest
      (BacnetClient sender, BacnetAddress adr, byte invoke_id,
      BacnetObjectId object_id, BacnetPropertyValue value, BacnetMaxSegments max_segments)
    {
      lock (BACnetDevice)
      {
        BaCSharpObject bacobj = BACnetDevice.FindBacnetObject(object_id);
        if (bacobj != null)
        {
          ErrorCodes error = bacobj.WritePropertyValue(sender, adr, value, true);
          if (error == ErrorCodes.Good)
          {
            sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invoke_id);
          }
          else
          {
            BacnetErrorCodes bacEr = BacnetErrorCodes.ERROR_CODE_OTHER;
            if (error == ErrorCodes.WriteAccessDenied)
              bacEr = BacnetErrorCodes.ERROR_CODE_WRITE_ACCESS_DENIED;
            if (error == ErrorCodes.OutOfRange)
              bacEr = BacnetErrorCodes.ERROR_CODE_VALUE_OUT_OF_RANGE;

            sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, bacEr);
          }
        }
        else
          sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_OBJECT, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT);
      }
    }

    /// <summary>ReadPropertyMultipleRequest発生時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="adr"></param>
    /// <param name="invoke_id"></param>
    /// <param name="properties"></param>
    /// <param name="max_segments"></param>
    private void client_OnReadPropertyMultipleRequest
      (BacnetClient sender, BacnetAddress adr, byte invoke_id, IList<BacnetReadAccessSpecification> properties, BacnetMaxSegments max_segments)
    {
      lock (BACnetDevice)
      {
        try
        {
          IList<BacnetPropertyValue> value;
          List<BacnetReadAccessResult> values = new List<BacnetReadAccessResult>();
          foreach (BacnetReadAccessSpecification p in properties)
          {
            if (p.propertyReferences.Count == 1 && p.propertyReferences[0].propertyIdentifier == (uint)BacnetPropertyIds.PROP_ALL)
            {
              BaCSharpObject bacobj = BACnetDevice.FindBacnetObject(p.objectIdentifier);
              if (!bacobj.ReadPropertyAll(sender, adr, out value))
              {
                sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROP_MULTIPLE, invoke_id, BacnetErrorClasses.ERROR_CLASS_OBJECT, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT);
                return;
              }
            }
            else
            {
              BaCSharpObject bacobj = BACnetDevice.FindBacnetObject(p.objectIdentifier);
              bacobj.ReadPropertyMultiple(sender, adr, p.propertyReferences, out value);
            }
            values.Add(new BacnetReadAccessResult(p.objectIdentifier, value));
          }
          sender.ReadPropertyMultipleResponse(adr, invoke_id, sender.GetSegmentBuffer(max_segments), values);
        }
        catch (Exception)
        {
          sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROP_MULTIPLE, invoke_id, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
        }
      }
    }

    #endregion

  }
}
