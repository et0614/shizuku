using System.IO.BACnet;
using System.IO.BACnet.Base;
using BaCSharp;

namespace Shizuku2.BACnet
{
  public class VRFCommunicator
  {

    #region 定数宣言

    /// <summary>VRFコントローラのデバイスID</summary>
    public const uint VRFCONTROLLER_DEVICE_ID = 2;

    /// <summary>VRFコントローラの排他的ポート番号</summary>
    public const int VRFCONTROLLER_EXCLUSIVE_PORT = 0xBAC0 + (int)VRFCONTROLLER_DEVICE_ID;

    /// <summary>VRFコントローラのBACnetアドレス</summary>
    public static readonly BacnetAddress VRFCTRL_BACADD = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + VRFCONTROLLER_EXCLUSIVE_PORT.ToString());

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    public enum VRFControllerMember
    {
      /// <summary>On/Offの設定</summary>
      OnOff_Setting = 1,
      /// <summary>On/Offの状態</summary>
      OnOff_Status = 2,
      /// <summary>運転モードの設定</summary>
      OperationMode_Setting = 3,
      /// <summary>運転モードの状態</summary>
      OperationMode_Status = 4,
      /// <summary>室温設定値の設定</summary>
      Setpoint_Setting = 5,
      /// <summary>室温設定値の状態</summary>
      Setpoint_Status = 6,
      /// <summary>還温度</summary>
      MeasuredRoomTemperature = 7,
      /// <summary>ファン風量の設定</summary>
      FanSpeed_Setting = 8,
      /// <summary>ファン風量の状態</summary>
      FanSpeed_Status = 9,
      /// <summary>風向の設定</summary>
      AirflowDirection_Setting = 10,
      /// <summary>風量の状態</summary>
      AirflowDirection_Status = 11,
      /// <summary>手元リモコン操作許可の設定</summary>
      RemoteControllerPermittion_Setpoint_Setting = 12,
      /// <summary>手元リモコン操作許可の状態</summary>
      RemoteControllerPermittion_Setpoint_Status = 13,
      /// <summary>冷媒温度強制制御の設定</summary>
      ForcedRefrigerantTemperature_Setting = 14,
      /// <summary>冷媒温度強制制御の状態</summary>
      ForcedRefrigerantTemperature_Status = 15,
      /// <summary>冷媒蒸発温度設定値の設定</summary>
      EvaporatingTemperatureSetpoint_Setting = 16,
      /// <summary>冷媒蒸発温度設定値の状態</summary>
      EvaporatingTemperatureSetpoint_Status = 17,
      /// <summary>冷媒凝縮温度設定値の設定</summary>
      CondensingTemperatureSetpoint_Setting = 18,
      /// <summary>冷媒凝縮温度設定値の状態</summary>
      CondensingTemperatureSetpoint_Status = 19
    }

    /// <summary>運転モード</summary>
    public enum Mode
    {
      /// <summary>冷却</summary>
      Cooling,
      /// <summary>加熱</summary>
      Heating
    }

    /// <summary>ファン風量</summary>
    public enum FanSpeed
    {
      /// <summary>弱</summary>
      Low,
      /// <summary>中</summary>
      Middle,
      /// <summary>強い</summary>
      High
    }

    /// <summary>風向</summary>
    public enum Direction
    {
      /// <summary>水平</summary>
      Horizontal,
      /// <summary>22.5度</summary>
      Degree_225,
      /// <summary>45.0度</summary>
      Degree_450,
      /// <summary>67.5度</summary>
      Degree_675,
      /// <summary>垂直</summary>
      Vertical
    }

    #endregion

    #region インスタンス変数・プロパティ

    private BACnetCommunicator communicator;

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="deviceID">通信で使うBACnet DeviceのID</param>
    public VRFCommunicator(uint deviceID)
    {
      DeviceObject dObject = new DeviceObject(deviceID, "VRF communicator", "VRF communicator", true);
      communicator = new BACnetCommunicator(dObject, (int)(0xBAC0 + deviceID));
      communicator.StartService();
    }

    #endregion

    #region 発停関連

