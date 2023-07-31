using System.IO.BACnet;
using BaCSharp;

namespace AccelerationController
{
  public partial class Form1 : Form
  {

    /// <summary>���g��Device ID</summary>
    const int DEVICE_ID = 7000;

    #region �����x����R���g���[���iDateTimeController�j�̏��

    /// <summary>�����x����R���g���[����BACnet Device ID</summary>
    const int DTCTRL_DEVICE_ID = 1;

    /// <summary>�����x����R���g���[����Exclusive�|�[�g</summary>
    const int DTCTRL_EXPORT = 0xBAC0 + 1;

    /// <summary>�����x����R���g���[����Meber Number</summary>
    public enum MemberNumber
    {
      CurrentDateTimeInSimulation = 1,
      AccerarationRate = 2,
      BaseRealDateTime = 3,
      BaseAcceleratedDateTime = 4,
    }

    #endregion

    private BACnetCommunicator communicator;

    DateTimeAccelerator dtAcc = new DateTimeAccelerator(60, DateTime.Now);

    public Form1()
    {
      InitializeComponent();

      DeviceObject dObject = new DeviceObject(DEVICE_ID, "Sample controller controlling the speed of emulator.", "Sample controller controlling the speed of emulator.", true);
      communicator = new BACnetCommunicator(dObject, 0xBAC0 + DEVICE_ID);

      communicator.StartService();
      //COV�ʍ��o�^����
      communicator.Client.OnCOVNotification += Client_OnCOVNotification;
      BacnetAddress bacAddress = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DTCTRL_EXPORT.ToString());
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber.AccerarationRate);
      communicator.Client.SubscribeCOVRequest(bacAddress, boID, (uint)MemberNumber.AccerarationRate, false, false, 3600);

      //Who is���M
      communicator.Client.WhoIs();

      //�ʃX���b�h�Ŏ����\�����X�V
      Task.Run(() =>
      {
        while (true)
        {
          this.Invoke((MethodInvoker)(() =>
          {
            this.lbl_dateTime.Text = dtAcc.AcceleratedDateTime.ToString("yyyy/MM/dd HH:mm:ss");
          }));

          Thread.Sleep(50);
        }
      });

    }

    private void Client_OnCOVNotification(BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier, BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier, uint timeRemaining, bool needConfirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
    {
      //�����x���ω������ꍇ      
      UInt16 port = BitConverter.ToUInt16(new byte[] { adr.adr[5], adr.adr[4] });
      if (
        port == DTCTRL_EXPORT &&
        monitoredObjectIdentifier.type == BacnetObjectTypes.OBJECT_ANALOG_OUTPUT &&
        monitoredObjectIdentifier.instance == (uint)MemberNumber.AccerarationRate)
      {
        //���̏����͉������E�E�E
        foreach (BacnetPropertyValue value in values)
        {
          if (value.property.propertyIdentifier == (uint)BacnetPropertyIds.PROP_PRESENT_VALUE)
          {
            int acc = (int)value.value[0].Value;

            BacnetObjectId boID;
            //������i�������ԁj
            //adr = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DTCTRL_PORT.ToString());
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber.BaseAcceleratedDateTime);
            if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val1))
            {
              DateTime dt1 = (DateTime)val1[0].Value;
              DateTime dt2 = (DateTime)val1[1].Value;
              DateTime bAccDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

              //������i�������ԁj
              boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber.BaseRealDateTime);
              if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val2))
              {
                dt1 = (DateTime)val2[0].Value;
                dt2 = (DateTime)val2[1].Value;
                DateTime bRealDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

                //������
                dtAcc.InitDateTime(acc, bRealDTime, bAccDTime);
              }
            }

            break;
          }
        }        
      }
    }

    private void tBar_acc_Scroll(object sender, EventArgs e)
    {
      int acc = tBar_acc.Value;

      lbl_acc.Text = acc.ToString();

      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber.AccerarationRate);
      List<BacnetValue> values = new List<BacnetValue>();
      values.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_SIGNED_INT, acc));
      communicator.Client.WritePropertyRequest(
        new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DTCTRL_EXPORT.ToString()),
        boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

    }
  }
}