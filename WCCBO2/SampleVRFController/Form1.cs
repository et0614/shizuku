using System;
using System.Net.NetworkInformation;

using System.IO.BACnet;
using BaCSharp;

namespace SampleVRFController
{
  public partial class Form1 : Form
  {

    #region 定数宣言

    const uint DEVICE_ID = 2; //DaikinController

    const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    #endregion

    #region 列挙型

    private enum ObjectNumber
    {
      AnalogInput = 0 * 4194304,
      AnalogOutput = 1 * 4194304,
      AnalogValue = 2 * 4194304,
      BinaryInput = 3 * 4194304,
      BinaryOutput = 4 * 4194304,
      BinaryValue = 5 * 4194304,
      MultiStateInput = 13 * 4194304,
      MultiStateOutput = 14 * 4194304,
      Accumulator = 23 * 4194304
    }

    private enum MemberNumber
    {
      OnOff_Setting = 1,
      OnOff_Status = 2,
      Alarm = 3,
      MalfunctionCode = 4,
      OperationMode_Setting = 5,
      OperationMode_Status = 6,
      FanSpeed_Setting = 7,
      FanSpeed_Status = 8,
      MeasuredRoomTemperature = 9,
      Setpoint = 10,
      FilterSignSignal = 11,
      FilterSignSignalReset = 12,
      RemoteControllerPermittion_OnOff = 13,
      RemoteControllerPermittion_OperationMode = 14,
      RemoteControllerPermittion_Setpoint = 16,
      CentralizedControl = 17,
      AccumulatedGas = 18,
      AccumulatedPower = 19,
      CommunicationStatus = 20,
      ForcedSystemStop = 21,
      AirflowDirection_Setting = 22,
      AirflowDirection_Status = 23,
      ForcedThermoOff_Setting = 24,
      ForcedThermoOff_Status = 25,
      EnergySaving_Setting = 26,
      EnergySaving_Status = 27,
      ThermoOn_Status = 28,
      Compressor_Status = 29,
      IndoorFan_Status = 30,
      Heater_Status = 31,
      VentilationMode_Setting = 32,
      VentilationMode_Status = 33,
      VentilationAmount_Setting = 34,
      VentilationAmount_Status = 35
    }

    #endregion

    #region インスタンス変数・プロパティ

    private int iuIndex = -1;

    List<VRFUnitIndex> vrfUnitIndices;

    BacnetClient client;

    BacnetAddress bacAddress = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + EXCLUSIVE_PORT.ToString());

    private uint cMode = 1;

    private double cSp = 24;

    private uint cAmount = 1;

    private uint cDirection = 1;

    #endregion

