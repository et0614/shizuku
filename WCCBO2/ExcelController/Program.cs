using NPOI.SS.UserModel;
using Shizuku2.BACnet;

namespace ExcelController
{
  internal class Program
  {

    #region 定数宣言

    /// <summary>自身のDevice ID</summary>
    const int DEVICE_ID = 7000;

    const string FILE_NAME = "schedule.xlsx";

    const string SHEET_NAME = "schedule";

    #endregion

    static void Main(string[] args)
    {
      /*OccupantCommunicator cm = new OccupantCommunicator(999, "", "");
      cm.StartService();
      while (true)
      {
        int ocNum = cm.GetOccupantNumber(OccupantCommunicator.Tenant.SouthEast, out bool s1);
        bool stay = cm.IsOccupantStayInOffice(OccupantCommunicator.Tenant.SouthEast, 1, out bool s2);
        OccupantCommunicator.ThermalSensation ss = cm.GetThermalSensation(OccupantCommunicator.Tenant.SouthWest, 1, out bool s3);

        Console.WriteLine(ocNum.ToString("F0") + ", " + (stay ? "Available" : "Vacant") + ", " + ss.ToString());

        Thread.Sleep(1000);
      }*/

      Console.WriteLine("Starting Excel controller.");

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
          float evpTemp = (float)wSheet.GetRow(line).GetCell(2 + 1 + i * 39).NumericCellValue;
          if (line == 3 || vc.evpTemp[vc.evpTemp.Count - 1].Item2 != evpTemp)
            vc.evpTemp.Add(Tuple.Create(dTime, evpTemp));

          //凝縮温度
          float cndTemp = (float)wSheet.GetRow(line).GetCell(2 + 2 + i * 39).NumericCellValue;
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
            float sp = (float)wSheet.GetRow(line).GetCell(2 + 5 + i * 39 + j * 6).NumericCellValue;
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

      //コントローラを用意して開始
      VRFCommunicator vrfCom = new VRFCommunicator(DEVICE_ID + 1, "Excel controller", "Controller who send control signal from excel data file.");
      DateTimeCommunicator dtCom = new DateTimeCommunicator(DEVICE_ID + 2, "Excel controller (Date time ctrl.)", "Excel controller (Date time ctrl.)");
      vrfCom.StartService();
      dtCom.StartService();

      //制御値更新ループ*************************************
      while (true)
      {
        while (!dtCom.DateTimeInitialized) ; //日時が初期化されるまでは空ループ

        bool success;
        DateTime now = dtCom.CurrentDateTimeInSimulation;
        for (int i = 0; i < 4; i++)
        {
          vrfCtrl vc = vrfCtrls[i];
          uint oUnitIndx = (uint)(i + 1);

          //冷媒温度設定
          if ((vc.refCtrlIndx < vc.refCtrl.Count) && (vc.refCtrl[vc.refCtrlIndx].Item1 < now))
          {
            Console.Write("Sending Forced Refrigerant Temperature status of VRF" + oUnitIndx + "...");

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
            Console.Write("Sending Evaporating Temperature Setpoint of VRF" + oUnitIndx + "...");

            vrfCom.ChangeEvaporatingTemperature(oUnitIndx, vc.evpTemp[vc.evpTempIndx].Item2, out success);
            Console.WriteLine(success ? "succeeded." : "failed.");

            vc.evpTempIndx++;
          }

          //凝縮温度設定
          if ((vc.cndTempIndx < vc.cndTemp.Count) && (vc.cndTemp[vc.cndTempIndx].Item1 < now))
          {
            Console.Write("Sending Condensing Temperature Setpoint of VRF" + oUnitIndx + "...");

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
              if (ic.onOff[ic.onOffIndx].Item2)
              {
                Console.Write("Turning on VRF" + oUnitIndx + "-" + iUnitIndx + "...");
                vrfCom.TurnOn(oUnitIndx, iUnitIndx, out success);
              }
              else
              {
                Console.Write("Turning off VRF" + oUnitIndx + "-" + iUnitIndx + "...");
                vrfCom.TurnOff(oUnitIndx, iUnitIndx, out success);
              }
              Console.WriteLine(success ? "succeeded." : "failed.");
              
              ic.onOffIndx++;
            }

            //Mode
            if ((ic.modeIndx < ic.mode.Count) && (ic.mode[ic.modeIndx].Item1 < now))
            {
              Console.Write("Sending Operation mode of VRF" + oUnitIndx + "-" + iUnitIndx + "...");

              vrfCom.ChangeMode(oUnitIndx, iUnitIndx, ic.mode[ic.modeIndx].Item2, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");

              ic.modeIndx++;
            }

            //Setpoint
            if ((ic.spTempIndx < ic.spTemp.Count) && (ic.spTemp[ic.spTempIndx].Item1 < now))
            {
              Console.Write("Sending setpoint temperature of VRF" + oUnitIndx + "-" + iUnitIndx + "...");

              vrfCom.ChangeSetpointTemperature(oUnitIndx, iUnitIndx, ic.spTemp[ic.spTempIndx].Item2, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");

              ic.spTempIndx++;
            }

            //Fan speed
            if ((ic.fanSpeedIndx < ic.fanSpeed.Count) && (ic.fanSpeed[ic.fanSpeedIndx].Item1 < now))
            {
              Console.Write("Sending fan speed of VRF" + oUnitIndx + "-" + iUnitIndx + "...");

              vrfCom.ChangeFanSpeed(oUnitIndx, iUnitIndx, ic.fanSpeed[ic.fanSpeedIndx].Item2, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");

              ic.fanSpeedIndx++;
            }

            //Direction
            if ((ic.directionIndx < ic.direction.Count) && (ic.direction[ic.directionIndx].Item1 < now))
            {
              Console.Write("Sending air flow direction of VRF" + oUnitIndx + "-" + iUnitIndx + "...");

              vrfCom.ChangeDirection(oUnitIndx, iUnitIndx, ic.direction[ic.directionIndx].Item2, out success);
              Console.WriteLine(success ? "succeeded." : "failed.");

              ic.directionIndx++;
            }

            //Permit control
            if ((ic.permitRCtrlIndx < ic.permitRCtrl.Count) && (ic.permitRCtrl[ic.permitRCtrlIndx].Item1 < now))
            {
              Console.Write("Sending remote controller permittion status of VRF" + oUnitIndx + "-" + iUnitIndx + "...");

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

    #region インナークラス定義

    private class vrfCtrl
    {
      /// <summary>refrigerant control</summary>
      public List<Tuple<DateTime, bool>> refCtrl = new List<Tuple<DateTime, bool>>();

      /// <summary>蒸発温度設定値</summary>
      public List<Tuple<DateTime, float>> evpTemp = new List<Tuple<DateTime, float>>();

      /// <summary>凝縮温度設定値</summary>
      public List<Tuple<DateTime, float>> cndTemp = new List<Tuple<DateTime, float>>();

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
      public List<Tuple<DateTime, float>> spTemp = new List<Tuple<DateTime, float>>();

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