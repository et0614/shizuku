using BaCSharp;
using System.IO.BACnet;
using Shizuku.Models;
using Popolo.HumanBody;
using Popolo.ThermalLoad;

namespace Shizuku2.BACnet
{
  internal class OccupantMonitor: IBACnetController
  {

    //BACnet Object IDは以下のルールで付与
    //10000*テナント番号 + 1000*ゾーン番号 + Member Number
    //10000*テナント番号 + 10*執務者番号 + Member Number
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
      /// <summary>着衣量</summary>
      ClothingIndex = 4,
      /// <summary>熱的な不満足者率</summary>
      Dissatisfied_Thermal,
      /// <summary>ドラフトによる不満足者率</summary>
      Dissatisfied_Draft,
      /// <summary>上下温度分布による不満足者率</summary>
      Dissatisfied_VerticalTemp
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    private BACnetCommunicator communicator;

    private ImmutableTenantList tenants;

    #endregion

    #region コンストラクタ

    public OccupantMonitor(ImmutableTenantList tenants)
    {
      this.tenants = tenants;

      communicator = new BACnetCommunicator
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
          "Number of occupants stay in office (tenant-" + (i + 1) + ").", 0, BacnetUnitsId.UNITS_NO_UNITS));

        //ゾーン別
        ImmutableZone[] zones = tenants.Tenants[i].Zones;
        for (int j = 0; j < zones.Length; j++)
        {
          int baseNum = 10000 * (i + 1) + 1000 * (j + 1);

          //在室人数
          dObject.AddBacnetObject(new AnalogInput<int>
            (baseNum + (int)MemberNumber.OccupantNumber,
            "Occupant number_ZN" + (j + 1) + "_TNT" + (i + 1),
            "Number of occupants stay in zone-" + (j + 1) + " of tenant-" + (i + 1), 0, BacnetUnitsId.UNITS_NO_UNITS));

          //温冷感
          dObject.AddBacnetObject(new AnalogInput<float>
            (baseNum + (int)MemberNumber.ThermalSensation,
            "Ave_T_Sensation_ZN" + (j + 1) + "_TNT" + (i + 1),
            "Averaged thermal sensation of zone-" + (j + 1) + " of tenant-" + (i + 1), 0, BacnetUnitsId.UNITS_NO_UNITS));

          //着衣量
          dObject.AddBacnetObject(new AnalogInput<float>
            (baseNum + (int)MemberNumber.ClothingIndex,
          "Ave_Clo_ZN" + (j + 1) + "_TNT" + (i + 1),
            "Averaged clothing index of zone-" + (j + 1) + " of tenant-" + (i + 1), 0, BacnetUnitsId.UNITS_NO_UNITS));

          //熱的不満足者率
          dObject.AddBacnetObject(new AnalogInput<float>
            (baseNum + (int)MemberNumber.Dissatisfied_Thermal,
            "DissatisfiedRate_Thermal_ZN" + (j + 1) + "_TNT" + (i + 1),
            "Rate of thermally dissatisfied occupants of zone-" + (j + 1) + " of tenant-" + (i + 1), 0, BacnetUnitsId.UNITS_NO_UNITS));

          //ドラフトによる不満足者率
          dObject.AddBacnetObject(new AnalogInput<float>
            (baseNum + (int)MemberNumber.Dissatisfied_Draft,
            "DissatisfiedRate_Draft_ZN" + (j + 1) + "_TNT" + (i + 1),
            "Rate of dissatisfied occupants caused by draft of zone-" + (j + 1) + " of tenant-" + (i + 1), 0, BacnetUnitsId.UNITS_NO_UNITS));

          //上下温度分布による不満足者率
          dObject.AddBacnetObject(new AnalogInput<float>
            (baseNum + (int)MemberNumber.Dissatisfied_VerticalTemp,
            "DissatisfiedRate_VerticalTemp_ZN" + (j + 1) + "_TNT" + (i + 1),
            "Rate of dissatisfied occupants caused by vertical temperature difference of zone-" + (j + 1) + " of tenant-" + (i + 1), 0, BacnetUnitsId.UNITS_NO_UNITS));
        }

