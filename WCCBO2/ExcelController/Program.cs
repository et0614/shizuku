using BaCSharp;
using NPOI.SS.UserModel;
using Org.BouncyCastle.Utilities.Encoders;
using System;
using System.IO.BACnet;
using System.IO.BACnet.Base;

using Shizuku2.BACnet;
using System.Security;

namespace ExcelController
{
  internal class Program
  {
    #region 定数宣言

    /// <summary>自身のDevice ID</summary>
    const int DEVICE_ID = 7001;

    const string FILE_NAME = "schedule.xlsx";

    const string SHEET_NAME = "schedule";

    #endregion

    #region 加速度制御コントローラ（DateTimeController）の情報

    /// <summary>加速度制御コントローラのBACnet Device ID</summary>
    const int DEVICE_ID_ACC = 1;

    /// <summary>加速度制御コントローラのExclusiveポート</summary>
    const int EXCLUSIVE_ACC = 0xBAC0 + DEVICE_ID_ACC;

    /// <summary>加速度制御コントローラのMeber Number</summary>
    public enum MemberNumber_Acc
    {
      CurrentDateTimeInSimulation = 1,
      AccerarationRate = 2,
      BaseRealDateTime = 3,
      BaseAcceleratedDateTime = 4,
    }

    #endregion

    private static BACnetCommunicator communicator;

    private static DateTimeAccelerator dtAcc = new DateTimeAccelerator(0, DateTime.Now);

    private static bool dtimeInitialized = false;


