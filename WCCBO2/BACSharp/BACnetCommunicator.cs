using BaCSharp;
using System.Diagnostics;
using System.IO.BACnet;

namespace BaCSharp
{
  /// <summary>BACnetで通信するオブジェクト</summary>
  public class BACnetCommunicator
  {

    #region インスタンス変数・プロパティの定義

    /// <summary>BACnetClient</summary>
    [NonSerialized]
    public BacnetClient Client;

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

    /// <summary>COV通知の登録リスト</summary>
    private Dictionary<BacnetObjectId, List<Subscription>> m_subscriptions = new Dictionary<BacnetObjectId, List<Subscription>>();

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
      Client = new BacnetClient(bUDP);

      //イベント登録//当面はIam,WhoIs,WhoHasは使わないが、遅くなるだけか？？？
      Client.OnIam += client_OnIam;
      Client.OnWhoIs += client_OnWhoIs;
      Client.OnWhoHas += client_OnWhoHas; ;
      Client.OnReadPropertyRequest += client_OnReadPropertyRequest;
      Client.OnReadPropertyMultipleRequest += client_OnReadPropertyMultipleRequest;
      Client.OnWritePropertyRequest += client_OnWritePropertyRequest;
      Client.OnWritePropertyMultipleRequest += Client_OnWritePropertyMultipleRequest;
      Client.OnSubscribeCOV += Client_OnSubscribeCOV;

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
      Client.Start();
    }

    /// <summary>リソースを解放する</summary>
    public void EndService()
    {
      if (BACnetDevice != null) BACnetDevice.Dispose();
      if (Client != null) Client.Dispose();
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
            //COV通知はこれで良いのか？？？2023.05.17
            sendChangeOfValue(object_id, value.property.GetPropertyId(), value.property.propertyArrayIndex, value.value);
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

    /// <summary>WritePeopertyMultipleRequest発生時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="adr"></param>
    /// <param name="invoke_id"></param>
    /// <param name="object_id"></param>
    /// <param name="values"></param>
    /// <param name="maxSegments"></param>
    /// <remarks>このメソッドは動作を十分に検証できていない</remarks>
    private void Client_OnWritePropertyMultipleRequest
      (BacnetClient sender, BacnetAddress adr, byte invoke_id, BacnetObjectId object_id, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
    {
      lock (BACnetDevice)
      {
        BaCSharpObject bacobj = BACnetDevice.FindBacnetObject(object_id);
        if (bacobj != null)
        {
          foreach (BacnetPropertyValue value in values)
          {
            ErrorCodes error = bacobj.WritePropertyValue(sender, adr, value, true);
            if (error == ErrorCodes.Good)
            {
              sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, invoke_id);
              //COV通知はこれで良いのか？？？2023.05.17
              sendChangeOfValue(object_id, value.property.GetPropertyId(), value.property.propertyArrayIndex, value.value);
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
        }
        else
          sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, invoke_id, BacnetErrorClasses.ERROR_CLASS_OBJECT, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT);
      }
    }

    #endregion

    #region COV関連の処理

    /// <summary>COV(Change Of Value)登録時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="adr"></param>
    /// <param name="invokeId"></param>
    /// <param name="subscriberProcessIdentifier"></param>
    /// <param name="monitoredObjectIdentifier"></param>
    /// <param name="cancellationRequest"></param>
    /// <param name="issueConfirmedNotifications"></param>
    /// <param name="lifetime"></param>
    /// <param name="maxSegments"></param>
    private void Client_OnSubscribeCOV
      (BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier,
      BacnetObjectId monitoredObjectIdentifier, bool cancellationRequest, bool issueConfirmedNotifications, uint lifetime, BacnetMaxSegments maxSegments)
    {
      lock (BACnetDevice)
      {
        try
        {
          //create
          Subscription sub = HandleSubscriptionRequest
            (sender, adr, invokeId, subscriberProcessIdentifier, monitoredObjectIdentifier, (uint)BacnetPropertyIds.PROP_ALL, cancellationRequest, issueConfirmedNotifications, lifetime, 0);

          //send confirm
          sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_SUBSCRIBE_COV, invokeId);

          //also send first values
          if (!cancellationRequest)
          {
            ThreadPool.QueueUserWorkItem((o) =>
            {
              BaCSharpObject bacobj = BACnetDevice.FindBacnetObject(sub.monitoredObjectIdentifier);
              IList<BacnetPropertyValue> values;
              if (bacobj.ReadPropertyAll(sender, adr, out values))
              {
                if (!sender.Notify(adr, sub.subscriberProcessIdentifier, DeviceID, sub.monitoredObjectIdentifier, (uint)sub.GetTimeRemaining(), sub.issueConfirmedNotifications, values))
                {
                  Trace.TraceError("Couldn't send notify");
                }
              }
            }, null);
          }
        }
        catch (Exception)
        {
          sender.ErrorResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_SUBSCRIBE_COV, invokeId, BacnetErrorClasses.ERROR_CLASS_DEVICE, BacnetErrorCodes.ERROR_CODE_OTHER);
        }
      }
    }