        //執務者別
        ImmutableOccupant[] ocs = tenants.Tenants[i].Occupants;
        for (int j = 0; j < ocs.Length; j++)
        {
          int baseNum = 10000 * (i + 1) + 10 * (j + 1);
          string name = " (" + ocs[j].FirstName + " " + ocs[j].LastName + ")";

          //在不在
          dObject.AddBacnetObject(new BinaryInput
            (baseNum + (int)MemberNumber.Availability,
            "Availability_OC_" + (j + 1),
            "Availability of occupant-" + (j + 1) + " of tenant-" + (i + 1) + name, false));

          //温冷感
          dObject.AddBacnetObject(new AnalogInput<int>
            (baseNum + (int)MemberNumber.ThermalSensation,
            "T_Sensation_OC_" + (j + 1),
            "Thermal sensation of occupant-" + (j + 1) + " of tenant-" + (i + 1) + name, 0, BacnetUnitsId.UNITS_NO_UNITS));

          //着衣量
          dObject.AddBacnetObject(new AnalogInput<float>
            (baseNum + (int)MemberNumber.ClothingIndex,
            "Clo_OC_" + (j + 1),
            "Clothing index of occupant-" + (j + 1) + " of tenant-" + (i + 1) + name, 0, BacnetUnitsId.UNITS_NO_UNITS));
        }
      }

      return dObject;
    }

    #endregion

    #region インスタンスメソッド

    public void OutputBACnetObjectInfo
      (out uint[] instances, out string[] types, out string[] names, out string[] descriptions, out string[] values)
    {
      List<string> tLst = new List<string>();
      List<uint> iLst = new List<uint>();
      List<string> nLst = new List<string>();
      List<string> dLst = new List<string>();
      List<string> vLst = new List<string>();
      foreach (BaCSharpObject bObj in communicator.BACnetDevice.ObjectsList)
      {
        tLst.Add(bObj.PROP_OBJECT_IDENTIFIER.type.ToString().Substring(7));
        iLst.Add(bObj.PROP_OBJECT_IDENTIFIER.instance);
        nLst.Add(bObj.PROP_OBJECT_NAME);
        dLst.Add(bObj.PROP_DESCRIPTION);
        IList<BacnetValue> bVal = bObj.FindPropValue("PROP_PRESENT_VALUE");
        if (bVal != null) vLst.Add(bVal[0].Value.ToString());
        else vLst.Add(null);
      }
      types = tLst.ToArray();
      instances = iLst.ToArray();
      names = nLst.ToArray();
      descriptions = dLst.ToArray();
      values = vLst.ToArray();
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime)
    { }

    public void EndService()
    {
      communicator.EndService();
    }

    public void ReadMeasuredValues(DateTime dTime)
    {
      BacnetObjectId boID;
      for (int i = 0; i < tenants.Tenants.Length; i++)
      {
        int baseNum = 10000 * (i + 1);

        //執務者の数
        boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.OccupantNumber));
        ((AnalogInput<int>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (int)tenants.Tenants[i].StayWorkerNumber;

        //ゾーン別
        ImmutableZone[] zones = tenants.Tenants[i].Zones;
        for (int j = 0; j < zones.Length; j++)
        {
          baseNum = 10000 * (i + 1) + 1000 * (j + 1);

          ImmutableOccupant[] ocs2 = tenants.Tenants[i].GetOccupants(zones[j]);
          int number = 0;
          double ths = 0;
          double clo = 0;
          for (int k = 0; k < ocs2.Length; k++)
          {
            if (ocs2[k].Worker.StayInOffice)
            {
              number++;
              ths += convertVote(ocs2[k].OCModel.Vote);
              clo += ocs2[k].CloValue;
            }
          }
          if (number != 0)
          {
            ths /= number;
            clo /= number;
          }

          //在室人数
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.OccupantNumber));
          ((AnalogInput<int>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = number;

          //温冷感
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.ThermalSensation));
          ((AnalogInput<float>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)ths;

          //着衣量
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.ClothingIndex));
          ((AnalogInput<float>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)clo;

          //熱的不満足者率
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.Dissatisfied_Thermal));
          ((AnalogInput<float>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)tenants.GetDissatisfactionRate_thermal(i,j);

          //ドラフトによる不満足者率
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.Dissatisfied_Draft));
          ((AnalogInput<float>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)tenants.GetDissatisfactionRate_draft(i,j);

          //上下温度分布による不満足者率
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.Dissatisfied_VerticalTemp));
          ((AnalogInput<float>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)tenants.GetDissatisfactionRate_vTempDif(i,j);
        }

        //執務者別
        ImmutableOccupant[] ocs = tenants.Tenants[i].Occupants;
        for (int j = 0; j < ocs.Length; j++)
        {
          baseNum = 10000 * (i + 1) + 10 * (j + 1);

          //在不在
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(baseNum + MemberNumber.Availability));
          ((BinaryInput)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = ocs[j].Worker.StayInOffice ? 1u : 0u;

          //温冷感
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.ThermalSensation));
          ((AnalogInput<int>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = convertVote(ocs[j].OCModel.Vote);

          //温冷感
          boID = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.ClothingIndex));
          ((AnalogInput<float>)communicator.BACnetDevice.FindBacnetObject(boID)).m_PROP_PRESENT_VALUE = (float)ocs[j].CloValue;
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
      communicator.StartService();
    }

    #endregion

  }
}
