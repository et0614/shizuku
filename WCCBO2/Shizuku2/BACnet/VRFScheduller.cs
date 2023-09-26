using BaCSharp;
using System.IO.BACnet;
using System.IO.BACnet.Base;

namespace Shizuku2.BACnet
{
    public class VRFScheduller : IBACnetController
    {

        #region 定数宣言

        const uint VRFCTRL_DEVICE_ID = 2;

        const uint HEXCTRL_DEVICE_ID = 6;

        const uint THIS_DEVICE_ID = 3;

        public const int VRFCTRL_EXCLUSIVE_PORT = 0xBAC0 + (int)VRFCTRL_DEVICE_ID;

        public const int HEXCTRL_EXCLUSIVE_PORT = 0xBAC0 + (int)HEXCTRL_DEVICE_ID;

        public const int THIS_EXCLUSIVE_PORT = 0xBAC0 + (int)THIS_DEVICE_ID;

        const string DEVICE_NAME = "WCCBO Original VRF and HEX scheduller";

        const string DEVICE_DESCRIPTION = "WCCBO Original VRF and HEX scheduller";

        /// <summary>VRFコントローラのBACnetアドレス</summary>
        readonly BacnetAddress vrfCtrlBAdd = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + VRFCTRL_EXCLUSIVE_PORT.ToString());