    public Form1()
    {
      InitializeComponent();

      //BACnetクライアント作成
      client = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false));
      client.WritePriority = 8;
      client.Retries = 0;
      client.Timeout = 1000;
      client.Start();

      //室内機をリストに追加
      vrfUnitIndices = new List<VRFUnitIndex>();
      for (int i = 0; i < 6; i++) vrfUnitIndices.Add(new VRFUnitIndex(0, i));
      for (int i = 0; i < 6; i++) vrfUnitIndices.Add(new VRFUnitIndex(1, i));
      for (int i = 0; i < 6; i++) vrfUnitIndices.Add(new VRFUnitIndex(2, i));
      for (int i = 0; i < 8; i++) vrfUnitIndices.Add(new VRFUnitIndex(3, i));

      for (int i = 0; i < vrfUnitIndices.Count; i++)
        lb_iUnits.Items.Add("室内機 " + vrfUnitIndices[i].ToString());

      //情報を一定時間間隔で更新
      Task.Run(() =>
      {
        while (true)
        {
          loadStatus();
          Thread.Sleep(500);
        }
      });
    }

    #region BACnet通信受信による状態更新処理

    private async void loadSetPoint()
    {
      //室内機を特定
      int iuNum = lb_iUnits.SelectedIndex;
      if (iuNum == -1) return;

      BacnetObjectId boID;

      //運転モードを取得
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
        (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.OperationMode_Status));
      IList<BacnetValue> rslt = await client.ReadPropertyAsync(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE);
      uint modeStt = (uint)rslt[0].Value;

      //温度設定値を取得
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE,
        (uint)getInstanceNumber(ObjectNumber.AnalogValue, iuNum, MemberNumber.Setpoint));
      rslt = await client.ReadPropertyAsync(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE);
      double tSp = (double)rslt[0].Value;

      //暖房モードの場合には5℃の偏差を反映
      if (modeStt == 2) tSp -= 5;

      lblSP.Text = tSp.ToString();
    }

    private async void loadStatus()
    {
      //室内機を特定
      if (iuIndex == -1) return;

      //読み込むプロパティ
      BacnetPropertyReference[] properties = new BacnetPropertyReference[]
      { new BacnetPropertyReference((uint)BacnetPropertyIds.PROP_PRESENT_VALUE, System.IO.BACnet.Serialize.ASN1.BACNET_ARRAY_ALL) };
      //読み込むBACnetObjectIdのリスト
      BacnetReadAccessSpecification[] propToRead = new BacnetReadAccessSpecification[]
      {
        //運転モード
        new BacnetReadAccessSpecification(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
          (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuIndex, MemberNumber.OperationMode_Status)),properties),
        //設定室温
         new BacnetReadAccessSpecification(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE,
          (uint)getInstanceNumber(ObjectNumber.AnalogValue, iuIndex, MemberNumber.Setpoint)),properties),
         //風量
         new BacnetReadAccessSpecification(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
          (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuIndex, MemberNumber.VentilationAmount_Status)),properties),
         //風向
         new BacnetReadAccessSpecification(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT,
          (uint)getInstanceNumber(ObjectNumber.AnalogInput, iuIndex, MemberNumber.AirflowDirection_Status)),properties)
      };

      if (client.ReadPropertyMultipleRequest(bacAddress, propToRead, out IList<BacnetReadAccessResult> rslt))
      {
        cMode = (uint)rslt[0].values[0].value[0].Value;
        cSp = (double)rslt[1].values[0].value[0].Value;
        cAmount = (uint)rslt[2].values[0].value[0].Value;
        cDirection = (uint)rslt[3].values[0].value[0].Value;
        if (this.IsDisposed) return;  //気休めコード。本質的な解決になっていない

        //暖房モードの場合には5℃の偏差を反映
        if (cMode == 2) cSp -= 5;

        //設定温度
        lblSP.Invoke(new Action(() =>
        {
          lblSP.Text = cSp.ToString("F0");
        }));

        //運転モード
        lblMode.Invoke(new Action(() =>
        {
          lblMode.Text =
          cMode == 1 ? "冷房" :
          cMode == 2 ? "暖房" :
          cMode == 3 ? "換気" :
          cMode == 4 ? "自動" : "除湿";
        }));

        //風量
        lblAmount.Invoke(new Action(() =>
        {
          lblAmount.Text =
          cAmount == 1 ? "弱" :
          cAmount == 2 ? "強" : "自動";
        }));

        //風向
        pbxDirection.Invoke(new Action(() =>
        {
          pbxDirection.Image =
          cDirection == 0 ? Resource._0 :
          cDirection == 1 ? Resource._1 :
          cDirection == 2 ? Resource._2 :
          cDirection == 3 ? Resource._3 :
          cDirection == 4 ? Resource._4 : Resource._7;
        }));
      }
    }

    #endregion

    #region 汎用関数

    private int getInstanceNumber
      (ObjectNumber objNumber, int iUnitNumber, MemberNumber memNumber)
    {
      //DBACSではこの番号で管理しているようだが、これでは桁が大きすぎる。
      //return (int)objNumber + iUnitNumber * 256 + (int)memNumber; 
      return iUnitNumber * 256 + (int)memNumber;
    }

    #endregion

    #region コントロール操作時の処理

    private void lb_iUnits_SelectedIndexChanged(object sender, EventArgs e)
    {
      iuIndex = lb_iUnits.SelectedIndex;
    }
    private void btnSPUpDown_Click(object sender, EventArgs e)
    {
      bool isUp = (sender == btnSPUp);

      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE,
          (uint)getInstanceNumber(ObjectNumber.AnalogValue, iuIndex, MemberNumber.Setpoint));
      List<BacnetValue> values = new List<BacnetValue>();
      values.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE, cSp + (isUp ? 1 : -1)));
      client.WritePropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);
    }

    private void btnModeUpDown_Click(object sender, EventArgs e)
    {
      bool isUp = (sender == btnModeUp);

      int inc = isUp ? 1 : -1;
      uint nMode = (uint)Math.Max(1, Math.Min(4, cMode + inc));

      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
          (uint)getInstanceNumber(ObjectNumber.MultiStateOutput, iuIndex, MemberNumber.OperationMode_Setting));
      List<BacnetValue> values = new List<BacnetValue>();
      values.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, nMode));
      client.WritePropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);
    }

    private void btnAmountUpDown_Click(object sender, EventArgs e)
    {
      bool isUp = (sender == btnAmountUp);

      int inc = isUp ? 1 : -1;
      uint nAmount = (uint)Math.Max(1, Math.Min(3, cAmount + inc));

      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
          (uint)getInstanceNumber(ObjectNumber.MultiStateOutput, iuIndex, MemberNumber.VentilationAmount_Setting));
      List<BacnetValue> values = new List<BacnetValue>();
      values.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, nAmount));
      client.WritePropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);
    }

    private void btnDirectionUpDown_Click(object sender, EventArgs e)
    {
      bool isUp = (sender == btnDirectionUp);

      //風向の値は0,1,2,3,4,7だから厄介
      uint nDirection;
      if (cDirection == 4 && isUp) nDirection = 7;
      else if (cDirection == 7 && !isUp) nDirection = 4;
      else if (cDirection == 7 && isUp) nDirection = 7;
      else
      {
        int inc = isUp ? 1 : -1;
        nDirection = (uint)Math.Max(0, Math.Min(4, cDirection + inc));
      }

      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE,
          (uint)getInstanceNumber(ObjectNumber.AnalogValue, iuIndex, MemberNumber.AirflowDirection_Setting));
      List<BacnetValue> values = new List<BacnetValue>();
      values.Add(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, nDirection));
      client.WritePropertyRequest(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);
    }

    #endregion

    #region 構造体定義

    /// <summary>室外機と室内機の番号を保持する</summary>
    private struct VRFUnitIndex
    {
      public string ToString()
      {
        return (OUnitIndex + 1).ToString() + "-" + (IUnitIndex + 1).ToString();
      }

      public int OUnitIndex { get; private set; }

      public int IUnitIndex { get; private set; }

      public VRFUnitIndex(int oUnitIndex, int iUnitIndex)
      {
        OUnitIndex = oUnitIndex;
        IUnitIndex = iUnitIndex;
      }
    }

    #endregion

  }
}