    static void Main(string[] args)
    {
      Console.WriteLine("Starting Excel controller.");

      VRFCommunicator vrfCom = new VRFCommunicator(DEVICE_ID + 1);

      //制御値保持インスタンス生成
      vrfCtrl[] vrfCtrls = new vrfCtrl[4];
      iHexCtrl[][] iHexCtrls = new iHexCtrl[4][];
      for (int i = 0; i < 4; i++)
      {
        vrfCtrls[i] = new vrfCtrl();
        iHexCtrls[i] = new iHexCtrl[i == 3 ? 8 : 6];
        for (int j = 0; j < iHexCtrls[i].Length; j++)
          iHexCtrls[i][j] = new iHexCtrl();
      }

      //Excelデータの読み込み*****************************************
      Console.Write("Loading excel data...");
      IWorkbook wbk = WorkbookFactory.Create(new FileStream(FILE_NAME, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
      ISheet wSheet = wbk.GetSheet(SHEET_NAME);
      int line = 3;
      while (true)
      {
        if (line == 675) break;

        DateTime dt1 = wSheet.GetRow(line).GetCell(0).DateCellValue;
        DateTime dt2 = wSheet.GetRow(line).GetCell(1).DateCellValue;
        DateTime dTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

        for (int i = 0; i < 4; i++)
        {
          vrfCtrl vc = vrfCtrls[i];

          //冷媒制御
          bool refCtrl = wSheet.GetRow(line).GetCell(2 + 0 + i * 39).BooleanCellValue;
          if (line == 3 || vc.refCtrl[vc.refCtrl.Count - 1].Item2 != refCtrl)
            vc.refCtrl.Add(Tuple.Create(dTime, refCtrl));

          //蒸発温度
          double evpTemp = wSheet.GetRow(line).GetCell(2 + 1 + i * 39).NumericCellValue;
          if (line == 3 || vc.evpTemp[vc.evpTemp.Count - 1].Item2 != evpTemp)
            vc.evpTemp.Add(Tuple.Create(dTime, evpTemp));

          //凝縮温度
          double cndTemp = wSheet.GetRow(line).GetCell(2 + 2 + i * 39).NumericCellValue;
          if (line == 3 || vc.cndTemp[vc.cndTemp.Count - 1].Item2 != cndTemp)
            vc.cndTemp.Add(Tuple.Create(dTime, cndTemp));

          //室内機別
          for (int j = 0; j < iHexCtrls[i].Length; j++)
          {
            iHexCtrl ic = iHexCtrls[i][j];

            //On/Off
            bool onOff = wSheet.GetRow(line).GetCell(2 + 3 + i * 39 + j * 6).BooleanCellValue;
            if (line == 3 || ic.onOff[ic.onOff.Count - 1].Item2 != onOff)
              ic.onOff.Add(Tuple.Create(dTime, onOff));

            //Mode
            string sMode = wSheet.GetRow(line).GetCell(2 + 4 + i * 39 + j * 6).StringCellValue;
            VRFCommunicator.Mode mode = 
              sMode == "Cool" ? VRFCommunicator.Mode.Cooling : VRFCommunicator.Mode.Heating;
            if (line == 3 || ic.mode[ic.mode.Count - 1].Item2 != mode)
              ic.mode.Add(Tuple.Create(dTime, mode));

            //Setpoint
            double sp = wSheet.GetRow(line).GetCell(2 + 5 + i * 39 + j * 6).NumericCellValue;
            if (line == 3 || ic.spTemp[ic.spTemp.Count - 1].Item2 != sp)
              ic.spTemp.Add(Tuple.Create(dTime, sp));

            //Fan speed
            string sFs = wSheet.GetRow(line).GetCell(2 + 6 + i * 39 + j * 6).StringCellValue;
            VRFCommunicator.FanSpeed fs = 
              sFs == "Low" ? VRFCommunicator.FanSpeed.Low : 
              sFs == "Middle" ? VRFCommunicator.FanSpeed.Middle : VRFCommunicator.FanSpeed.High;
            if (line == 3 || ic.fanSpeed[ic.fanSpeed.Count - 1].Item2 != fs)
              ic.fanSpeed.Add(Tuple.Create(dTime, fs));

            //Direction
            string sDir = wSheet.GetRow(line).GetCell(2 + 7 + i * 39 + j * 6).StringCellValue;
            VRFCommunicator.Direction dir = 
              sDir == "Horizontal" ? VRFCommunicator.Direction.Horizontal :
              sDir == "22.5deg" ? VRFCommunicator.Direction.Degree_225 :
              sDir == "45.0deg" ? VRFCommunicator.Direction.Degree_450:
              sDir == "67.5deg" ? VRFCommunicator.Direction.Degree_675 : VRFCommunicator.Direction.Vertical;
            if (line == 3 || ic.direction[ic.direction.Count - 1].Item2 != dir)
              ic.direction.Add(Tuple.Create(dTime, dir));

            //Permit control
            bool pmt = wSheet.GetRow(line).GetCell(2 + 8 + i * 39 + j * 6).BooleanCellValue;
            if (line == 3 || ic.permitRCtrl[ic.permitRCtrl.Count - 1].Item2 != pmt)
              ic.permitRCtrl.Add(Tuple.Create(dTime, pmt));
          }
        }

        line++;
      }
      Console.WriteLine(" done.");

      //コントローラ起動
      DeviceObject dObject = new DeviceObject(DEVICE_ID, "Controller who send control signal from excel.", "Controller who send control signal from excel.", true);
      communicator = new BACnetCommunicator(dObject, 0xBAC0 + DEVICE_ID);

      communicator.StartService();
      //COV通告登録処理
      communicator.Client.OnCOVNotification += Client_OnCOVNotification;
      BacnetAddress bacAddress = new BacnetAddress(BacnetAddressTypes.IP, "127.0.0.1:" + EXCLUSIVE_ACC.ToString());
      BacnetObjectId boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber_Acc.AccerarationRate);
      communicator.Client.SubscribeCOVRequest(bacAddress, boID, (uint)MemberNumber_Acc.AccerarationRate, false, false, 3600);

      //Who is送信
      communicator.Client.WhoIs();

      //制御値更新ループ*************************************
      while (true)
      {
        while (!dtimeInitialized) ; //日時が初期化されるまでは空ループ

        bool success;
        DateTime now = dtAcc.AcceleratedDateTime;
        for (int i = 0; i < 4; i++)
        {
          vrfCtrl vc = vrfCtrls[i];
          uint oUnitIndx = (uint)(i + 1);

          //冷媒温度設定
          if ((vc.refCtrlIndx < vc.refCtrl.Count) && (vc.refCtrl[vc.refCtrlIndx].Item1 < now))
          {
            Console.Write("Sending Forced Refrigerant Temperature status of VRF" + (i + 1) + "...");

            if(vc.refCtrl[vc.refCtrlIndx].Item2)
              vrfCom.EnableRefrigerantTemperatureControl(oUnitIndx, out success);
            else
              vrfCom.DisableRefrigerantTemperatureControl(oUnitIndx, out success);
            Console.WriteLine(success ? "succeeded." : "failed.");

            vc.refCtrlIndx++;
          }

          //蒸発温度設定
          if ((vc.evpTempIndx < vc.evpTemp.Count) && (vc.evpTemp[vc.evpTempIndx].Item1 < now))
          {
            Console.Write("Sending Evaporating Temperature Setpoint of VRF" + (i + 1) + "...");

            vrfCom.ChangeEvaporatingTemperature(oUnitIndx, vc.evpTemp[vc.evpTempIndx].Item2, out success);
            Console.WriteLine(success ? "succeeded." : "failed.");

            vc.evpTempIndx++;
          }

          //凝縮温度設定
          if ((vc.cndTempIndx < vc.cndTemp.Count) && (vc.cndTemp[vc.cndTempIndx].Item1 < now))
          {
            Console.Write("Sending Condensing Temperature Setpoint of VRF" + (i + 1) + "...");

            vrfCom.ChangeCondensingTemperature(oUnitIndx, vc.cndTemp[vc.cndTempIndx].Item2, out success);
            Console.WriteLine(success ? "succeeded." : "failed.");

            vc.cndTempIndx++;
          }

          //室内機別
          for (int j = 0; j < iHexCtrls[i].Length; j++)
          {
            iHexCtrl ic = iHexCtrls[i][j];
            uint iUnitIndx = (uint)(j + 1);

            //On/off
            if ((ic.onOffIndx < ic.onOff.Count) && (ic.onOff[ic.onOffIndx].Item1 < now))
            {
              Console.Write("Sending On/Off status of VRF" + (i + 1) + "-" + (j + 1) + "...");

              if(ic.onOff[ic.onOffIndx].Item2)
                vrfCom.TurnOn(oUnitIndx, iUnitIndx, out success);
              else
                vrfCom.TurnOff(oUnitIndx, iUnitIndx, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");
              
              ic.onOffIndx++;
            }

            //Mode
            if ((ic.modeIndx < ic.mode.Count) && (ic.mode[ic.modeIndx].Item1 < now))
            {
              Console.Write("Sending Operation mode of VRF" + (i + 1) + "-" + (j + 1) + "...");

              vrfCom.ChangeMode(oUnitIndx, iUnitIndx, ic.mode[ic.modeIndx].Item2, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");

              ic.modeIndx++;
            }

            //Setpoint
            if ((ic.spTempIndx < ic.spTemp.Count) && (ic.spTemp[ic.spTempIndx].Item1 < now))
            {
              Console.Write("Sending setpoint temperature of VRF" + (i + 1) + "-" + (j + 1) + "...");

              vrfCom.ChangeSetpointTemperature(oUnitIndx, iUnitIndx, ic.spTemp[ic.spTempIndx].Item2, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");

              ic.spTempIndx++;
            }

            //Fan speed
            if ((ic.fanSpeedIndx < ic.fanSpeed.Count) && (ic.fanSpeed[ic.fanSpeedIndx].Item1 < now))
            {
              Console.Write("Sending fan speed of VRF" + (i + 1) + "-" + (j + 1) + "...");

              vrfCom.ChangeFanSpeed(oUnitIndx, iUnitIndx, ic.fanSpeed[ic.fanSpeedIndx].Item2, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");

              ic.fanSpeedIndx++;
            }

            //Direction
            if ((ic.directionIndx < ic.direction.Count) && (ic.direction[ic.directionIndx].Item1 < now))
            {
              Console.Write("Sending air flow direction of VRF" + (i + 1) + "-" + (j + 1) + "...");

              vrfCom.ChangeDirection(oUnitIndx, iUnitIndx, ic.direction[ic.directionIndx].Item2, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");

              ic.directionIndx++;
            }

            //Permit control
            if ((ic.permitRCtrlIndx < ic.permitRCtrl.Count) && (ic.permitRCtrl[ic.permitRCtrlIndx].Item1 < now))
            {
              Console.Write("Sending remote controller permittion status of VRF" + (i + 1) + "-" + (j + 1) + "...");

              if (ic.permitRCtrl[ic.permitRCtrlIndx].Item2)
                vrfCom.PermitLocalControl(oUnitIndx, iUnitIndx, out success);
              else
                vrfCom.ProhibitLocalControl(oUnitIndx, iUnitIndx, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");

              ic.permitRCtrlIndx++;
            }
          }
        }

        Thread.Sleep(50);
      }

    }


    private static void Client_OnCOVNotification(BacnetClient sender, BacnetAddress adr, byte invokeId, uint subscriberProcessIdentifier, BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier, uint timeRemaining, bool needConfirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments maxSegments)
    {
      //加速度が変化した場合      
      UInt16 port = BitConverter.ToUInt16(new byte[] { adr.adr[5], adr.adr[4] });
      if (
        port == EXCLUSIVE_ACC &&
        monitoredObjectIdentifier.type == BacnetObjectTypes.OBJECT_ANALOG_OUTPUT &&
        monitoredObjectIdentifier.instance == (uint)MemberNumber_Acc.AccerarationRate)
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
            boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber_Acc.BaseAcceleratedDateTime);
            if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val1))
            {
              DateTime dt1 = (DateTime)val1[0].Value;
              DateTime dt2 = (DateTime)val1[1].Value;
              DateTime bAccDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

              //基準日時（現実時間）
              boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber_Acc.BaseRealDateTime);
              if (communicator.Client.ReadPropertyRequest(adr, boID, BacnetPropertyIds.PROP_PRESENT_VALUE, out IList<BacnetValue> val2))
              {
                dt1 = (DateTime)val2[0].Value;
                dt2 = (DateTime)val2[1].Value;
                DateTime bRealDTime = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt2.Hour, dt2.Minute, dt2.Second);

                //初期化
                dtAcc.InitDateTime(acc, bRealDTime, bAccDTime);
                dtimeInitialized = true;
              }
            }

            break;
          }
        }
      }
    }

