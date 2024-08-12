using System.IO.BACnet;
using BaCSharp;

using Shizuku2.BACnet;

namespace AccelerationController
{
  public partial class Form1 : Form
  {

    /// <summary>自身のDevice ID</summary>
    const int DEVICE_ID = 150;

    #region 加速度制御コントローラ（DateTimeController）の情報

    /// <summary>加速度制御コントローラのBACnet Device ID</summary>
    const int DTCTRL_DEVICE_ID = 1;

    /// <summary>加速度制御コントローラのExclusiveポート</summary>
    const int DTCTRL_EXPORT = 0xBAC0 + DTCTRL_DEVICE_ID;

    /// <summary>加速度制御コントローラのMeber Number</summary>
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

      //別スレッドで時刻表示を更新
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