    private Subscription HandleSubscriptionRequest(BacnetClient sender, BacnetAddress adr, byte invoke_id, uint subscriberProcessIdentifier, BacnetObjectId monitoredObjectIdentifier, uint property_id, bool cancellationRequest, bool issueConfirmedNotifications, uint lifetime, float covIncrement)
    {
      //remove old leftovers
      RemoveOldSubscriptions();

      //find existing
      List<Subscription> subs = null;
      Subscription sub = null;
      if (m_subscriptions.ContainsKey(monitoredObjectIdentifier))
      {
        subs = m_subscriptions[monitoredObjectIdentifier];
        foreach (Subscription s in subs)
        {
          // Modif FC
          if (s.reciever.Equals(sender) && s.reciever_address.Equals(adr) && s.monitoredObjectIdentifier.Equals(monitoredObjectIdentifier) && s.monitoredProperty.propertyIdentifier == property_id)
          {
            sub = s;
            break;
          }
        }
      }

      //cancel
      if (cancellationRequest && sub != null)
      {
        subs.Remove(sub);
        if (subs.Count == 0)
          m_subscriptions.Remove(sub.monitoredObjectIdentifier);

        //send confirm
        // F. Chaxel : a supprimer, c'est fait par l'appellant
        sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_SUBSCRIBE_COV, invoke_id);

        return null;
      }

      //create if needed
      if (sub == null)
      {
        sub = new Subscription(sender, adr, subscriberProcessIdentifier, monitoredObjectIdentifier, new BacnetPropertyReference((uint)BacnetPropertyIds.PROP_ALL, System.IO.BACnet.Serialize.ASN1.BACNET_ARRAY_ALL), issueConfirmedNotifications, lifetime, covIncrement);

        if (subs == null)
        {
          subs = new List<Subscription>();
          m_subscriptions.Add(sub.monitoredObjectIdentifier, subs);
        }
        subs.Add(sub);
      }

      //update perhaps
      sub.issueConfirmedNotifications = issueConfirmedNotifications;
      sub.lifetime = lifetime;
      sub.start = DateTime.Now;

      return sub;
    }

    private void RemoveOldSubscriptions()
    {
      LinkedList<BacnetObjectId> to_be_deleted = new LinkedList<BacnetObjectId>();
      foreach (KeyValuePair<BacnetObjectId, List<Subscription>> entry in m_subscriptions)
      {
        for (int i = 0; i < entry.Value.Count; i++)
        {
          // Modif F. Chaxel <0 modifié == 0
          if (entry.Value[i].GetTimeRemaining() < 0)
          {
            entry.Value.RemoveAt(i);
            i--;
          }
        }
        if (entry.Value.Count == 0)
          to_be_deleted.AddLast(entry.Key);
      }
      foreach (BacnetObjectId obj_id in to_be_deleted)
        m_subscriptions.Remove(obj_id);
    }

    /// <summary>COV発生時に通知する</summary>
    /// <param name="object_id"></param>
    /// <param name="property_id"></param>
    /// <param name="array_index"></param>
    /// <param name="value"></param>
    private void sendChangeOfValue(BacnetObjectId object_id, BacnetPropertyIds property_id, uint array_index, IList<BacnetValue> value)
    {
      ThreadPool.QueueUserWorkItem((o) =>
      {
        lock (BACnetDevice)
        {
          //remove old leftovers
          RemoveOldSubscriptions();

          //find subscription
          if (!m_subscriptions.ContainsKey(object_id)) return;
          List<Subscription> subs = m_subscriptions[object_id];

          //convert
          List<BacnetPropertyValue> values = new List<BacnetPropertyValue>();
          BacnetPropertyValue tmp = new BacnetPropertyValue();
          tmp.property = new BacnetPropertyReference((uint)property_id, array_index);
          tmp.value = value;
          values.Add(tmp);

          //send to all
          foreach (Subscription sub in subs)
          {
            if (sub.monitoredProperty.propertyIdentifier == (uint)BacnetPropertyIds.PROP_ALL || sub.monitoredProperty.propertyIdentifier == (uint)property_id)
            {
              //send notify
              if (!sub.reciever.Notify(sub.reciever_address, sub.subscriberProcessIdentifier,　DeviceID, sub.monitoredObjectIdentifier, (uint)sub.GetTimeRemaining(), sub.issueConfirmedNotifications, values))
                Trace.TraceError("Couldn't send notify");
            }
          }
        }
      }, null);
    }

    #region インナークラスの定義

    private class Subscription
    {
      public BacnetClient reciever;
      public BacnetAddress reciever_address;
      public uint subscriberProcessIdentifier;
      public BacnetObjectId monitoredObjectIdentifier;
      public BacnetPropertyReference monitoredProperty;
      public bool issueConfirmedNotifications;
      public uint lifetime;
      public DateTime start;
      public float covIncrement;
      public Subscription(BacnetClient reciever, BacnetAddress reciever_address, uint subscriberProcessIdentifier, BacnetObjectId monitoredObjectIdentifier, BacnetPropertyReference property, bool issueConfirmedNotifications, uint lifetime, float covIncrement)
      {
        this.reciever = reciever;
        this.reciever_address = reciever_address;
        this.subscriberProcessIdentifier = subscriberProcessIdentifier;
        this.monitoredObjectIdentifier = monitoredObjectIdentifier;
        this.monitoredProperty = property;
        this.issueConfirmedNotifications = issueConfirmedNotifications;
        this.lifetime = lifetime;
        this.start = DateTime.Now;
        this.covIncrement = covIncrement;
      }
      public int GetTimeRemaining()
      {

        if (lifetime == 0) return 0;

        uint elapse = (uint)(DateTime.Now - start).TotalSeconds;

        if (lifetime > elapse)
          return (int)(lifetime - elapse);
        else

          return -1;

      }
    }

    #endregion

    #endregion

    #region 補助関数

    /// <summary>object型をbool値に変換する</summary>
    /// <param name="obj">object型変数</param>
    /// <returns>bool値</returns>
    public static bool ConvertToBool(object obj)
    {
      if (obj is uint) return ((uint)obj == 1);
      else if (obj is int) return ((int)obj == 1);
      else if (obj is bool) return (bool)obj;
      else return false;
    }

    #endregion

  }
}