    #region インナークラス定義

    private class vrfCtrl
    {
      /// <summary>refrigerant control</summary>
      public List<Tuple<DateTime, bool>> refCtrl = new List<Tuple<DateTime, bool>>();

      /// <summary>蒸発温度設定値</summary>
      public List<Tuple<DateTime, double>> evpTemp = new List<Tuple<DateTime, double>>();

      /// <summary>凝縮温度設定値</summary>
      public List<Tuple<DateTime, double>> cndTemp = new List<Tuple<DateTime, double>>();

      public int refCtrlIndx = 0;

      public int evpTempIndx = 0;

      public int cndTempIndx = 0;
    }

    private class iHexCtrl
    {
      /// <summary>on/off</summary>
      public List<Tuple<DateTime, bool>> onOff = new List<Tuple<DateTime, bool>>();

      /// <summary>運転モード</summary>
      public List<Tuple<DateTime, VRFCommunicator.Mode>> mode = new List<Tuple<DateTime, VRFCommunicator.Mode>>();

      /// <summary>温度設定値</summary>
      public List<Tuple<DateTime, double>> spTemp = new List<Tuple<DateTime, double>>();

      /// <summary>ファン風量</summary>
      public List<Tuple<DateTime, VRFCommunicator.FanSpeed>> fanSpeed = new List<Tuple<DateTime, VRFCommunicator.FanSpeed>>();

      /// <summary>羽根角度</summary>
      public List<Tuple<DateTime, VRFCommunicator.Direction>> direction = new List<Tuple<DateTime, VRFCommunicator.Direction>>();

      /// <summary>コントローラ許可</summary>
      public List<Tuple<DateTime, bool>> permitRCtrl = new List<Tuple<DateTime, bool>>();

      public int onOffIndx = 0;

      public int modeIndx = 0;

      public int spTempIndx = 0;

      public int fanSpeedIndx = 0;

      public int directionIndx = 0;

      public int permitRCtrlIndx = 0;

    }

    #endregion

  }
}