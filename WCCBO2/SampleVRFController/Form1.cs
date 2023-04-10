using System;
using System.Net.NetworkInformation;

using System.IO.BACnet;
using BaCSharp;

namespace SampleVRFController
{
  public partial class Form1 : Form
  {

    #region �萔�錾

    const uint DEVICE_ID = 2; //DaikinController

    const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    #endregion

    #region �񋓌^

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

    #region �C���X�^���X�ϐ��E�v���p�e�B

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

      //BACnet�N���C�A���g�쐬
      client = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false));
      client.WritePriority = 8;
      client.Retries = 0;
      client.Timeout = 1000;
      client.Start();

      //�����@�����X�g�ɒǉ�
      vrfUnitIndices = new List<VRFUnitIndex>();
      for (int i = 0; i < 6; i++) vrfUnitIndices.Add(new VRFUnitIndex(0, i));
      for (int i = 0; i < 6; i++) vrfUnitIndices.Add(new VRFUnitIndex(1, i));
      for (int i = 0; i < 6; i++) vrfUnitIndices.Add(new VRFUnitIndex(2, i));
      for (int i = 0; i < 8; i++) vrfUnitIndices.Add(new VRFUnitIndex(3, i));

      for (int i = 0; i < vrfUnitIndices.Count; i++)
        lb_iUnits.Items.Add("�����@ " + vrfUnitIndices[i].ToString());

      //������莞�ԊԊu�ōX�V
      Task.Run(() =>
      {
        while (true)
        {
          loadStatus();
          Thread.Sleep(500);
        }
      });
    }

    #region BACnet�ʐM��M�ɂ���ԍX�V����

    private async void loadSetPoint()
    {
      //�����@�����
      int iuNum = lb_iUnits.SelectedIndex;
      if (iuNum == -1) return;

      BacnetObjectId boID;

      //�^�]���[�h���擾
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
        (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuNum, MemberNumber.OperationMode_Status));
      IList<BacnetValue> rslt = await client.ReadPropertyAsync(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE);
      uint modeStt = (uint)rslt[0].Value;

      //���x�ݒ�l���擾
      boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE,
        (uint)getInstanceNumber(ObjectNumber.AnalogValue, iuNum, MemberNumber.Setpoint));
      rslt = await client.ReadPropertyAsync(bacAddress, boID, BacnetPropertyIds.PROP_PRESENT_VALUE);
      double tSp = (double)rslt[0].Value;

      //�g�[���[�h�̏ꍇ�ɂ�5���̕΍��𔽉f
      if (modeStt == 2) tSp -= 5;

      lblSP.Text = tSp.ToString();
    }

    private async void loadStatus()
    {
      //�����@�����
      if (iuIndex == -1) return;

      //�ǂݍ��ރv���p�e�B
      BacnetPropertyReference[] properties = new BacnetPropertyReference[]
      { new BacnetPropertyReference((uint)BacnetPropertyIds.PROP_PRESENT_VALUE, System.IO.BACnet.Serialize.ASN1.BACNET_ARRAY_ALL) };
      //�ǂݍ���BACnetObjectId�̃��X�g
      BacnetReadAccessSpecification[] propToRead = new BacnetReadAccessSpecification[]
      {
        //�^�]���[�h
        new BacnetReadAccessSpecification(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
          (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuIndex, MemberNumber.OperationMode_Status)),properties),
        //�ݒ莺��
         new BacnetReadAccessSpecification(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE,
          (uint)getInstanceNumber(ObjectNumber.AnalogValue, iuIndex, MemberNumber.Setpoint)),properties),
         //����
         new BacnetReadAccessSpecification(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
          (uint)getInstanceNumber(ObjectNumber.MultiStateInput, iuIndex, MemberNumber.VentilationAmount_Status)),properties),
         //����
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
        if (this.IsDisposed) return;  //�C�x�߃R�[�h�B�{���I�ȉ����ɂȂ��Ă��Ȃ�

        //�g�[���[�h�̏ꍇ�ɂ�5���̕΍��𔽉f
        if (cMode == 2) cSp -= 5;

        //�ݒ艷�x
        lblSP.Invoke(new Action(() =>
        {
          lblSP.Text = cSp.ToString("F0");
        }));

        //�^�]���[�h
        lblMode.Invoke(new Action(() =>
        {
          lblMode.Text =
          cMode == 1 ? "��[" :
          cMode == 2 ? "�g�[" :
          cMode == 3 ? "���C" :
          cMode == 4 ? "����" : "����";
        }));

        //����
        lblAmount.Invoke(new Action(() =>
        {
          lblAmount.Text =
          cAmount == 1 ? "��" :
          cAmount == 2 ? "��" : "����";
        }));

        //����
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

    #region �ėp�֐�

    private int getInstanceNumber
      (ObjectNumber objNumber, int iUnitNumber, MemberNumber memNumber)
    {
      //DBACS�ł͂��̔ԍ��ŊǗ����Ă���悤�����A����ł͌����傫������B
      //return (int)objNumber + iUnitNumber * 256 + (int)memNumber; 
      return iUnitNumber * 256 + (int)memNumber;
    }

    #endregion

    #region �R���g���[�����쎞�̏���

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

      //�����̒l��0,1,2,3,4,7��������
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

    #region �\���̒�`

    /// <summary>���O�@�Ǝ����@�̔ԍ���ێ�����</summary>
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