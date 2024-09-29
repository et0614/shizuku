using BaCSharp;
using System.IO.BACnet;
using Shizuku.Models;
using Popolo.HumanBody;
using Popolo.ThermalLoad;

using System.IO.BACnet.Storage;
using System.Reflection;

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
      Dissatisfied_Thermal = 5,
      /// <summary>ドラフトによる不満足者率</summary>
      Dissatisfied_Draft = 6,
      /// <summary>上下温度分布による不満足者率</summary>
      Dissatisfied_VerticalTemp = 7
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator;

    private ImmutableTenantList tenants;

    #endregion

    #region コンストラクタ

    public OccupantMonitor(ImmutableTenantList tenants, string localEndpointIP)
    {
      this.tenants = tenants;      

      Communicator = new BACnetCommunicator(makeStorage(), EXCLUSIVE_PORT, localEndpointIP);
    }

    #endregion

    #region インスタンスメソッド

    private DeviceStorage makeStorage()
    {
      DeviceStorage strg = DeviceStorage.Load(
        new StreamReader
        (Assembly.GetExecutingAssembly().GetManifestResourceStream("Shizuku2.Resources.OccupantMonitorStorage.xml"))
        );

      for (int i = 0; i < tenants.Tenants.Length; i++)
      {
        //執務者の数
        strg.AddObject(new System.IO.BACnet.Storage.Object()
        {
          Instance = (uint)(10000 * (i + 1) + (int)MemberNumber.OccupantNumber),
          Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
          Properties = new Property[]
          {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Number of occupants stay in office (tenant-" + (i + 1) + ")."),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (10000 * (i + 1) + (int)MemberNumber.OccupantNumber)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Occupant number"),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"),
          }
        });

        //ゾーン別
        ImmutableZone[] zones = tenants.Tenants[i].Zones;
        for (int j = 0; j < zones.Length; j++)
        {
          int baseNum = 10000 * (i + 1) + 1000 * (j + 1);

          //在室人数
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(baseNum + (int)MemberNumber.OccupantNumber),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Number of occupants stay in zone-" + (j + 1) + " of tenant-" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (baseNum + (int)MemberNumber.OccupantNumber)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Occupant number_ZN" + (j + 1) + "_TNT" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"),
            }
          });

          //温冷感
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(baseNum + (int)MemberNumber.ThermalSensation),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Averaged thermal sensation of zone-" + (j + 1) + " of tenant-" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (baseNum + (int)MemberNumber.ThermalSensation)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Ave_T_Sensation_ZN" + (j + 1) + "_TNT" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"),
            }
          });

          //着衣量
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(baseNum + (int)MemberNumber.ClothingIndex),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Averaged clothing index of zone-" + (j + 1) + " of tenant-" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (baseNum + (int)MemberNumber.ClothingIndex)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Ave_Clo_ZN" + (j + 1) + "_TNT" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"),
            }
          });

          //熱的不満足者率
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(baseNum + (int)MemberNumber.Dissatisfied_Thermal),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Rate of thermally dissatisfied occupants of zone-" + (j + 1) + " of tenant-" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (baseNum + (int)MemberNumber.Dissatisfied_Thermal)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "DissatisfiedRate_Thermal_ZN" + (j + 1) + "_TNT" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"),
            }
          });

          //ドラフトによる不満足者率
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(baseNum + (int)MemberNumber.Dissatisfied_Draft),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Rate of dissatisfied occupants caused by draft of zone-" + (j + 1) + " of tenant-" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (baseNum + (int)MemberNumber.Dissatisfied_Draft)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "DissatisfiedRate_Draft_ZN" + (j + 1) + "_TNT" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"),
            }
          });

          //上下温度分布による不満足者率
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(baseNum + (int)MemberNumber.Dissatisfied_VerticalTemp),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Rate of dissatisfied occupants caused by vertical temperature difference of zone-" + (j + 1) + " of tenant-" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (baseNum + (int)MemberNumber.Dissatisfied_VerticalTemp)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "DissatisfiedRate_VerticalTemp_ZN" + (j + 1) + "_TNT" + (i + 1)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"),
            }
          });
        }

        //執務者別
        ImmutableOccupant[] ocs = tenants.Tenants[i].Occupants;
        for (int j = 0; j < ocs.Length; j++)
        {
          int baseNum = 10000 * (i + 1) + 10 * (j + 1);
          string name = " (" + ocs[j].FirstName + " " + ocs[j].LastName + ")";

          //在不在
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(baseNum + (int)MemberNumber.Availability),
            Type = BacnetObjectTypes.OBJECT_BINARY_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Availability of occupant-" + (j + 1) + " of tenant-" + (i + 1) + name),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_BINARY_INPUT:" + (baseNum + (int)MemberNumber.Availability)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Availability_OC_" + (j + 1)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "3"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "1"),
              new Property(BacnetPropertyIds.PROP_POLARITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
            }
          });

          //温冷感
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(baseNum + (int)MemberNumber.ThermalSensation),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Thermal sensation of occupant-" + (j + 1) + " of tenant-" + (i + 1) + name),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (baseNum + (int)MemberNumber.ThermalSensation)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "T_Sensation_OC_" + (j + 1)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"),
            }
          });

          //着衣量
          strg.AddObject(new System.IO.BACnet.Storage.Object()
          {
            Instance = (uint)(baseNum + (int)MemberNumber.ClothingIndex),
            Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
            Properties = new Property[]
            {
              new Property(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Clothing index of occupant-" + (j + 1) + " of tenant-" + (i + 1) + name),
              new Property(BacnetPropertyIds.PROP_EVENT_STATE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (baseNum + (int)MemberNumber.ClothingIndex)),
              new Property(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "Clo_OC_" + (j + 1)),
              new Property(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
              new Property(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, "0"),
              new Property(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
              new Property(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
              new Property(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"),
            }
          });
        }
      }

      return strg;
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
      for (int i = 0; i < tenants.Tenants.Length; i++)
      {
        int baseNum = 10000 * (i + 1);

        //執務者の数
        Communicator.Storage.WriteProperty(
          new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.OccupantNumber)),
          BacnetPropertyIds.PROP_PRESENT_VALUE,
          new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)tenants.Tenants[i].StayWorkerNumber)
          );

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
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.OccupantNumber)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)number)
            );

          //温冷感
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.ThermalSensation)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)ths)
            );

          //着衣量
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.ClothingIndex)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)clo)
            );

          //熱的不満足者率
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.Dissatisfied_Thermal)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)tenants.GetDissatisfactionRate_thermal(i, j))
            );

          //ドラフトによる不満足者率
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.Dissatisfied_Draft)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)tenants.GetDissatisfactionRate_draft(i, j))
            );

          //上下温度分布による不満足者率
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.Dissatisfied_VerticalTemp)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)tenants.GetDissatisfactionRate_vTempDif(i, j))
            );
        }

        //執務者別
        ImmutableOccupant[] ocs = tenants.Tenants[i].Occupants;
        for (int j = 0; j < ocs.Length; j++)
        {
          baseNum = 10000 * (i + 1) + 10 * (j + 1);

          //在不在
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)(baseNum + MemberNumber.Availability)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, ocs[j].Worker.StayInOffice ? 1u : 0u)
            );

          //温冷感
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.ThermalSensation)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)convertVote(ocs[j].OCModel.Vote))
            );

          //着衣量
          Communicator.Storage.WriteProperty(
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(baseNum + MemberNumber.ClothingIndex)),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)ocs[j].CloValue)
            );
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
