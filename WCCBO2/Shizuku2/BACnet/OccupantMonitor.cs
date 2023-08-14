using BaCSharp;
using System.IO.BACnet;
using Shizuku.Models;
using Popolo.HumanBody;

namespace Shizuku2.BACnet
{
  internal class OccupantMonitor: IBACnetController
  {

    //BACnet Object IDは以下のルールで付与
    //10000*テナント番号 + 100*執務者番号 + Member Number
    //ただしテナント全体に関わる項目は執務者番号=0とする

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DEVICE_ID = 5;

    /// <summary>排他的ポート番号</summary>
    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    /// <summary>Deviceの名称</summary>
    const string DEVICE_NAME = "Occupant monitor";

    /// <summary>Deviceの説明</summary>
    const string DEVICE_DESCRIPTION = "BACnet device monitoring occupants state.";

    #endregion

    #region 列挙型

    /// <summary>項目</summary>
    public enum MemberNumber
    {
      /// <summary>執務者の数</summary>
      OccupantNumber = 1,
      /// <summary>在室状況</summary>
      Availability = 2,
      /// <summary>温冷感</summary>
      ThermalSensation = 3,
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator;

    private ImmutableTenantList tenants;

    #endregion

    #region コンストラクタ

    public OccupantMonitor(ImmutableTenantList tenants)
    {
      this.tenants = tenants;

      Communicator = new BACnetCommunicator
        (makeDeviceObject(), EXCLUSIVE_PORT);
    }

    /// <summary>BACnet Deviceを作成する</summary>
    private DeviceObject makeDeviceObject()
    {
      DeviceObject dObject = new DeviceObject(DEVICE_ID, DEVICE_NAME, DEVICE_DESCRIPTION, true);

      for (int i = 0; i < tenants.Tenants.Length; i++)
      {
        //執務者の数
        dObject.AddBacnetObject(new AnalogInput<int>
          (10000 * (i + 1) + (int)MemberNumber.OccupantNumber,
          "Occupant number",
          "Number of occupants stay in office.", 0, BacnetUnitsId.UNITS_NO_UNITS));

        ImmutableOccupant[] ocs = tenants.Tenants[i].Occupants;
        for (int j = 0; j < ocs.Length; j++)
        {
          int baseNum = 10000 * (i + 1) + 100 * (j + 1);

          //在不在
          dObject.AddBacnetObject(new BinaryInput
            (baseNum + (int)MemberNumber.Availability,
            "Availability of occupant-" + (j + 1),
            "Availability of occupant-" + (j + 1), false));

          //温冷感
          dObject.AddBacnetObject(new AnalogInput<int>
            (baseNum + (int)MemberNumber.ThermalSensation,
            "Thermal sensation of occupant-" + (j + 1),
            "Thermal sensation of occupant-" + (j + 1), 0, BacnetUnitsId.UNITS_NO_UNITS));
        }
      }

      return dObject;
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime)
    { }

    public void EndService()
    {
      Communicator.EndService();
    }

    public void ReadMeasuredValues(DateTime dTime)
    {
      BacnetObjectId boID;
      for (int i = 0; i < tenants.Tenants.Length; i++)
      {
        int baseNum = 10000 * (i + 1);

        //執務者の数
        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.OccupantNumber));
        ((AnalogInput<int>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (int)tenants.Tenants[i].StayWorkerNumber;

        ImmutableOccupant[] ocs = tenants.Tenants[i].Occupants;
        for (int j = 0; j < ocs.Length; j++)
        {
          baseNum = 10000 * (i + 1) + 100 * (j + 1);

          //在不在
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(baseNum + MemberNumber.Availability));
          ((BinaryInput)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = ocs[j].Worker.StayInOffice ? 1u : 0u;

          //温冷感
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.ThermalSensation));
          ((AnalogInput<int>)Communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = convertVote(ocs[j].OCModel.Vote);
        }
      }
    }

    private int convertVote(OccupantModel_Langevin.ASHRAE_Vote vote)
    {
      switch (vote)
      {
        case OccupantModel_Langevin.ASHRAE_Vote.Cold:
          return -3;
        case OccupantModel_Langevin.ASHRAE_Vote.Cool:
          return -2;
        case OccupantModel_Langevin.ASHRAE_Vote.SlightlyCool:
          return -1;
        case OccupantModel_Langevin.ASHRAE_Vote.Neutral:
          return 0;
        case OccupantModel_Langevin.ASHRAE_Vote.SlightlyWarm:
          return 1;
        case OccupantModel_Langevin.ASHRAE_Vote.Warm:
          return 2;
        case OccupantModel_Langevin.ASHRAE_Vote.Hot:
          return 3;
        default:
          return 0;
      }
    }

    public void StartService()
    {
      Communicator.StartService();
    }

    #endregion

  }
}