    /// <summary>起動する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void TurnOn
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.OnOff_Setting));
      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_ACTIVE) }
        );
    }

    /// <summary>停止する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void TurnOff
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.OnOff_Setting));
      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_INACTIVE) }
        );
    }

    /// <summary>起動しているか否かを取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>起動しているか否か</returns>
    public bool IsTurnedOn
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_BINARY_INPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.OnOff_Status));
      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (bool)val[0].Value;
      }

      succeeded = false;
      return false;
    }

    #endregion

    #region 運転モード関連

    /// <summary>運転モードを変える</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="mode">運転モード</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangeMode(uint oUnitIndex, uint iUnitIndex, Mode mode, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.OperationMode_Setting));

      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, mode == Mode.Cooling ? 1u : 2u) } //1:冷房, 2:暖房
        );
    }

    /// <summary>運転モードを取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>運転モード</returns>
    public Mode GetMode(uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.OperationMode_Status));

      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        if ((uint)val[0].Value == 1) return Mode.Cooling;
        else return Mode.Heating;
      }

      succeeded = false;
      return Mode.Cooling;
    }

    #endregion

    #region 室温関連

    /// <summary>室温設定値[C]を変える</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="setpointTemperature">室温設定値[C]</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangeSetpointTemperature
      (uint oUnitIndex, uint iUnitIndex, double setpointTemperature, out bool succeeded) 
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_ANALOG_VALUE,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.Setpoint_Setting));

      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE, setpointTemperature) }
        );
    }

    /// <summary>室温設定値[C]を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>室温設定値[C]</returns>
    public double GetSetpointTemperature
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.Setpoint_Status));

      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (double)val[0].Value;
      }

      succeeded = false;
      return 0;
    }

    /// <summary>室温[C]を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>室温[C]</returns>
    public double GetRoomTemperature
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.MeasuredRoomTemperature));

      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (double)val[0].Value;
      }

      succeeded = false;
      return 0;
    }

    #endregion

    #region 風量関連

    /// <summary>ファン風量を変える</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="speed">ファン風量</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangeFanSpeed
      (uint oUnitIndex, uint iUnitIndex, FanSpeed speed, out bool succeeded) 
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.FanSpeed_Setting));

      uint spd = speed == FanSpeed.Low ? 1u : speed == FanSpeed.Middle ? 2u : 3u;
      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, spd) }
        );
    }

    /// <summary>ファン風量を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>ファン風量</returns>
    public FanSpeed GetFanSpeed
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.FanSpeed_Status));

      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        switch ((uint)val[0].Value)
        {
          case 1u:
            return FanSpeed.Low;
          case 2u:
            return FanSpeed.Middle;
          default:
            return FanSpeed.High;
        }
      }

      succeeded = false;
      return FanSpeed.Low;
    }

    #endregion

    #region 風向関連

    /// <summary>風向を変える</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="direction">風向</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangeDirection
      (uint oUnitIndex, uint iUnitIndex, Direction direction, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.AirflowDirection_Setting));

      uint dir =
        direction == Direction.Horizontal ? 1u :
        direction == Direction.Degree_225 ? 2u :
        direction == Direction.Degree_450 ? 3u :
        direction == Direction.Degree_675 ? 4u : 5u;
      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, dir) }
        );
    }

    /// <summary>風向を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>風向</returns>
    public Direction GetDirection
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded) 
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.AirflowDirection_Status));

      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        switch ((uint)val[0].Value)
        {
          case 1u:
            return Direction.Horizontal;
          case 2u:
            return Direction.Degree_225;
          case 3u:
            return Direction.Degree_450;
          case 4u:
            return Direction.Degree_675;
          default:
            return Direction.Vertical;
        }
      }

      succeeded = false;
      return Direction.Horizontal;
    }

    #endregion

    #region 手元リモコン関連

    /// <summary>手元リモコン操作を許可する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void PermitLocalControl
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_BINARY_VALUE,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.RemoteControllerPermittion_Setpoint_Setting));
      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_ACTIVE) }
        );
    }

    /// <summary>手元リモコン操作を禁止する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ProhibitLocalControl
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_BINARY_VALUE,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.RemoteControllerPermittion_Setpoint_Setting));
      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_INACTIVE) }
        );
    }

    /// <summary>手元リモコン操作が許可されているか否か</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>手元リモコン操作が許可されているか否か</returns>
    public bool IsLocalControlProhibited
      (uint oUnitIndex, uint iUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_BINARY_INPUT,
        GetInstanceNumber(oUnitIndex, iUnitIndex, VRFControllerMember.RemoteControllerPermittion_Setpoint_Status));
      
      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (bool)val[0].Value;
      }

      succeeded = false;
      return false;
    }

    #endregion

    #region 冷媒温度強制制御関連

    /// <summary>冷媒温度強制制御を有効にする</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void EnableRefrigerantTemperatureControl(uint oUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_BINARY_VALUE,
        GetInstanceNumber(oUnitIndex, VRFControllerMember.ForcedRefrigerantTemperature_Setting));

      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_ACTIVE) }
        );
    }

    /// <summary>冷媒温度強制制御を無効にする</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void DisableRefrigerantTemperatureControl(uint oUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_BINARY_VALUE,
        GetInstanceNumber(oUnitIndex, VRFControllerMember.ForcedRefrigerantTemperature_Setting));

      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_INACTIVE) }
        );
    }

    /// <summary>冷媒温度強制制御が有効か否かを取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>冷媒温度強制制御が有効か否か</returns>
    public bool IsRefrigerantTemperatureControlEnabled(uint oUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_BINARY_INPUT,
        GetInstanceNumber(oUnitIndex, VRFControllerMember.ForcedRefrigerantTemperature_Setting));

      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (bool)val[0].Value;
      }

      succeeded = false;
      return false;
    }

    #endregion

    #region 蒸発/凝縮温度関連

    /// <summary>蒸発温度設定値[C]を変える</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="evaporatingTemperature">蒸発温度設定値[C]</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangeEvaporatingTemperature
      (uint oUnitIndex, double evaporatingTemperature, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_ANALOG_VALUE,
        GetInstanceNumber(oUnitIndex, VRFControllerMember.EvaporatingTemperatureSetpoint_Setting));

      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE, evaporatingTemperature) }
        );
    }

    /// <summary>蒸発温度設定値[C]を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>蒸発温度設定値[C]</returns>
    public double GetEvaporatingTemperature(uint oUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        GetInstanceNumber(oUnitIndex, VRFControllerMember.EvaporatingTemperatureSetpoint_Status));

      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (double)val[0].Value;
      }

      succeeded = false;
      return 0;
    }

    /// <summary>凝縮温度設定値[C]を変える</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="condensingTemperature">凝縮温度設定値[C]</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void ChangeCondensingTemperature
      (uint oUnitIndex, double condensingTemperature, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_ANALOG_VALUE,
        GetInstanceNumber(oUnitIndex, VRFControllerMember.CondensingTemperatureSetpoint_Setting));

      succeeded = communicator.Client.WritePropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE,
        new List<BacnetValue>
        { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE, condensingTemperature) }
        );
    }

    /// <summary>凝縮温度設定値[C]を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>凝縮温度設定値[C]</returns>
    public double GetCondensingTemperature(uint oUnitIndex, out bool succeeded)
    {
      BacnetObjectId boID = new BacnetObjectId(
        BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        GetInstanceNumber(oUnitIndex, VRFControllerMember.CondensingTemperatureSetpoint_Status));

      if (communicator.Client.ReadPropertyRequest(VRFCTRL_BACADD, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val))
      {
        succeeded = true;
        return (double)val[0].Value;
      }

      succeeded = false;
      return 0;
    }

    #endregion

    #region 補助メソッド

    /// <summary>インスタンス番号を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="iUnitIndex">室内機番号（1～8）</param>
    /// <param name="member">項目</param>
    /// <returns>インスタンス番号</returns>
    public static uint GetInstanceNumber
      (uint oUnitIndex, uint iUnitIndex, VRFControllerMember member)
    {
      if (!isIndexValid(oUnitIndex, iUnitIndex)) 
        throw new Exception("Index of outdoor/indoor unit is invalid.");

      return 1000 * oUnitIndex + 100 * iUnitIndex + (uint)member;
    }

    /// <summary>インスタンス番号を取得する</summary>
    /// <param name="oUnitIndex">室外機番号（1～4）</param>
    /// <param name="member">項目</param>
    /// <returns>インスタンス番号</returns>
    /// <exception cref="Exception"></exception>
    public static uint GetInstanceNumber
      (uint oUnitIndex, VRFControllerMember member)
    {
      if (!isIndexValid(oUnitIndex))
        throw new Exception("Index of outdoor unit is invalid.");

      return 1000 * oUnitIndex + (uint)member;
    }

    /// <summary>室内外機の番号が有効か否かを判定する</summary>
    /// <param name="oUnitIndex">室外機番号</param>
    /// <param name="iUnitIndex">室内機番号</param>
    /// <returns>室内外機の番号が有効か否か</returns>
    private static bool isIndexValid
      (uint oUnitIndex, uint iUnitIndex)
    {
      if (
        oUnitIndex < 0 ||
        4 < oUnitIndex ||
        iUnitIndex < 0 ||
        8 < iUnitIndex ||
        (oUnitIndex != 4 && 6 < iUnitIndex)
        )
        return false;

      else return true;
    }

    /// <summary>室外機の番号が有効か否かを判定する</summary>
    /// <param name="oUnitIndex">室外機番号</param>
    /// <returns>室外機の番号が有効か否か</returns>
    private static bool isIndexValid(uint oUnitIndex)
    {
      if (oUnitIndex < 0 || 4 < oUnitIndex)
        return false;

      else return true;
    }

    #endregion

  }
}