        /// <summary>HEXコントローラのBACnetアドレス</summary>
        readonly BacnetAddress hexCtrlBAdd = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + HEXCTRL_EXCLUSIVE_PORT.ToString());

        #endregion

        #region インスタンス変数・プロパティ

        private DateTimeAccelerator dtAccl;

        /// <summary>現在の日時を取得する</summary>
        public DateTime CurrentDateTime
        { get { return dtAccl.AcceleratedDateTime; } }

        /// <summary>加速度を取得する</summary>
        public int AccelerationRate
        { get { return dtAccl.AccelerationRate; } }

        private BACnetCommunicator communicator;

        /// <summary>スケジュールを有効にする</summary>
        public bool EnableScheduling { get; set; } = false;

        #endregion

        #region コンストラクタ

        public VRFScheduller(ExVRFSystem[] vrfs, int accRate, DateTime now)
        {
            dtAccl = new DateTimeAccelerator(accRate, now);
            DeviceObject dObject = new DeviceObject(THIS_DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);
            communicator = new BACnetCommunicator(dObject, THIS_EXCLUSIVE_PORT);

            try
            {
                //別スレッドでスケジュール設定
                Task.Run(() =>
                {
                    DateTime lastDt = dtAccl.AcceleratedDateTime;
                    while (true)
                    {
                        //空調開始時
                        if (!isHVACTime(lastDt) && isHVACTime(dtAccl.AcceleratedDateTime))
                        {
                            BacnetObjectId boID;
                            List<BacnetValue> values;

                            for (int oUnt = 0; oUnt < vrfs.Length; oUnt++)
                            {
                                for (int iUnt = 0; iUnt < vrfs[oUnt].VRFSystem.IndoorUnitNumber; iUnt++)
                                {
                                    int bBase = bBase = 1000 * (oUnt + 1) + 100 * (iUnt + 1);

                                    //VRF************************
                                    //On/Off
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + VRFController.MemberNumber.OnOff_Setting));
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_ACTIVE)
                          };
                                    communicator.Client.WritePropertyRequest(vrfCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                                    //Mode
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + VRFController.MemberNumber.OperationMode_Setting));
                                    bool isCooling = 5 <= dtAccl.AcceleratedDateTime.Month && dtAccl.AcceleratedDateTime.Month <= 10;
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, isCooling ? 1u : 2u) //1:冷房, 2:暖房, 3:換気
                          };
                                    communicator.Client.WritePropertyRequest(vrfCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                                    //SP
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)(bBase + VRFController.MemberNumber.Setpoint_Setting));
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, isCooling ? 26f : 22f)
                          };
                                    communicator.Client.WritePropertyRequest(vrfCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                                    //風量
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + VRFController.MemberNumber.FanSpeed_Setting));
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, 2u)  //1:Low, 2:Midddle, 3:High
                          };
                                    communicator.Client.WritePropertyRequest(vrfCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                                    //角度
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + VRFController.MemberNumber.AirflowDirection_Setting));
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, 3u) //1:Horizontal, 2:22.5deg ,3:45deg ,4:67.5deg ,5:Vertical
                          };
                                    communicator.Client.WritePropertyRequest(vrfCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                                    //HEX************************
                                    //On/Off
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + VentilationController.MemberNumber.HexOnOff));
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_ACTIVE)
                          };
                                    communicator.Client.WritePropertyRequest(hexCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                                    //Bypass
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + VentilationController.MemberNumber.HexBypassEnabled));
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_INACTIVE)
                          };
                                    communicator.Client.WritePropertyRequest(hexCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                                    //ファン風量
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)(bBase + VentilationController.MemberNumber.HexFanSpeed));
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, 3u)  //1:Low, 2:Midddle, 3:High
                          };
                                    communicator.Client.WritePropertyRequest(hexCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);
                                }
                            }
                        }
                        //空調停止時
                        else if (isHVACTime(lastDt) && !isHVACTime(dtAccl.AcceleratedDateTime))
                        {
                            BacnetObjectId boID;
                            List<BacnetValue> values;

                            for (int oHex = 0; oHex < vrfs.Length; oHex++)
                            {
                                for (int iHex = 0; iHex < vrfs[oHex].VRFSystem.IndoorUnitNumber; iHex++)
                                {
                                    int bBase = 1000 * (oHex + 1) + 100 * (iHex + 1);

                                    //VRF On/Off
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + VRFController.MemberNumber.OnOff_Setting));
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_INACTIVE)
                          };
                                    communicator.Client.WritePropertyRequest(vrfCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);

                                    //HEX On/Off
                                    boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)(bBase + VentilationController.MemberNumber.HexOnOff));
                                    values = new List<BacnetValue>
                          {
                    new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, BacnetBinaryPv.BINARY_INACTIVE)
                          };
                                    communicator.Client.WritePropertyRequest(hexCtrlBAdd, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, values);
                                }
                            }
                        }

                        lastDt = dtAccl.AcceleratedDateTime;
                        Thread.Sleep(100);
                    }
                });
            }
            catch (Exception e)
            {

            }
        }

        private bool isWeekday(DateTime dTime)
        {
            return dTime.DayOfWeek != DayOfWeek.Saturday && dTime.DayOfWeek != DayOfWeek.Sunday;
        }

        private bool isHVACTime(DateTime dTime)
        {
            return isWeekday(dTime) && 7 <= dTime.Hour && dTime.Hour <= 19;
        }

        #endregion

        #region 日時同期用の補助関数

        /// <summary>COV通告を受けた場合の処理</summary>
        /// <param name="sender"></param>
        /// <param name="adr"></param>
        /// <param name="invokeId"></param>
        /// <param name="subscriberProcessIdentifier"></param>
        /// <param name="initiatingDeviceIdentifier"></param>
        /// <param name="monitoredObjectIdentifier"></param>
        /// <param name="timeRemaining"></param>
        /// <param name="needConfirm"></param>
        /// <param name="values"></param>
        /// <param name="maxSegments"></param>
        private void Client_OnCOVNotification
          (BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier,
          BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier,
          uint timeRemaining, bool needConfirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
        {
            //加速度が変化した場合      
            ushort port = BitConverter.ToUInt16(new byte[] { adr.adr[5], adr.adr[4] });
            if (
              port == DateTimeController.EXCLUSIVE_PORT &&
              monitoredObjectIdentifier.type == BacnetObjectTypes.OBJECT_ANALOG_OUTPUT &&
              monitoredObjectIdentifier.instance == (uint)DateTimeController.MemberNumber.Acceleration)
            {
                //この処理は汚いが・・・
                foreach (BacnetPropertyValue value in values)
                {
                    if (value.property.propertyIdentifier == (uint)BacnetPropertyIds.PROP_PRESENT_VALUE)
                    {
                        int acc = (int)value.value[0].Value;

                        BacnetObjectId boID;
                        //基準日時（加速時間）
                        //adr = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DTCTRL_PORT.ToString());
                        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)DateTimeController.MemberNumber.BaseAcceleratedDateTime);
                        if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val1))
                        {
                            DateTime dt1 = (DateTime)val1[0].Value;
                            DateTime dt2 = (DateTime)val1[1].Value;
                            DateTime bAccDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

                            //基準日時（現実時間）
                            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)DateTimeController.MemberNumber.BaseRealDateTime);
                            if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val2))
                            {
                                dt1 = (DateTime)val2[0].Value;
                                dt2 = (DateTime)val2[1].Value;
                                DateTime bRealDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

                                //初期化
                                dtAccl.InitDateTime(acc, bRealDTime, bAccDTime);
                            }
                        }

                        break;
                    }
                }
            }
        }

        #endregion

        #region IBACnetController実装

        /// <summary>制御値を機器やセンサに反映する</summary>
        public void ApplyManipulatedVariables(DateTime dTime)
        {

        }

        /// <summary>機器やセンサの検出値を取得する</summary>
        public void ReadMeasuredValues(DateTime dTime)
        {

        }

        /// <summary>BACnetControllerのサービスを開始する</summary>
        public void StartService()
        {
            communicator.StartService();

            //COV通告登録処理
            communicator.Client.OnCOVNotification += Client_OnCOVNotification;
            BacnetAddress bacAddress = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + DateTimeController.EXCLUSIVE_PORT.ToString());
            BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)DateTimeController.MemberNumber.Acceleration);
            communicator.Client.SubscribeCOVRequest(bacAddress, boID, (uint)DateTimeController.MemberNumber.Acceleration, false, false, 3600);
        }

        /// <summary>BACnetControllerのリソースを解放する</summary>
        public void EndService()
        {
            communicator.EndService();
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
