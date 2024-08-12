using System.IO.BACnet;
using BaCSharp;

using Shizuku2.BACnet;

namespace AccelerationController
{
  public partial class Form1 : Form
  {

    /// <summary>���g��Device ID</summary>
    const int DEVICE_ID = 150;

    #region �����x����R���g���[���iDateTimeController�j�̏��

    /// <summary>�����x����R���g���[����BACnet Device ID</summary>
    const int DTCTRL_DEVICE_ID = 1;

    /// <summary>�����x����R���g���[����Exclusive�|�[�g</summary>
    const int DTCTRL_EXPORT = 0xBAC0 + DTCTRL_DEVICE_ID;

    /// <summary>�����x����R���g���[����Meber Number</summary>
    public enum MemberNumber
    {
      CurrentDateTimeInSimulation = 1,
      AccerarationRate = 2,
      BaseRealDateTime = 3,
      BaseAcceleratedDateTime = 4,
    }

    #endregion

    private PresentValueReadWriter pvrw = new PresentValueReadWriter(DEVICE_ID);

    public Form1()
    {
      InitializeComponent();

      pvrw.StartService();

      //�ʃX���b�h�Ŏ����\�����X�V
      Task.Run(() =>
      {
        if (pvrw.SubscribeDateTimeCOV())
        {
          while (true)
          {
            this.Invoke((MethodInvoker)(() =>
            {
              this.lbl_dateTime.Text = pvrw.CurrentDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            }));

            Thread.Sleep(1000);
          }
        }
      });

    }

    private void tBar_acc_Scroll(object sender, EventArgs e)
    {
      int acc = tBar_acc.Value;

      lbl_acc.Text = acc.ToString();

      pvrw.WritePresentValue(
        new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DTCTRL_EXPORT.ToString()),
        BacnetObjectTypes.OBJECT_ANALOG_OUTPUT,
        (uint)MemberNumber.AccerarationRate,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)acc),
        out bool success
        );
    }
  }
}