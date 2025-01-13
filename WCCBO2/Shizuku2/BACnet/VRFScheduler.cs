using BaCSharp;

namespace Shizuku2.BACnet
{
  public class VRFScheduler : IBACnetController
  {

    #region 定数宣言

    const uint THIS_DEVICE_ID = 3;

    #endregion

    #region インスタンス変数・プロパティ

    VRFSystemCommunicator vrfCom;// = new VRFSystemCommunicator(THIS_DEVICE_ID, "WCCBO Original VRF scheduler");

    VentilationSystemCommunicator vntCom;// = new VentilationSystemCommunicator(THIS_DEVICE_ID + 10, "WCCBO Original HEX scheduler"); //ID適当

    #endregion

    #region コンストラクタ

    public VRFScheduler(ExVRFSystem[] vrfs, int accRate, DateTime now, string localEndPointIP)
    {
      vrfCom = new VRFSystemCommunicator(THIS_DEVICE_ID, "WCCBO Original VRF scheduler", localEndPointIP);
      vntCom = new VentilationSystemCommunicator(THIS_DEVICE_ID + 10, "WCCBO Original HEX scheduler", localEndPointIP); //ID適当
    }

    private void startScheduling()
    {
      try
      {
        //別スレッドでスケジュール設定
        Task.Run(() =>
        {
          DateTime lastDt = vrfCom.CurrentDateTime;

          while (true)
          {
            DateTime now = vrfCom.CurrentDateTime;

            //空調開始時
            if (!isHVACTime(lastDt) && isHVACTime(now))
            {
              bool isCooling = 5 <= now.Month && now.Month <= 10;

              for (uint oUnt = 1; oUnt <= VRFSystemCommunicator.VRFNumber; oUnt++)
              {
                for (uint iUnt = 1; iUnt <= VRFSystemCommunicator.GetIndoorUnitNumber((int)oUnt); iUnt++)
                {
                  //VRF************************                  
                  vrfCom.TurnOn(oUnt, iUnt, out _); //On/Off

                  vrfCom.ChangeMode(oUnt, iUnt, isCooling ? VRFSystemCommunicator.Mode.Cooling : VRFSystemCommunicator.Mode.Heating, out _); //Mode

                  vrfCom.ChangeSetpointTemperature(oUnt, iUnt, isCooling ? 26 : 22, out _); //SP

                  vrfCom.ChangeFanSpeed(oUnt, iUnt, VRFSystemCommunicator.FanSpeed.Middle, out _); //風量

                  vrfCom.ChangeDirection(oUnt, iUnt, VRFSystemCommunicator.Direction.Degree_450, out _); //角度

                  //HEX************************
                  vntCom.StartVentilation(oUnt, iUnt, out _); //On/Off

                  vntCom.DisableBypassControl(oUnt, iUnt, out _); //Bypass

                  vntCom.ChangeFanSpeed(oUnt, iUnt, VentilationSystemCommunicator.FanSpeed.High, out _); //ファン風量
                }
              }
            }
            //空調停止時
            else if (isHVACTime(lastDt) && !isHVACTime(now))
            {
              for (uint oHex = 1; oHex <= VRFSystemCommunicator.VRFNumber; oHex++)
              {
                for (uint iHex = 1; iHex <= VRFSystemCommunicator.GetIndoorUnitNumber((int)oHex); iHex++)
                {
                  vrfCom.TurnOff(oHex, iHex, out _); //VRF On/Off

                  vntCom.StopVentilation(oHex, iHex, out _); //HEX On/Off
                }
              }
            }

            lastDt = now;
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
      vrfCom.StartService();
      vntCom.StartService();
      vrfCom.SubscribeDateTimeCOV(); //日時更新
      startScheduling();
    }

    /// <summary>BACnetControllerのリソースを解放する</summary>
    public void EndService()
    {
      vrfCom.EndService();
      vntCom.EndService();
    }

    #endregion

  }
}
