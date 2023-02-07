using BaCSharp;
using System.IO.BACnet;

namespace Shizuku2
{
  public abstract class AbstractBACnetDevice
  {

    #region Fixメンバ

    /// <summary>BACnet Device</summary>
    protected DeviceObject? dObject;

    /// <summary>BACnetClient</summary>
    [NonSerialized]
    private BacnetClient? client;

    /// <summary>BACnet Device IDを取得する</summary>
    public uint DeviceID { get; protected set; }

    /// <summary>ポート番号を取得する</summary>
    public int PortNumber { get; private set; }

    /// <summary>DeviceIDとポート番号対応表を作成する</summary>
    /// <param name="devicePortNumberTable">DeviceIDとポート番号対応表</param>
    internal void makePortNumberTable(ref Dictionary<uint, int> devicePortNumberTable)
    {
      devicePortNumberTable.Add(DeviceID, PortNumber);
    }

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
    protected static bool convertToBool(object obj)
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

    #region abstract / virtualメンバ

    /// <summary>サービスを開始する</summary>
    public void StartService()
    {
      //UDPで通信
      BacnetIpUdpProtocolTransport bUDP = new BacnetIpUdpProtocolTransport(0xBAC0, false);
      client = new BacnetClient(bUDP);

      //イベント登録//当面はIam,WhoIs,WhoHasは使わないが、遅くなるだけか？？？
      client.OnIam += client_OnIam;
      client.OnWhoIs += client_OnWhoIs;
      client.OnWhoHas += client_OnWhoHas; ;
      client.OnReadPropertyRequest += client_OnReadPropertyRequest;
      client.OnReadPropertyMultipleRequest += client_OnReadPropertyMultipleRequest;
      client.OnWritePropertyRequest += client_OnWritePropertyRequest;

      //サーバー開始,ポート登録
      client.Start();
      PortNumber = bUDP.ExclusivePort;
    }

    /// <summary>リソースを解放する</summary>
    public void EndService()
    {
      if (dObject != null) dObject.Dispose();
      if (client != null) client.Dispose();
    }

    /// <summary>制御値を機器やセンサに反映する</summary>
    public abstract void ApplyManipulatedVariables();

    /// <summary>機器やセンサの検出値を取得する</summary>
    public abstract void ReadMeasuredValues();

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
      dObject.ReceivedIam(sender, adr, device_id);
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
        o = dObject.FindBacnetObject(objIdNotNull);
        if (o != null)
          sender.IHave(dObject.m_PROP_OBJECT_IDENTIFIER, objIdNotNull, o.m_PROP_OBJECT_NAME);
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
      lock (dObject)
      {
        BaCSharpObject bacobj = dObject.FindBacnetObject(object_id);

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
      lock (dObject)
      {
        BaCSharpObject bacobj = dObject.FindBacnetObject(object_id);
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
      lock (dObject)
      {
        try
        {
          IList<BacnetPropertyValue> value;
          List<BacnetReadAccessResult> values = new List<BacnetReadAccessResult>();
          foreach (BacnetReadAccessSpecification p in properties)
          {
            if (p.propertyReferences.Count == 1 && p.propertyReferences[0].propertyIdentifier == (uint)BacnetPropertyIds.PROP_ALL)
            {
              BaCSharpObject bacobj = dObject.FindBacnetObject(p.objectIdentifier);
              if (!bacobj.ReadPropertyAll(sender, adr, out value))
              {
                sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROP_MULTIPLE, invoke_id, BacnetErrorClasses.ERROR_CLASS_OBJECT, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT);
                return;
              }
            }
            else
            {
              BaCSharpObject bacobj = dObject.FindBacnetObject(p.objectIdentifier);
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
