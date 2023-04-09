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

    List<VRFUnitIndex> vrfUnitIndices;

    BacnetClient client;

    BacnetAddress bacAddress = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + EXCLUSIVE_PORT.ToString());

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
    }

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

      lbl_SP.Text = tSp.ToString();
    }

    private int getInstanceNumber
      (ObjectNumber objNumber, int iUnitNumber, MemberNumber memNumber)
    {
      //DBACSではこの番号で管理しているようだが、これでは桁が大きすぎる。
      //return (int)objNumber + iUnitNumber * 256 + (int)memNumber; 
      return iUnitNumber * 256 + (int)memNumber;
    }

    private void lb_iUnits_SelectedIndexChanged(object sender, EventArgs e)
    {
      loadSetPoint();
    }

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